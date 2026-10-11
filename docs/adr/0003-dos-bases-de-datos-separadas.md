# ADR 0003 — Dos bases de datos separadas: negocio y servicio

**Estado:** Aprobado
**Fecha:** 2026-09-02

## Contexto

El DER del documento (10.4.7) ya está dibujado como dos diagramas: *DER Negocio*
—equipos, incidencias, mantenimientos, alertas— y *DER Servicio* —usuarios,
patentes, familias, bitácora—. Queda por decidir si esa separación conceptual se
traduce en dos bases físicas o en dos grupos de tablas dentro de una sola.

La tensión es concreta. Una sola base permite claves foráneas entre
`Incidencia.id_tecnico` y `Usuario`, integridad referencial garantizada por el
motor, y consultas con `JOIN` directo. Dos bases obligan a resolver esas
referencias por convención y a hacer dos consultas donde podría haber una.

A favor de separar pesan tres cosas:

- El Req. Arq. 002 exige que la bitácora sea inmutable y auditable. Una base de
  seguridad con su propio ciclo de respaldo y sus propios permisos hace esa
  exigencia estructural en vez de declarativa.
- El sistema es multi-organización (ADR 0005). Los datos de negocio de un cliente
  y las credenciales de acceso tienen ciclos de vida y sensibilidades distintas.
- La cátedra evalúa la separación de responsabilidades, y una base de servicio
  reutilizable entre sistemas es la forma más literal de demostrarla. El proyecto de
  referencia de 3.º año hace exactamente esto con `FastFoodDB` y `ServicesPP`.

## Decisión

Se usan **dos bases de datos con cadenas de conexión separadas**:

- `PredictIT_Negocio` — organizaciones, ubicaciones, equipos, incidencias,
  clasificaciones, recomendaciones de asignación, mantenimientos, reglas, alertas y
  evaluaciones de riesgo.
- `PredictIT_Servicio` — usuarios, roles, familias, patentes, bitácora, tipos de
  evento, idiomas, traducciones y respaldos.

Ningún DAO de negocio puede alcanzar la base de servicio ni al revés: cada capa de
acceso recibe su propia cadena y no conoce la otra. Las referencias cruzadas
—`Equipo.id_responsable`, `Incidencia.id_tecnico`, `Mantenimiento.id_tecnico`— son
**referencias lógicas por GUID**, documentadas como tales en el esquema, sin clave
foránea.

La integridad de esas referencias se valida en la BLL antes de persistir, no en el
motor.

## Alternativas consideradas

| Opción | Por qué se descartó |
|---|---|
| Una sola base con esquemas `negocio` y `seguridad` | Conserva las claves foráneas y simplifica todo, pero la separación pasa a ser una convención de nombres que nada impide violar, y se pierde el argumento de respaldo y permisos independientes. |
| Dos bases con *synonyms* o consultas de tres partes | Recupera el `JOIN` a costa de perforar exactamente el aislamiento que motiva la decisión. |

## Consecuencias

**Positivas**

- La separación es real y verificable: un DAO de negocio no tiene forma de leer una
  contraseña ni de escribir en la bitácora.
- Cada base se respalda y se restaura por separado, y la de servicio puede
  reutilizarse en otro sistema sin arrastrar el dominio.
- La inmutabilidad de la bitácora se garantiza donde corresponde —un trigger
  `INSTEAD OF UPDATE, DELETE` en la base de servicio— y no depende de que la
  aplicación se porte bien.

**Negativas y riesgos**

- **No hay integridad referencial entre bases.** Un usuario borrado deja
  incidencias apuntando a un GUID inexistente. Se mitiga con baja lógica
  (`Usuario.activo`), nunca física, y con validación en la BLL.
- Toda pantalla que muestre "el técnico de la incidencia" necesita dos consultas y
  una composición en la BLL. Es el costo recurrente de la decisión.
- Las pruebas de integración tienen que preparar y limpiar dos bases, y una
  transacción no puede abarcar las dos sin transacción distribuida —que
  deliberadamente no se usa—. Las operaciones que tocan ambas (registrar una
  incidencia y asentar la bitácora) no son atómicas: se resuelve asentando la
  bitácora **después** del commit de negocio, aceptando que un fallo entre ambos
  pasos pierde el registro de auditoría pero nunca el dato de negocio.
