# ADR 0005 — Multi-organización desde el modelo de datos

**Estado:** Aprobado
**Fecha:** 2026-09-02
**Amplía:** documento del TFI, sección 10.4.7 (ver `docs/CAMBIOS-DOCUMENTO.md`, D-02)

## Contexto

El plan de negocio del TFI define tres segmentos, y el **Segmento C** —proveedores
tercerizados de soporte informático— es explícitamente el canal estratégico: cada
proveedor introduce el sistema en varias PyMEs a la vez. La sección 5.3.6 le pone
precio a eso: *Plan Partner, ARS 27.900 por cliente gestionado*, con acceso a un
"portal multi-cliente".

El DER del documento, sin embargo, no tiene entidad `Organizacion`. Las entidades
cuelgan unas de otras y el modelo asume implícitamente una sola empresa por
instalación. Con ese modelo, el plan Partner sólo se puede implementar desplegando
una instancia y una base por cliente, que es exactamente lo que un proveedor con
cinco clientes chicos no va a operar.

La decisión hay que tomarla ahora: agregar aislamiento por organización una vez que
existen los DAO, la BLL y la API significa tocar cada consulta del sistema.

## Decisión

Se incorpora `Organizacion` como **raíz del modelo de negocio**. Toda tabla de
negocio lleva `id_organizacion`: `Ubicacion`, `Equipo`, `Incidencia`,
`Mantenimiento`, `ReglaAlerta`, `AlertaPredictiva`, `EvaluacionRiesgo`,
`TecnicoEspecialidad` y `ConfiguracionAsignacion`.

El aislamiento se aplica **en la capa DAO**, tomando la organización del contexto de
sesión, no del pedido HTTP. Un controlador no puede pedir datos de otra
organización porque no tiene forma de nombrarla: el identificador no viaja en la
petición, se resuelve del token.

Los usuarios llevan `id_organizacion` en la base de servicio como referencia lógica
(ADR 0003). El perfil **Partner** es la excepción: tiene `id_organizacion` nulo y
sus organizaciones habilitadas viven en `Usuario_Organizacion`. Cambiar de
organización activa requiere la patente `ORGANIZACION_CAMBIAR`, que sólo tiene ese
rol, y queda asentado en bitácora.

Los catálogos verdaderamente globales —tipos de equipo, estados, prioridades,
categorías, especialidades— se comparten entre organizaciones. Las reglas
predictivas y la configuración de asignación **no**: cada cliente calibra su propio
motor.

## Alternativas consideradas

| Opción | Por qué se descartó |
|---|---|
| Una instalación y una base por cliente | Aislamiento perfecto y modelo más simple, pero convierte al plan Partner en algo inoperable: cinco clientes son cinco despliegues, cinco respaldos y cinco actualizaciones. |
| Un esquema de SQL Server por organización | Aislamiento fuerte sin multiplicar instancias, pero el alta de un cliente pasa a ser una operación DDL y el Partner no puede consultar de forma consolidada. |
| Dejarlo como evolución futura | Es la opción que más se paga: agregar `id_organizacion` con el sistema construido obliga a revisar cada consulta, cada test y cada endpoint. |

## Consecuencias

**Positivas**

- El plan Partner del capítulo 5 pasa a ser implementable, y el sistema puede
  demostrar en la defensa el escenario que sostiene el canal de venta.
- El aislamiento en el DAO hace que olvidarse del filtro sea difícil: no hay una
  consulta "sin organización" que un desarrollador pueda escribir por descuido.
- Permite demostrar el aislamiento con datos: el seed carga dos organizaciones
  justamente para poder probar que una no ve a la otra.

**Negativas y riesgos**

- **Es el riesgo de seguridad más serio del sistema.** Una consulta que se olvide
  el filtro filtra datos entre clientes. Se mitiga con el filtro obligatorio en el
  DAO, pruebas de integración específicas de aislamiento por cada entidad, y
  revisión dirigida a eso en el análisis estático.
- Todas las claves únicas pasan a ser compuestas con la organización: el código
  `PC-ADM-014` puede existir en dos clientes distintos.
- Complica el modelo y el DER respecto de lo que dice el documento, que hay que
  actualizar.
- El Partner introduce un estado de sesión adicional —la organización activa— que
  es una fuente clásica de errores si se cachea mal.
