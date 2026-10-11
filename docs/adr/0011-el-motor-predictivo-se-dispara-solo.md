# ADR 0011 — El motor predictivo se dispara solo

**Estado:** Aprobado
**Fecha:** 2026-09-02
**Origen:** prueba de aceptación sobre el sistema corriendo

## Contexto

El motor predictivo estaba completo: las cinco estrategias del ADR 0007, el
scoring ponderado, la generación de alertas y la pantalla de análisis. Pasaba sus
pruebas y, consultado por la API, devolvía datos correctos —47 equipos evaluados,
5 reglas activas, ranking con scores y niveles—.

Y sin embargo **sólo corría cuando alguien apretaba «Evaluar»**. Una revisión de
las llamadas lo confirmó: nada invocaba `EvaluarParque` ni `EvaluarEquipo` fuera
del controlador. Ni el alta de una incidencia, ni el registro de un
mantenimiento, ni ningún proceso programado.

Eso no cumple RF-11. Un sistema que calcula el riesgo cuando se lo consulta es un
informe, no un sistema predictivo: la alerta llega cuando alguien ya fue a
buscarla, que es justo cuando ya no hace falta que le avisen.

El defecto era invisible desde el código y desde las pruebas. Cada pieza estaba
bien y la que faltaba no existía, así que no había nada que leer mal ni ninguna
prueba que fallara. Se detectó recorriendo el sistema corriendo.

## Decisión

El motor pasa a tener **dos disparadores**, porque las condiciones que evalúa
aparecen de dos maneras distintas.

### Por hecho — `ReevaluarPorEvento(idEquipo)`

Se dispara al registrar una incidencia y al registrar un mantenimiento. Son los
dos momentos en que cambian los datos que las reglas miran: una incidencia nueva
puede completar una recurrencia o cruzar un umbral de acumulación, y un
mantenimiento puede resolver una alerta de mantenimiento vencido.

Tres propiedades deliberadas:

- **No exige patente.** Es una consecuencia del alta, no una consulta que alguien
  pidió. Exigir `ALERTA_VER` haría que un solicitante —que puede reportar pero no
  ver alertas— no dispare la reevaluación de su propio equipo.
- **No propaga errores.** Que el análisis falle no puede hacer fallar el registro
  de la falla que lo disparó. El error se asienta en bitácora y la operación
  sigue.
- **Sólo asienta si generó algo.** Una línea de bitácora por cada incidencia
  diciendo «no pasó nada» haría ilegible la pantalla de auditoría.

### Por tiempo — `BarridoPredictivo`

Un `BackgroundService` que recorre las organizaciones activas cada 24 horas,
configurable, y con 0 lo desactiva.

Hace falta además del disparador por hecho porque **hay reglas que se cumplen sin
que pase nada**: un mantenimiento vence, una garantía se acerca a su fin, un
equipo cumple años. Esas condiciones aparecen por el paso del tiempo, y sin
barrido la alerta esperaría a la próxima incidencia de ese equipo —que puede ser
justamente la falla que se quería prevenir—.

Dos decisiones sobre cómo corre:

- **Contexto de sistema acotado.** El barrido no tiene sesión HTTP de la cual
  sacar el usuario y la organización, así que se le construye un contexto por
  organización con **sólo la patente que necesita**. No es un contexto de
  superusuario: si mañana el barrido intentara hacer otra cosa le faltaría el
  permiso y fallaría, que es el comportamiento buscado. El usuario queda vacío:
  la bitácora tiene que decir que lo hizo el sistema y no atribuírselo a una
  persona que no estaba.
- **Una organización que falla no frena a las demás.** Sin reglas configuradas,
  por ejemplo, `EvaluarParque` lanza a propósito. Ese caso se asienta y el
  barrido sigue con la siguiente.

## Consecuencias

**A favor**

- RF-11 se cumple de verdad: la alerta aparece cuando la condición se cumple.
- La demostración deja de depender de que alguien apriete un botón en el momento
  justo.
- El contexto de sistema, acotado por patente, sirve para cualquier otro proceso
  en segundo plano que venga después.

**En contra**

- El alta de una incidencia hace más trabajo. Se acotó reevaluando **un equipo** y
  no el parque, y sin propagar errores, pero el costo existe.
- El barrido compite por conexiones con la operación interactiva. Por eso espera
  antes del primer ciclo —al arrancar la API está aplicando scripts y atendiendo
  el primer login— y por eso el bulkhead del ADR 0009 sigue siendo necesario.
- El barrido corre dentro del proceso de la API. Con varias instancias, cada una
  haría su propio barrido. Hoy hay una sola; el día que haya más va a hacer falta
  un candado, y conviene decirlo ahora y no descubrirlo con alertas duplicadas.

## Alternativas descartadas

**Sólo el barrido programado.** Más simple, y demora la alerta hasta 24 horas
desde el hecho que la habilita. La tercera incidencia del mismo equipo en una
semana es exactamente el momento en que hay que avisar.

**Sólo el disparador por hecho.** No cubre las reglas que dependen del calendario,
que son tres de las cinco.

**Un programador externo —el Programador de tareas de Windows llamando a un
endpoint—.** Menos código propio, y suma una pieza de infraestructura por fuera
del sistema, que hay que instalar y documentar aparte, y que falla en silencio si
alguien la desactiva. Un `BackgroundService` viaja con la aplicación.
