# ADR 0004 — ADO.NET con DAO explícito en lugar de Entity Framework Core

**Estado:** Aprobado
**Fecha:** 2026-09-02

## Contexto

Elegido C# (ADR 0002), queda decidir cómo se accede a los datos. La opción por
defecto de la plataforma es Entity Framework Core: menos código, migraciones
automáticas, LINQ tipado y materialización de objetos resuelta.

El problema es qué queda de la capa DAO si se usa EF. Un `DbContext` **es** la capa
de acceso a datos: escribir un `IEquipoDao` que envuelve un `DbSet<Equipo>` produce
una capa que no hace nada más que delegar, y la separación que la cátedra evalúa
pasa a ser decorativa. El patrón Unit of Work tampoco tendría sentido propio: EF ya
lo implementa internamente en el `SaveChanges`.

En sentido contrario pesa que el volumen del sistema es chico —RNF-04 habla de 500
activos y 5000 incidencias— y que la mayoría de las consultas son CRUD sobre una
tabla. Es decir: el argumento de rendimiento a favor de SQL a mano no aplica acá, y
el de productividad a favor de EF sí, pero contra el objetivo académico.

Hay además un factor de continuidad: el proyecto de referencia de 3.º año usa
ADO.NET con un `SqlHelper` estático, `IObjectMapper<T>` para mapear el
`SqlDataReader`, y `SqlTransactRepository` como Unit of Work. Ese código está
probado y es directamente portable.

## Decisión

El acceso a datos se implementa con **ADO.NET** (`Microsoft.Data.SqlClient`),
SQL parametrizado con `SqlParameter` y mapeo explícito, siguiendo la estructura del
proyecto de referencia:

- `IGenericDao<T>` como contrato base, con los DAO específicos extendiéndolo.
- `IObjectMapper<T>` por entidad, que traduce `SqlDataReader` a objeto de dominio.
- `SqlHelper` para la base de negocio y `SqlHelperSeguridad` para la de servicio,
  cada uno con su cadena (ADR 0003).
- `IUnitOfWork` / `SqlTransactRepository` para las operaciones que abarcan varias
  tablas dentro de una transacción.
- `FactoryDao` como Abstract Factory, único punto donde se instancian los DAO.

**Todo parámetro va por `SqlParameter`.** No se concatena entrada de usuario en una
sentencia bajo ninguna circunstancia; es la mitigación de inyección SQL y se
verifica por análisis estático (ver ADR 0008).

El esquema se versiona como **scripts SQL idempotentes** en `db/`, no con
migraciones generadas: el script es el artefacto que se entrega y se lee en la
defensa, y tiene que ser legible.

## Alternativas consideradas

| Opción | Por qué se descartó |
|---|---|
| EF Core con `DbContext` | Vacía de contenido la capa DAO y el Unit of Work, que es justamente lo que se evalúa. |
| Dapper | Buen punto medio —SQL a la vista, mapeo automático— pero el mapeo automático es precisamente el `IObjectMapper<T>` que hay que mostrar escrito. |
| EF Core sólo para los catálogos y ADO.NET para el resto | Dos modelos mentales de acceso a datos en el mismo sistema, sin ganancia real. |

## Consecuencias

**Positivas**

- Cada patrón que la cátedra evalúa está escrito y es señalable en el código, no
  delegado a un framework.
- Control total sobre el SQL que se ejecuta, incluidas las consultas del motor
  predictivo, que agrupan por ventana temporal y no se expresan cómodamente en LINQ.
- Los scripts de `db/` son el esquema, legibles sin herramientas.

**Negativas y riesgos**

- **Mucho más código repetitivo.** Cada entidad necesita su DAO, su mapper y sus
  cuatro sentencias. Es el costo aceptado y hay que sostener la consistencia entre
  ellos.
- Un cambio de columna se propaga a mano: script, entidad, mapper y cada sentencia
  que la nombre. Sin compilador que avise. Se mitiga con pruebas de integración de
  DAO que recorren el CRUD completo de cada entidad contra la base real.
- El riesgo de inyección SQL pasa a depender de la disciplina del desarrollador en
  lugar de estar eliminado por construcción. Mitigado por revisión y SAST.
