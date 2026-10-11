# ADR 0013 — Reportes en PDF generados en el servidor

**Estado:** Aprobado
**Fecha:** 2026-09-03
**Origen:** RF-14, y la patente `REPORTE_GENERAR` declarada sin uso

## Contexto

RF-14 pide reportes **filtrados y exportables**. La patente `REPORTE_GENERAR`
estaba en la base, concedida a la familia `OPERACION_TECNICA`, y ningún endpoint
la exigía: el requisito estaba en el documento y no en el sistema.

El formato lo eligió el autor: **PDF**. Es la decisión que condiciona todo lo
demás, porque un PDF hay que componerlo y no sólo serializarlo.

## Decisión

Se genera el PDF **en el servidor**, con **QuestPDF** bajo licencia Community.

### Por qué en el servidor y no en el navegador

Tres razones, en orden de peso:

1. **El reporte tiene que poder explicarse solo.** Lleva impresa la fecha de
   emisión, quién lo generó y los filtros aplicados. Eso sólo es creíble si lo
   escribe el sistema: un PDF armado en el navegador lo firma el navegador.
2. **La bitácora tiene que saber que salió.** Un reporte impreso circula fuera
   del sistema, y saber que salió de acá, cuándo y con qué recorte es la condición
   para poder explicarlo después. Generándolo en el cliente, el servidor no se
   enteraría.
3. **Los datos ya están del lado del servidor.** Mandarlos al navegador para que
   los componga es mover el parque completo para dibujar una tabla.

### Por qué QuestPDF

- **No necesita binarios nativos ni un navegador headless**, así que funciona
  igual dentro del contenedor de la API. Es la razón que descartó a
  `wkhtmltopdf` y a las soluciones basadas en Chromium: habría que meter un
  navegador en la imagen y la imagen pasaría de 350 MB a más de un giga.
- Licencia **Community**, gratuita por debajo del millón de dólares de
  facturación anual. Para este proyecto es limpio, y queda dicho acá para que no
  haya que averiguarlo el día que se use en producción.
- Se descartó **iText** por su licencia AGPL, que obliga a liberar el sistema
  entero.

### El generador no sabe qué reporta

`IReporteService.Listado<T>` recibe encabezado, filas y definición de columnas.
Por eso los tres reportes —parque, incidencias y mantenimientos— son **tres
llamadas y no tres clases**: lo que cambia entre ellos son los datos y los
títulos, no el documento.

### Los reportes leen los datos que ya tiene el sistema

El del parque sale del mismo listado que la pantalla de activos; el de
incidencias, del mismo buscador. Si el reporte trajera sus propias consultas, la
pantalla y el papel podrían decir cosas distintas sobre el mismo día, y no habría
forma de saber cuál miente.

### Detalles de composición que son decisiones

- **Los filtros van en el encabezado y no al pie.** Un reporte filtrado que no
  dice con qué filtros se hizo se puede malinterpretar, y quien lo lea impreso no
  tiene forma de averiguarlo.
- **La cabecera de la tabla se repite en cada página.** El parque son tres hojas,
  y las hojas dos y tres sin encabezado son columnas de datos sin nombre.
- **Apaisado automático con más de cinco columnas.** Siete columnas en A4
  vertical salen ilegibles.
- **Un reporte vacío dice que está vacío.** Una hoja con el encabezado y nada
  debajo se lee como un error de generación.
- **Los números se alinean a la derecha.** No es cosmético: una columna de
  importes alineada a la izquierda obliga a leer cada fila entera para comparar
  dos valores.

### El PDF no se guarda

Se genera y se entrega. Un reporte guardado envejece, y alguien termina abriendo
el de la semana pasada creyendo que es el de hoy. Los filtros viajan por *query
string* justamente para que el enlace sea reproducible: el mismo enlace da el
mismo recorte, con los datos del momento en que se pide.

### `IReporteBusiness` va aparte

No se suma a `IAdministracionBusiness`. Esa interfaz ya mezcla cinco temas y es
la violación de segregación de interfaces que el proyecto tiene anotada como
deuda propia. No se le agrega un sexto.

## Consecuencias

**A favor**

- RF-14 queda cumplido con un entregable que se puede imprimir y mostrar.
- El reporte del parque cruza el riesgo de la última evaluación. Ese cruce es
  el que lo vuelve útil para decidir y no sólo para inventariar. El de incidencias trae el
  tiempo medio de resolución, que recién ahora se puede calcular porque la fecha
  de resolución existe (ADR 0012).
- Cada emisión queda en la bitácora con quién, cuándo y cuántas filas.

**En contra**

- Una dependencia más en el backend, con una licencia que hay que leer. Está
  documentada acá y no descubierta después.
- Generar el PDF ocupa el hilo de la petición. Con 52 equipos son milisegundos;
  con un parque de miles habría que pasarlo a un trabajo en segundo plano y
  entregar el archivo cuando esté. No se hace ahora porque sería resolver un
  problema que el sistema no tiene.
- Los tres reportes traen la tabla completa a memoria para filtrar. Es
  consistente con cómo trabaja el resto del sistema y es lo que el requisito de
  carga del ADR 0008 tiene que medir.

## Alternativas descartadas

**CSV.** Sin dependencias y en cuarenta líneas. Lo descartó el autor, y con
razón para lo que se va a mostrar: un CSV no lleva encabezado, no lleva los
filtros impresos y no se ve como un entregable.

**Excel con formato.** Más presentable que el CSV y menos que el PDF, y suma una
dependencia igual. El PDF además se imprime y se firma, que es el recorrido
habitual de un informe de servicios.

**HTML imprimible desde el navegador.** Sin dependencias, y el resultado depende
del navegador y de la configuración de impresión de cada máquina. Un entregable
que se ve distinto en cada computadora no es un entregable.
