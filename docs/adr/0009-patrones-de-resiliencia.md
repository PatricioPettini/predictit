# ADR 0009 — Patrones de resiliencia: circuit breaker y bulkhead

**Estado:** Aprobado
**Fecha:** 2026-09-02
**Origen:** auditoría de arquitectura, `docs/trazabilidad/PROMPTS-CRITICOS.md` · PC-05

## Contexto

La auditoría del diseño previa a la implementación (patrón B, abogado del diablo)
encontró dos problemas que el diseño de los ADR 0002 a 0007 no resolvía.

**El usuario espera a la IA.** CU-006 registra la incidencia y CU-007 se dispara de
forma sincrónica. Con el timeout de 15 segundos del ADR 0006, un solicitante puede
esperar 15 segundos para que le confirmen que su pedido se registró. Peor: si el
servicio está caído, **cada** incidencia paga esos 15 segundos. Veinte reportes
seguidos en una mañana de corte de internet son cinco minutos de espera acumulada,
todos terminando en el mismo respaldo previsible.

El timeout protege al sistema de colgarse, pero no lo protege de un servicio que
está consistentemente caído. Es la diferencia entre fallar rápido una vez y fallar
lento muchas veces.

**Una evaluación larga puede dejar sin atender a los usuarios.** El motor predictivo
evaluando el parque completo abre conexiones sostenidas contra la base de negocio.
Con el pool compartido, esa evaluación puede consumir las conexiones disponibles y
las peticiones de los usuarios quedan esperando. Un proceso en segundo plano no
debería poder degradar la operación interactiva.

## Decisión

Se incorporan dos patrones de resiliencia, acotados a los dos puntos identificados.
No se adopta una política general de resiliencia: cada patrón responde a un hallazgo
concreto.

### Circuit Breaker sobre `IProveedorIA`

Un disyuntor delante de la llamada al servicio externo, con tres estados:

- **Cerrado** — las llamadas pasan. Se cuentan los fallos consecutivos.
- **Abierto** — tras **5 fallos consecutivos**, el circuito abre y durante **60
  segundos** las llamadas **no se intentan**: se va directo a la asignación de
  respaldo, sin esperar el timeout.
- **Semiabierto** — cumplido ese lapso, se deja pasar una llamada de prueba. Si
  responde, el circuito cierra; si falla, vuelve a abrir.

Cuenta como fallo lo mismo que ya define el ADR 0006: error, timeout, respuesta
malformada, o respuesta que nombra un técnico inexistente.

La apertura y el cierre del circuito se asientan en bitácora, y el estado se muestra
en la pantalla de Integración de IA: *"Sin respuesta desde hace 12 min · se está
aplicando la regla de respaldo"*. Que el administrador vea esto es parte de la
decisión: un sistema degradado que no lo dice deja al administrador
sin motivo para revisar nada.

### Bulkhead para los procesos en segundo plano

Las operaciones interactivas y los procesos en segundo plano —evaluación del parque
completo, respaldos automáticos— usan **cadenas de conexión con pools separados**
(`Max Pool Size` propio en cada una). Una evaluación larga puede agotar su propio
pool; no puede tocar el de los usuarios.

Es la mitigación más barata posible para este hallazgo: no requiere infraestructura,
sólo dos cadenas de conexión distintas hacia la misma base.

## Alternativas consideradas

| Opción | Por qué se descartó |
|---|---|
| Sólo timeout, sin circuit breaker | Es lo que ya define el ADR 0006 y es exactamente lo que la auditoría encontró insuficiente: protege de colgarse, no de fallar lento repetidamente. |
| Retry con backoff exponencial sobre la llamada de IA | Multiplica la latencia que es el problema a resolver. Se conserva sólo para la reevaluación en segundo plano, donde nadie está esperando. |
| Asignación asincrónica por cola | Elimina el problema de raíz, pero agrega una pieza de infraestructura desproporcionada para el alcance y rompe la expectativa de CU-007, que asigna al registrar. |
| Biblioteca de resiliencia completa (Polly) | Resuelve esto y mucho más, pero un disyuntor de tres estados son unas pocas decenas de líneas y acá conviene que el patrón esté escrito y sea señalable en la defensa, no delegado. |

## Consecuencias

**Positivas**

- Con el servicio caído, el registro de una incidencia responde en el tiempo normal
  en lugar de esperar el timeout. El sistema degrada de forma predecible.
- La degradación es **visible**: queda en bitácora y se muestra en pantalla.
- El disyuntor es fácil de demostrar: forzando fallos en el proveedor simulado se
  puede mostrar la transición de los tres estados. Se suma a CP-14 como caso de
  prueba propio.
- El bulkhead elimina una clase entera de incidentes —"el sistema se puso lento a las
  3 de la mañana"— por el costo de una cadena de conexión adicional.

**Negativas y riesgos**

- **Con el circuito abierto se dejan de intentar llamadas que podrían funcionar.**
  Si el servicio se recupera al segundo 5, el sistema no lo aprovecha hasta el 60.
  Es el costo aceptado del patrón, y el estado semiabierto lo acota.
- Los umbrales —5 fallos, 60 segundos— son una elección inicial sin datos que la
  respalden. Quedan configurables y hay que revisarlos con el comportamiento real.
- El disyuntor es estado en memoria: con varias instancias de la API, cada una tiene
  el suyo y abren por separado. Para el alcance del TFI hay una sola instancia; con
  más de una habría que compartir el estado, y eso está fuera de alcance.
- Los pools separados dividen un recurso finito: mal dimensionados, el proceso en
  segundo plano se queda corto y tarda más. Es un intercambio deliberado —preferimos
  un job lento a usuarios esperando— pero hay que dimensionarlo con las pruebas de
  carga del ADR 0008.

## Precisión de la implementación (02/09/2026)

**El disyuntor es por organización, no uno solo para todo el sistema.** Esta
decisión no estaba en la versión original del ADR y apareció al implementarlo.

El motivo es de aislamiento y no de rendimiento. Los modos de falla del proveedor
son en buena medida **por cliente**: la clave de API venció, la cuenta se quedó sin
crédito, la organización configuró un modelo que ya no existe. Con un disyuntor
compartido, cualquiera de esas situaciones en un cliente abriría el circuito para
todos los demás, y organizaciones cuyo proveedor responde con normalidad pasarían a
asignación de respaldo sin ninguna razón. Sería el mismo error de diseño que el ADR
0005 evita en el acceso a datos, trasladado a la resiliencia.

La implementación es un registro de disyuntores indexado por organización, con el
mismo umbral y la misma espera para todos. Todo lo demás que dice este ADR —los tres
estados, los cinco fallos, los sesenta segundos, la única llamada de prueba en
semiabierto— vale igual para cada disyuntor del registro.

La consecuencia negativa ya anotada se mantiene y se amplía: el estado sigue siendo
de esta instancia de la API, y ahora hay un disyuntor por cada organización activa
en lugar de uno. Son unas pocas decenas de bytes cada uno, así que el costo es
irrelevante; lo que hay que tener presente es que el diagnóstico de «el circuito
está abierto» pasa a ser una pregunta por organización y no del sistema.
