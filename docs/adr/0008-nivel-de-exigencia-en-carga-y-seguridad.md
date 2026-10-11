# ADR 0008 — Nivel de exigencia en carga y seguridad

**Estado:** Aprobado
**Fecha:** 2026-09-02
**Amplía:** documento del TFI, sección 10.2.2 — RNF (ver `docs/CAMBIOS-DOCUMENTO.md`, D-08)

## Contexto

El desarrollo se apoya en asistencia por IA, y eso cambia el perfil de riesgo del
proyecto, no sólo su velocidad. El código asistido tiende a ser sintácticamente
correcto y plausible, lo que desactiva la señal de alarma más útil que tiene un
desarrollador: que algo *se vea mal*. Los defectos que sobreviven a esa revisión son
justamente los que no se ven —una consulta sin filtro de organización, un
`SqlCommand` armado por concatenación en un método que quedó fuera del patrón, un
`SqlDataReader` sin `using`, una dependencia con vulnerabilidad conocida que el
asistente sugirió por costumbre—.

La consigna del trabajo lo plantea directamente: dado el incremento de velocidad de
codificación, el nivel de exigencia en pruebas de estrés, perfilado de memoria y
análisis estático debe ser **superior al estándar tradicional**.

Los RNF actuales del documento no alcanzan para eso. RNF-04 fija un objetivo de
rendimiento (menos de 2 segundos con 500 activos y 5000 incidencias) pero no dice
cómo se verifica, y RNF-02 habla de seguridad en términos de control de acceso, sin
mencionar análisis de código ni dependencias.

## Decisión

Se incorporan al proyecto tres controles obligatorios, ejecutables por comando y
verificados antes de cada entrega formal. Ninguno es opcional ni queda "para si hay
tiempo".

### 1. Análisis estático (SAST)

- **Analizadores de .NET en modo estricto**: `AnalysisMode=All` y
  `TreatWarningsAsErrors=true` en `Directory.Build.props`. Una advertencia rompe la
  compilación; no hay advertencias toleradas.
- **`dotnet list package --vulnerable --include-transitive`** en cada entrega. Una
  dependencia con vulnerabilidad conocida bloquea la entrega.
- **Reglas dirigidas a los riesgos propios de este sistema**, más allá del conjunto
  genérico:
  - CA2100 — SQL construido por concatenación. Es el riesgo directo de haber
    elegido ADO.NET (ADR 0004).
  - CA2000 / CA2213 — `SqlConnection` y `SqlDataReader` sin liberar.
  - Ausencia de filtro por organización en un DAO. No hay analizador que lo detecte:
    se cubre con revisión dirigida y con pruebas de aislamiento (ADR 0005).
- Secretos fuera del repositorio: la clave de API se guarda cifrada en base y las
  cadenas de conexión reales van por configuración de entorno.

### 2. Pruebas de carga

Se define un perfil de carga que refleja el escenario de RNF-04 y se verifica
contra él, no contra una corrida a ojo:

| Escenario | Volumen | Objetivo |
|---|---|---|
| Consulta de inventario con filtros | 500 activos | p95 < 2 s |
| Historial técnico de un equipo | 5000 incidencias en base | p95 < 2 s |
| Dashboard | agregados sobre el total | p95 < 2 s |
| Evaluación del motor predictivo | parque completo | tiempo total acotado y medido |
| Registro de incidencia con IA simulada | 20 usuarios concurrentes | p95 < 2 s |

Se mide con **NBomber** (C#, integrable a la solución de pruebas) sobre una base
cargada con volumen sintético generado por un script de `db/`. Se registra el
resultado de cada corrida para poder mostrar la evolución, no sólo el último número.

El escenario que más interesa no es el más rápido sino el más frágil: el motor
predictivo sobre el parque completo, porque su costo crece con el historial y el
historial sólo crece.

### 3. Perfilado de memoria

Con las herramientas del propio SDK, sin dependencias adicionales:
`dotnet-counters` para observar el comportamiento bajo carga sostenida,
`dotnet-gcdump` para comparar el montón antes y después de una corrida larga, y
`dotnet-trace` cuando haga falta ubicar un punto caliente.

Se busca específicamente lo que este sistema puede tener: conexiones o lectores no
liberados durante la evaluación del parque completo, y acumulación en el caché de
permisos del `SeguridadService`.

## Alternativas consideradas

| Opción | Por qué se descartó |
|---|---|
| SonarQube o CodeQL | Cobertura superior, pero agregan infraestructura y tiempo de configuración desproporcionados para el alcance del TFI. Los analizadores del SDK cubren los riesgos identificados. |
| JMeter o k6 para la carga | Herramientas más difundidas, pero quedan fuera de la solución. NBomber vive en el proyecto de pruebas y se corre con `dotnet test`. |
| Dejar la carga y el perfilado para el cierre | Es cuando ya no hay margen para actuar sobre lo que se encuentre. Se corren desde que existe la API. |

## Consecuencias

**Positivas**

- Convierte "el nivel de exigencia debe ser superior" en comandos concretos que se
  corren y devuelven un número.
- `TreatWarningsAsErrors` obliga a resolver los avisos en el momento, con el contexto
  todavía fresco.
- Los resultados de carga alimentan directamente la verificación de RNF-04, que hoy
  es una afirmación sin evidencia.

**Negativas y riesgos**

- `TreatWarningsAsErrors` es incómodo: rompe compilaciones por cosas menores y
  tienta a suprimir reglas. Toda supresión va con comentario que la justifique, y
  las supresiones se revisan en cada entrega.
- Generar volumen sintético realista lleva trabajo y el resultado nunca es igual a
  datos reales: 5000 incidencias distribuidas uniformemente no estresan lo mismo que
  5000 concentradas en pocos equipos. El generador produce distribución sesgada a
  propósito.
- Ninguno de estos controles detecta el defecto más peligroso del sistema —una
  consulta sin filtro de organización—. Eso depende de las pruebas de aislamiento.
  Conviene no confundir la existencia del control con la ausencia del riesgo.
