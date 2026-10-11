# ADR 0001 — Registrar las decisiones de arquitectura

**Estado:** Aprobado
**Fecha:** 2026-09-02

## Contexto

El desarrollo de PredictIT se apoya de manera intensiva en asistencia por IA. Eso
acelera la escritura de código, pero traslada el peso de la ingeniería desde
*escribir* hacia *decidir y validar*: la pregunta relevante en la defensa no va a
ser cómo se escribió una clase, sino por qué el sistema está construido así y qué
se descartó en el camino.

El documento del TFI describe el sistema en su estado final, no el razonamiento que
llevó hasta ahí. Sin un registro explícito, las decisiones quedan implícitas en el
código y se pierden: dentro de dos meses nadie —ni el propio autor— puede
reconstruir por qué el acceso a datos es ADO.NET y no Entity Framework, ni qué
alternativas se evaluaron.

## Decisión

Se registran las decisiones de arquitectura significativas como **Architecture
Decision Records** en formato Nygard, en `docs/adr/`, numerados secuencialmente y
versionados junto al código.

Una decisión es significativa —y por lo tanto lleva ADR— cuando cumple al menos una
de estas condiciones:

- Es costosa de revertir una vez escrito el código que depende de ella.
- Contradice lo que dice el documento del TFI.
- Se eligió entre alternativas reales, no por falta de opciones.
- Determina cómo se cumple un requerimiento no funcional.

Cada ADR lleva Título, Estado, Contexto, Decisión y Consecuencias, separando en
estas últimas lo positivo de lo negativo y los riesgos asumidos. Un ADR aprobado no
se edita: si la decisión cambia, se escribe uno nuevo que lo reemplaza y el anterior
pasa a estado *Reemplazado*.

Los ADR se enlazan desde `docs/CAMBIOS-DOCUMENTO.md` cuando la decisión obliga a
modificar el documento del TFI, y desde `docs/trazabilidad/PROMPTS-CRITICOS.md`
cuando la decisión se tomó consultando a una herramienta de IA.

## Consecuencias

**Positivas**

- Las decisiones quedan defendibles con su contexto y sus alternativas, que es
  exactamente lo que se evalúa en la defensa oral.
- El capítulo de arquitectura del documento se redacta a partir de los ADR en vez
  de reconstruirse de memoria al final.
- Un ADR obliga a escribir las consecuencias negativas, lo que fuerza a mirar el
  costo de cada decisión antes de tomarla y no después.

**Negativas y riesgos**

- Cuesta disciplina: un ADR escrito una semana tarde ya perdió el contexto que lo
  hacía valioso.
- Hay riesgo de sobredocumentar. El criterio de significatividad de arriba existe
  para eso: si una decisión se revierte cambiando dos archivos, no lleva ADR.
- El formato puede degradarse en trámite si se escribe para cumplir. Un ADR sin
  alternativas descartadas no aporta nada.
