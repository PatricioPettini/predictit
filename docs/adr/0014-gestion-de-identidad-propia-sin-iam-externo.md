# ADR 0014 — Gestión de identidad propia, sin IAM externo

**Estado:** Aprobado
**Fecha:** 2026-09-03
**Origen:** H-46. La pregunta apareció al revisar los requisitos de la cátedra,
que mencionan Keycloak como opción

## Contexto

El sistema resuelve la identidad por su cuenta: usuarios en
`PredictIT_Servicio`, contraseñas derivadas con PBKDF2-HMAC-SHA256 a 120.000
iteraciones, sesión con JWT firmado, y permisos en el modelo de familias y
patentes que viene del proyecto de referencia de la carrera.

La alternativa evaluada fue delegar todo eso en un gestor de identidad externo
—Keycloak es el candidato natural: es libre, se despliega en un contenedor y
trae OpenID Connect, federación, autenticación en dos pasos y una consola de
administración que ya está escrita.

La pregunta importa porque **el modelo de permisos es lo que la cátedra
evalúa**. No es una pieza de infraestructura cualquiera.

## Decisión

**No se incorpora un IAM externo.** La identidad y la autorización quedan en el
sistema.

### El motivo principal no es técnico

El modelo de familias y patentes, con el composite anidado y su guarda de
ciclos, **es contenido de la materia**. Delegarlo en Keycloak lo convertiría en
un archivo de configuración: los permisos pasarían a ser roles y scopes de un
producto de terceros, y el composite —que es el patrón que el trabajo tiene que
mostrar aplicado— desaparecería del código.

Un trabajo final que resuelve la autorización configurando un producto no
demuestra lo mismo que uno que la implementa. Y lo que se pide demostrar acá es
lo segundo.

### Lo que además pesa

- **Dos fuentes de verdad.** El sistema es multi-organización y el aislamiento
  se aplica en el acceso a datos a partir del contexto de sesión. Con un IAM
  externo, la organización de un usuario viviría en Keycloak y las
  organizaciones vivirían en la base de negocio: dos lugares que hay que
  mantener sincronizados, y cualquier diferencia entre ellos se resuelve por
  accidente.
- **Un contenedor más y una dependencia de arranque.** Hoy el sistema levanta
  con cuatro contenedores y un comando. Keycloak agrega un quinto, su propia
  base, y un orden de arranque: sin el IAM disponible, nadie entra. Para una
  PyME que despliega esto en un servidor propio, es una pieza más que puede
  fallar un lunes a la mañana.
- **La revocación inmediata se pierde o se complica.** El token de PredictIT no
  lleva las patentes: se consultan en cada pedido, así que quitarle un permiso a
  alguien tiene efecto en la llamada siguiente. Un token de OIDC lleva los
  claims adentro, y revocar antes del vencimiento exige introspección en cada
  pedido —que es lo mismo que hace hoy el sistema, con un salto de red más— o
  aceptar que el permiso viejo siga valiendo hasta que el token expire.

## Lo que se pierde, y hay que decirlo

Esto no es gratis. Delegando en Keycloak se tendrían, sin escribir código:

- **Autenticación en dos pasos.** El sistema no la tiene y no está planificada.
  Es la ausencia más significativa de esta decisión.
- **Federación e inicio de sesión único.** Entrar con la cuenta corporativa de
  Google o de Microsoft. Para el segmento PyME es una función que se pide.
- **Recuperación de contraseña por correo**, con sus plantillas y sus tokens de
  un solo uso. Hoy el restablecimiento lo hace un administrador.
- **Políticas de contraseña configurables** por organización.
- **Auditoría de autenticación ya hecha**, con su consola.

De esa lista, la que más duele es la de dos pasos. La mitiga en parte el bloqueo
por intentos —cinco fallidos y la cuenta se cierra— más el límite por dirección
de origen que se agregó en H-45, pero mitigar no es lo mismo que tener.

## Consecuencias

- El código de seguridad es propio, así que los defectos de seguridad también
  son propios. Es lo que hace obligatoria la revisión contra OWASP Top 10 de
  `docs/tecnico/REVISION-OWASP.md`: sin un producto de terceros que se ocupe,
  la revisión no la hace nadie más.
- Cambiar de opinión más adelante es caro pero no imposible. `ISeguridadService`
  concentra la verificación de credenciales y `IContextoSesion` la resolución de
  permisos: un adaptador contra un IAM externo entraría por ahí. Lo que no se
  puede migrar sin rediseño es el modelo de familias y patentes, porque no tiene
  equivalente directo en los roles y scopes de OIDC.
- La decisión se revisa si el sistema sale de la etapa académica. En un
  despliegue comercial real, la falta de dos pasos y de inicio de sesión único
  pasa a ser un problema de venta, no una decisión de diseño.

## Alternativas descartadas

**Keycloak completo.** Descartada por el motivo principal de arriba.

**Keycloak sólo para autenticar, permisos propios.** Es la opción intermedia y
la más tentadora: se gana dos pasos y federación, y el composite se queda en el
sistema. Se descartó porque el costo de infraestructura —el contenedor, la base,
el orden de arranque— se paga igual y completo, y a cambio se resuelve sólo la
mitad del problema. Es la que habría que reconsiderar primero si el proyecto
sale de la facultad.

**ASP.NET Core Identity.** Trae el manejo de usuarios y contraseñas de
Microsoft, sin contenedor extra. Se descartó porque impone su propio esquema de
tablas, y el DER de seguridad del capítulo 10 es parte de lo que se evalúa: no
se puede reemplazar por el de otro.
