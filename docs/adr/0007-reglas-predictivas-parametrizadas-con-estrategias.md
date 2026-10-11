# ADR 0007 — Reglas predictivas parametrizadas, resueltas con estrategias

**Estado:** Aprobado
**Fecha:** 2026-09-02

## Contexto

El motor predictivo es el diferencial del producto (1.7, 10.2.1) y RF-11 lo define
como reglas **configurables** sobre el historial. El documento nombra cinco
condiciones: recurrencia de fallas, mantenimiento vencido, acumulación de
incidencias, antigüedad del equipo y garantía por vencer.

"Configurable" admite grados muy distintos, y hay que elegir uno:

- Que el administrador ajuste **parámetros** de reglas predefinidas (cuántas fallas,
  en cuántos días, cuánto pesa).
- Que pueda **escribir condiciones nuevas** en algún lenguaje de expresiones.
- Que las reglas estén fijas en el código y sólo se activen o desactiven.

El usuario objetivo es un responsable administrativo no técnico. Un editor de
expresiones es una funcionalidad que nadie de ese perfil va a usar y que introduce
un evaluador de expresiones —es decir, ejecución de entrada de usuario— en el
sistema. Reglas fijas en código, en el otro extremo, contradicen RF-11 y dejan al
producto sin el argumento de que cada cliente calibra su motor.

Hay además una tensión de modelado: cada tipo de regla necesita parámetros
distintos. "Recurrencia" necesita cantidad y ventana temporal; "antigüedad" necesita
años; "garantía" necesita días de aviso. Modelar eso con columnas produce una tabla
mayormente nula, y con una tabla de parámetros clave-valor produce consultas
incómodas para algo que siempre se lee entero.

## Decisión

Las reglas se guardan en `ReglaAlerta` con un campo `tipo` que discrimina la
estrategia y un campo `condicion` con **los parámetros en JSON**:

```
tipo = 'RECURRENCIA_FALLAS'
condicion = {"minIncidencias": 3, "ventanaDias": 30}
peso = 30
```

Cada tipo se resuelve con una **estrategia** (patrón Strategy) que sabe leer sus
propios parámetros y devuelve un aporte al score más el motivo textual que lo
explica. El `EvaluadorReglas` recorre las reglas activas de la organización,
selecciona la estrategia por `tipo` y compone el resultado.

El score final es la **suma ponderada de los aportes**, acotada a 0–100, y se
traduce a nivel según umbrales: bajo 0–39, medio 40–69, alto 70–100. Cada evaluación
se persiste completa en `EvaluacionRiesgo`, con el detalle por regla en JSON.

El `tipo` está restringido por `CHECK` en la base al conjunto de estrategias
implementadas: no se puede guardar una regla que el sistema no sepa evaluar.

Agregar un tipo de regla nuevo es escribir una estrategia y agregar su valor al
`CHECK`. **No** es una operación que pueda hacer el usuario final.

## Alternativas consideradas

| Opción | Por qué se descartó |
|---|---|
| Lenguaje de expresiones editable por el usuario | Nadie del perfil objetivo lo usaría, y mete evaluación de entrada de usuario en el sistema: superficie de ataque sin beneficio. |
| Una columna por parámetro en `ReglaAlerta` | Tabla mayormente nula, y cada tipo de regla nuevo es una migración de esquema. |
| Tabla `ParametroRegla` clave-valor | Normaliza bien pero se lee siempre entera de todos modos, y pierde el tipado que igual hay que reconstruir al parsear. |
| Reglas fijas en código, sólo activables | Contradice RF-11 y elimina el argumento comercial de calibración por cliente. |

## Consecuencias

**Positivas**

- El motor es determinístico y se prueba sin base de datos ni red: cada estrategia
  es una función de (historial, parámetros) a (puntos, motivo).
- La pantalla de Reglas predictivas puede ofrecer un preview real —"con estos
  valores se dispararía en 4 equipos"— porque evaluar es barato.
- Persistir `EvaluacionRiesgo` con el detalle permite responder la pregunta que va a
  aparecer en la defensa: por qué este equipo estaba en rojo hace dos semanas y hoy
  no.
- El motor explica sus decisiones. Cada aporte al score viene con el dato concreto
  que lo generó, y es el desglose que muestra la pantalla de detalle
  de equipo.

**Negativas y riesgos**

- El JSON de `condicion` no está validado por el motor de base: una regla con
  parámetros mal formados sólo falla al evaluarse. Se mitiga validando el JSON
  contra el esquema de cada tipo en la BLL al guardar, no al usar.
- La suma ponderada acotada a 100 hace que, con muchas reglas activas y pesos altos,
  todo tienda a saturar en riesgo alto y el score pierda capacidad de discriminar.
  La pantalla de reglas muestra la suma de pesos para que sea visible.
- El score es una construcción, no una probabilidad de falla. Presentarlo como
  "86 % de probabilidad de romperse" sería falso; se presenta como score de riesgo y
  así hay que sostenerlo en la defensa.
- Cambiar una regla cambia el score histórico si se reevalúa. Por eso
  `EvaluacionRiesgo` guarda la foto: las evaluaciones pasadas no se recalculan.
