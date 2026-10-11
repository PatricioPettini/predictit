# ADR 0010 — Cifrado reversible, separado del hash de contraseñas

**Estado:** Aprobado
**Fecha:** 2026-09-02
**Origen:** auditoría contra el modelo de referencia de 3.º año (CU.Arq.006)

## Contexto

El sistema tenía una sola primitiva criptográfica: PBKDF2-HMAC-SHA256 sobre las
contraseñas, con 120 000 iteraciones y el formato `PBKDF2$iter$salt$hash` del
proyecto de referencia. Alcanza para autenticar y no alcanza para nada más,
porque un hash es de un solo sentido por definición.

El caso que lo dejó en evidencia fue la clave de API del proveedor de IA. La
tabla `ConfiguracionAsignacion` tiene desde el principio una columna
`api_key_cifrada`, y estaba **vacía**: la clave se leía de la variable de entorno
`ANTHROPIC_API_KEY`, con este comentario en `Program.cs`:

> La clave sale de la variable de entorno y no de la columna `api_key_cifrada`:
> todavía no hay servicio de cifrado, y guardar una clave en claro en la base
> sería peor que no guardarla.

El comentario era correcto y la solución, provisoria. Tiene dos consecuencias que
importan al producto, no sólo a la arquitectura:

1. **Una sola clave para todas las organizaciones.** El plan Partner vende
   atender a varias PyMEs; con la clave en el entorno, todas comparten la
   credencial de quien opera el servidor, y el consumo de una organización se
   factura contra la cuenta de otra.
2. **El cliente no puede administrar su propia credencial.** Cargar una clave
   exige acceso al servidor, que es exactamente lo que un servicio en la nube no
   le da a su cliente.

Además, CU.Arq.006 del modelo de referencia pide encriptación y desencriptación
como caso de uso propio, distinto del hash.

## Decisión

Se agrega `ICifradoService`, **separado** de `HashContrasena` y sin reemplazarlo.

**Los dos mecanismos coexisten porque resuelven problemas distintos.** Una
contraseña no hace falta recuperarla: el sistema sólo necesita verificarla, y
poder recuperarla sería una debilidad. Una credencial de un tercero sí hay que
recuperarla, porque hay que enviarla en cada llamada. Usar hash para la clave de
API sería inútil; usar cifrado para las contraseñas sería peor que inútil.

**Algoritmo: AES-256 en modo GCM.** GCM cifra y autentica en la misma operación:
un texto alterado no se descifra a basura en silencio, falla, porque la etiqueta
de autenticación no coincide. Con AES-CBC habría que sumar un HMAC aparte para
obtener lo mismo, y omitirlo es el error clásico de esta clase de código.

**Formato: `AESGCM$v1$nonce$etiqueta$cifrado`**, las tres partes en Base64. Sigue
la forma del hash de contraseñas para que un valor almacenado se reconozca de un
vistazo, y lleva versión adelante para poder rotar el algoritmo sin tener que
adivinar con qué se cifró cada fila.

**Nonce aleatorio por operación.** Repetir un nonce con la misma clave es la
única forma de romper GCM sin romper AES. El efecto observable es que cifrar dos
veces el mismo texto da resultados distintos. Si no fuera así,
alguien con acceso a la base sabría que dos organizaciones cargaron la misma
clave sin necesidad de leerla.

**Sin clave configurada, el servicio se niega a cifrar.** No guarda en claro «para
no romper». Que el sistema quede sin credencial es recuperable; una credencial de
un tercero en claro en la base, no.

**La clave de cifrado es distinta de la de firma de tokens.** Quien pueda firmar
tokens no tiene por qué poder leer las credenciales de los clientes. `arrancar.ps1`
genera las dos por separado, y ninguna se versiona.

### Cómo queda la clave de API

- Se guarda cifrada en `ConfiguracionAsignacion.api_key_cifrada`, **por
  organización**.
- La clave guardada **gana** sobre la variable de entorno: si el cliente cargó la
  suya, es la que quiso usar. El entorno queda como respaldo para desarrollo.
- **No hay endpoint que la devuelva.** Se puede cargar y borrar. Lo que se
  consulta es de dónde sale la que está en uso —`BASE`, `ENTORNO` o `NINGUNA`—,
  porque eso cambia quién la controla, y nunca su valor.
- La bitácora asienta el hecho de que se cargó una clave y jamás el valor, ni
  recortado: los primeros caracteres de una credencial ya son información sobre
  ella.
- Si la clave de cifrado se rota sin volver a cargar las credenciales, el
  descifrado falla; se asienta el error y el sistema cae al proveedor simulado.
  Es preferible asignar con heurísticas y decirlo, a no asignar.

## Consecuencias

**A favor**

- Cada organización administra su propia credencial, condición necesaria para que el plan
  Partner pueda para facturar el consumo donde corresponde.
- La credencial no viaja en texto plano ni en la base, ni en la bitácora, ni en
  ninguna respuesta de la API.
- El sistema falla ruidosamente ante un dato alterado, en vez de operar con
  basura.
- CU.Arq.006 queda cubierto con un caso de uso real y no con una demostración.

**En contra**

- Aparece una clave más para administrar en el despliegue, y perderla vuelve
  ilegibles las credenciales guardadas. Se mitiga documentándolo en la plantilla
  de configuración y generándola automáticamente en `arrancar.ps1`, pero el
  riesgo operativo es real y no se elimina.
- El cifrado es simétrico: quien tenga a la vez la base y la clave puede
  descifrar. Un esquema de sobres con una clave maestra externa —KMS— sería más
  fuerte, y queda fuera de alcance porque supone infraestructura de nube que el
  proyecto no tiene.
- No hay rotación automatizada. Rotar hoy es cambiar la clave y volver a cargar
  las credenciales a mano. Con una sola credencial por organización es aceptable;
  con más habría que escribir la migración.

## Alternativas descartadas

**Seguir con la variable de entorno.** Es lo que había. No resuelve la
multi-organización ni deja al cliente administrar su clave, y deja `api_key_cifrada`
como una columna muerta que el DER promete y el sistema no cumple.

**Windows DPAPI.** Menos código, y ata el sistema a Windows y a la máquina: la
base respaldada y restaurada en otro servidor dejaría de poder descifrarse. El
proyecto se despliega en Windows hoy, pero nada del resto de la arquitectura lo
exige.

**AES-CBC + HMAC.** Equivalente en fuerza si se implementa bien, y el «si se
implementa bien» es justamente el problema: hay que encadenar cifrado y
autenticación en el orden correcto y comparar el HMAC en tiempo constante. GCM lo
resuelve en una operación.

**Cifrar también las contraseñas, para tener un solo mecanismo.** Sería una
regresión de seguridad grave: volvería recuperable algo que no tiene por qué
serlo. La uniformidad no es un valor cuando los problemas son distintos.
