# ADR 0006 — Proveedor de IA conmutable con asignación de respaldo

**Estado:** Aprobado
**Fecha:** 2026-09-02
**Amplía:** documento del TFI, secciones 1.7, 6.4.1 y 10.2.3 (ver `docs/CAMBIOS-DOCUMENTO.md`, D-03 y D-04)

## Contexto

El documento define la asignación automática de incidencias mediante un servicio
externo de IA (RF-08, CU-007) y su comportamiento ante falla (CP-14). El alcance se
amplía además con la clasificación del texto libre del solicitante (D-03/D-04).

Eso introduce en el camino crítico del sistema una dependencia que el sistema **no
controla**: un servicio de terceros, por red, con latencia variable, que puede estar
caído, devolver algo malformado o —el caso más incómodo— responder correctamente
pero con el nombre de un técnico que no existe.

Hay además dos restricciones prácticas:

1. **No hay crédito de API disponible.** Un sistema que sólo funciona con una clave
   cargada no se puede demostrar en la defensa si ese día la cuenta no tiene saldo o
   la red del aula falla.
2. **CP-14 exige poder provocar la caída del servicio** para verificar la asignación
   de respaldo. Depender de un servicio real hace ese caso de prueba no
   determinístico, y un caso de prueba que a veces pasa no prueba nada.

## Decisión

La integración se define detrás de una interfaz de dominio, **`IProveedorIA`**, con
dos operaciones: clasificar una incidencia y recomendar un técnico. Se implementa
dos veces:

- **`ProveedorIAClaude`** — consume la API de Anthropic con salida JSON estructurada.
- **`ProveedorIASimulado`** — heurística determinística, sin red, y con la capacidad
  de simular fallas y demoras a pedido.

La implementación activa se elige por configuración **por organización**
(`ConfiguracionAsignacion.proveedor_ia`), no por variable de entorno global: es el
cliente el que decide si sus datos salen o no hacia un tercero. El valor por defecto
es `SIMULADO`.

Sobre la llamada real se aplican tres protecciones, en este orden:

1. **Timeout** configurable, 15 segundos por defecto.
2. **Validación de la respuesta** contra el conjunto de técnicos que efectivamente
   existen, están activos y pertenecen a la organización. Una recomendación que
   nombra a alguien fuera de ese conjunto se descarta como si el servicio hubiera
   fallado.
3. **Asignación de respaldo**: técnico con menor carga, `tipo_asignacion = RESPALDO`,
   incidencia marcada `pendiente_revision`, y evento en bitácora con el motivo.

El resultado de la IA es siempre **una sugerencia editable**, nunca un hecho
consumado: la clasificación se guarda en `ClasificacionIncidencia` separada de los
campos reales de la incidencia, y la recomendación en `RecomendacionAsignacion`
junto con el contexto exacto que se envió.

Ningún flujo del sistema queda bloqueado esperando a la IA. Si el servicio no está,
el sistema funciona con heurísticas y lo dice.

## Alternativas consideradas

| Opción | Por qué se descartó |
|---|---|
| Llamar a la API directamente desde la BLL | Ata el sistema a un proveedor, hace las pruebas dependientes de la red y vuelve CP-14 no reproducible. |
| Simular en las pruebas con un *mock* y usar sólo la implementación real en producción | El *mock* prueba el código de prueba. Acá el modo simulado es también un modo de operación legítimo: es como se demuestra el sistema sin crédito de API. |
| Entrenar un modelo propio sobre el historial | Fuera del alcance declarado en 6.4.1, y sin volumen de datos que lo justifique: una PyME con 50 equipos no genera un conjunto de entrenamiento. |
| Cola asincrónica: registrar la incidencia y asignar después | Elimina la latencia del camino crítico, pero agrega una pieza de infraestructura desproporcionada y rompe la expectativa de CU-007, que asigna al registrar. |

## Consecuencias

**Positivas**

- El sistema es demostrable siempre, con o sin conexión y con o sin crédito.
- CP-14 pasa a ser un caso de prueba determinístico y automatizable: se fuerza la
  falla en el proveedor simulado y se verifica el respaldo.
- Cambiar de proveedor de IA es escribir una clase nueva, sin tocar la BLL.
- `RecomendacionAsignacion` guarda el contexto enviado, lo que permite auditar
  después qué se le mandó a un tercero y por qué recomendó lo que recomendó.

**Negativas y riesgos**

- **Hay dos caminos de código y sólo uno se ejercita en la demostración.** Si sólo
  se prueba el simulado, la implementación real puede estar rota sin que nadie se
  entere. Se mitiga con un conjunto de pruebas de contrato que ambas
  implementaciones deben satisfacer, y una prueba de integración contra el servicio
  real marcada para ejecución manual.
- El modo simulado puede dar una impresión inflada de la capacidad del sistema si no
  se aclara en la defensa cuál está activo. La pantalla de Integración de IA muestra
  el proveedor en uso, y ése es el punto donde se aclara.
- Guardar el contexto enviado implica almacenar datos sobre la carga de trabajo de
  personas identificables. Es información laboral sensible: se guarda porque hace
  auditable la decisión, y se acota con las casillas de la pantalla de configuración,
  donde el cliente elige qué se envía.
- La validación de la respuesta contra los técnicos reales es imprescindible: sin
  ella, una respuesta plausible pero inventada asignaría la incidencia a nadie.
