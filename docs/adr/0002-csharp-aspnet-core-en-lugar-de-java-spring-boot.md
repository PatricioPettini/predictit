# ADR 0002 — C# / ASP.NET Core en lugar de Java / Spring Boot

**Estado:** Aprobado
**Fecha:** 2026-09-02
**Contradice:** documento del TFI, sección 10.2.5 (ver `docs/CAMBIOS-DOCUMENTO.md`, D-01)

## Contexto

La sección 10.2.5 del documento fija el entorno del sistema en Spring Boot / Java /
MySQL / IIS 10. Esa definición se escribió antes de empezar a construir y no estaba
atada a ninguna restricción real del proyecto: no hay infraestructura preexistente,
ni integraciones que impongan una plataforma, ni cliente que la exija.

Al mismo tiempo hay dos condicionantes concretos que sí pesan:

1. **La cátedra evalúa la separación en capas** (DOMAIN / DAO / BLL / SERVICE) y los
   patrones que la sostienen. Existe un proyecto de referencia propio —el Trabajo de
   Diploma de 3.º año— que implementa exactamente ese modelo en C# con ADO.NET, con
   `IGenericDao<T>`, `IObjectMapper<T>`, Abstract Factory, Unit of Work y el esquema
   de permisos Usuario / Familia / Patente ya resuelto.
2. **C# es el lenguaje visto a lo largo de la carrera.** El TFI se defiende oralmente
   y hay que poder explicar cualquier línea del sistema bajo pregunta.

El costo de elegir Java sería construir desde cero un andamiaje que ya está resuelto
y probado, en un lenguaje con el que el autor tiene menos recorrido, sin ninguna
ventaja compensatoria para este dominio: no hay volumen de datos, concurrencia ni
requisitos de despliegue que diferencien seriamente a una plataforma de la otra en
la escala de este sistema (500 activos y 5000 incidencias, según RNF-04).

## Decisión

El sistema se desarrolla en **C# sobre .NET 9**, con **ASP.NET Core Web API** como
backend y **React 19 + TypeScript** como frontend, contra **SQL Server 2022**.

La solución replica la estructura del proyecto de referencia llevada a web:
`PredictIT.Domain`, `PredictIT.DAO`, `PredictIT.BLL`, `PredictIT.Service`,
`PredictIT.Api` y `PredictIT.Tests`, con las dependencias cableadas de modo que sólo
puedan ir hacia abajo.

Se actualiza la sección 10.2.5 del documento y los diagramas de componentes y
despliegue en consecuencia.

## Alternativas consideradas

| Opción | Por qué se descartó |
|---|---|
| Java / Spring Boot / MySQL, como dice el documento | Reescribir desde cero un andamiaje ya resuelto, en el lenguaje con menos recorrido del autor, sin ventaja técnica para este dominio. |
| ASP.NET Core MVC con vistas Razor | Una sola solución y menos piezas móviles, pero el módulo del cliente y el panel de administración tienen necesidades de interacción distintas, y el frontend separado permite tratarlos como dos aplicaciones sobre la misma API. |
| Node.js / TypeScript en todo el stack | Sin instalación previa y con el mayor recorrido práctico del autor, pero se aleja del lenguaje de la carrera y del modelo de capas que evalúa la cátedra. |

## Consecuencias

**Positivas**

- Reutiliza un modelo de capas ya validado académicamente, con sus patrones
  identificables uno por uno en el código.
- El autor puede defender cualquier parte del sistema en el lenguaje que conoce.
- SQL Server habilita `BACKUP` / `RESTORE DATABASE` nativo, con lo cual el
  Req. Arq. 003 se cumple sin construir un mecanismo propio.

**Negativas y riesgos**

- Obliga a modificar el documento en varios puntos: entorno, diagramas de
  componentes y de despliegue, y manual de instalación.
- Ata el sistema a un motor de base de datos comercial. Para el alcance del TFI se
  cubre con la edición Developer en contenedor, pero un despliegue productivo real
  tendría un costo de licencia que Java + MySQL no tendría.
- El frontend separado agrega una pieza más al despliegue y al manual de
  instalación que una solución monolítica no tendría.
