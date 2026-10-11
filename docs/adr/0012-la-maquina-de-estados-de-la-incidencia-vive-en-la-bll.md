# ADR 0012 — La máquina de estados de la incidencia vive en la BLL

**Estado:** Aprobado
**Fecha:** 2026-09-03
**Origen:** auditoría de patentes declaradas sin uso

## Contexto

La incidencia se podía registrar y reasignar, y nada más. El catálogo
`EstadoIncidencia` tiene **siete estados** y sólo dos eran alcanzables:
*Pendiente de asignación* y *Asignada*. Las columnas `diagnostico`, `solucion` y
`fecha_resolucion` existían en la base, el detalle de la incidencia las
devolvía, y **ninguna consulta del sistema las escribía**.

La patente `INCIDENCIA_ATENDER` estaba en la base y ningún endpoint la exigía.
Apareció buscando en el código las patentes declaradas que nadie usa, con la misma
técnica que ya había encontrado la bitácora sin eventos de negocio y el motor
predictivo que no se disparaba solo.

El agujero no era menor. Sin fecha de resolución no hay tiempo de reparación, no
hay cumplimiento del SLA de cuatro horas que el documento promete, y el tablero
no puede calcular el tiempo medio. El caso de uso central del técnico —tomar una
incidencia, diagnosticarla y resolverla— no existía.

Y era invisible: cada pieza estaba bien escrita, nada fallaba, ninguna prueba se
ponía en rojo. Faltaba código, y lo que no existe no puede fallar.

## Decisión

Se implementa el ciclo de vida completo, con la máquina de estados **en la capa
de negocio**.

### Las transiciones

```
Pendiente de asignación  ->  Asignada (lo hace la asignación) | Anulada
Asignada                 ->  En curso | Anulada
En curso                 ->  En espera de repuesto | Resuelta | Anulada
En espera de repuesto    ->  En curso | Anulada
Resuelta                 ->  Cerrada | En curso   (reapertura)
Cerrada, Anulada         ->  nada. Son finales.
```

### En un solo lugar

La máquina vive en `IncidenciaBusiness` y en ningún otro lado. La pantalla
**pregunta** qué transiciones hay —`GET /incidencias/{id}/transiciones`— y ofrece
las que el negocio devuelve. Si la máquina viviera también en el frontend habría
dos, y se contradirían la primera vez que cambie una regla.

### Tres reglas que van con la decisión

**Resolver exige diagnóstico y solución.** Una incidencia cerrada sin decir qué
se encontró y qué se hizo no le sirve al próximo técnico que vea el mismo
equipo, ni al motor predictivo, que se alimenta del historial.

**La fecha de resolución la escribe el paso a *Resuelta* y la limpia la
reapertura.** Es el dato del que sale el tiempo de reparación: una incidencia
reabierta que conservara la fecha vieja produciría un tiempo de reparación
falso, y falso hacia abajo, que es el peor sentido.

**Sólo la atiende el técnico asignado**, o quien pueda reasignar cualquier
incidencia. Es la misma regla que ya regía la reasignación; tenerla distinta acá
sería una puerta lateral. *Anular* es aparte: cancela el trabajo de otro, así que
exige la patente de reasignar cualquiera.

### Dónde termina la validación y dónde empieza la guía

Esta parte se decidió **después de un error propio**, y conviene que quede
escrita porque es el tipo de cosa que se repite.

El primer diseño hacía que el negocio reportara «falta cargar el diagnóstico»
como impedimento de la transición a *Resuelta*. El negocio mira lo **guardado**,
así que el botón quedaba deshabilitado para siempre: para guardar el diagnóstico
hacía falta una transición, y la transición pedía el diagnóstico guardado. Un
interbloqueo, y no se detectó leyendo el código sino usando la pantalla.

El reparto correcto es otro:

- **El negocio valida.** `Atender` rechaza resolver sin los dos textos, y es la
  única garantía: vale contra la API, contra Swagger y contra cualquier cliente
  futuro.
- **El negocio informa sólo los impedimentos que el formulario no puede conocer**
  —si hay técnico asignado, y quién es—.
- **El formulario guía.** Deshabilita *Resuelta* mientras falte texto, evaluando
  lo **escrito**, porque los dos viajan en la misma llamada.

## Consecuencias

**A favor**

- El caso de uso central del sistema existe, y con él el tiempo de reparación y
  el SLA medible.
- Siete estados alcanzables en lugar de dos.
- Resolver una incidencia reevalúa el riesgo del equipo, porque cambió su
  historial. No propaga errores, igual que el alta.
- El avance queda auditado: cuatro códigos de evento nuevos, con el tiempo de
  resolución escrito en la línea de bitácora.

**En contra**

- La tabla de transiciones es un diccionario en código. Un catálogo en base
  sería configurable, y también permitiría dejar el sistema en un estado sin
  salida por configuración. Se eligió el diccionario: las transiciones de una
  incidencia no son algo que cada organización deba poder cambiar.
- La reapertura de una incidencia resuelta pierde la fecha de resolución
  anterior. Es deliberado —la nueva resolución tiene que medirse desde el alta—
  pero significa que el sistema no guarda cuántas veces se reabrió. Si eso
  importa, hace falta una tabla de historial de estados.
- El técnico asignado es el único que puede atender, así que una licencia
  obliga a reasignar. Es correcto y es fricción real.

## Alternativas descartadas

**Un endpoint por transición** (`/resolver`, `/cerrar`, `/anular`). Se lee mejor
en la URL y esparce la máquina de estados por seis controladores. La primera
regla nueva habría que agregarla en seis lugares.

**Estado libre, validado sólo por el catálogo.** Es lo que había de hecho, y
permite pasar de *Pendiente* a *Cerrada* sin que nadie la atienda: el registro
queda consistente y la realidad que describe, no.

**Un motor de flujo de trabajo configurable.** Resuelve un problema que este
sistema no tiene. Siete estados y ocho transiciones no justifican un motor, y
sumarlo haría que la regla más simple del sistema fuera la más difícil de leer.
