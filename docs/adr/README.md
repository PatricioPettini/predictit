# Decisiones de arquitectura (ADR)

Registro de las decisiones de arquitectura significativas de PredictIT, en formato
Nygard. El criterio para escribir uno está en el [ADR 0001](0001-registrar-las-decisiones-de-arquitectura.md).

Un ADR aprobado no se edita. Si la decisión cambia, se escribe uno nuevo que lo
reemplaza y el anterior pasa a estado *Reemplazado*.

| # | Decisión | Estado | Impacto en el documento |
|---|---|---|---|
| [0001](0001-registrar-las-decisiones-de-arquitectura.md) | Registrar las decisiones de arquitectura | Aprobado | — |
| [0002](0002-csharp-aspnet-core-en-lugar-de-java-spring-boot.md) | C# / ASP.NET Core en lugar de Java / Spring Boot | Aprobado | Contradice 10.2.5 · D-01 |
| [0003](0003-dos-bases-de-datos-separadas.md) | Dos bases de datos separadas: negocio y servicio | Aprobado | — |
| [0004](0004-ado-net-con-dao-explicito-en-lugar-de-entity-framework.md) | ADO.NET con DAO explícito en lugar de EF Core | Aprobado | — |
| [0005](0005-multi-organizacion-desde-el-modelo-de-datos.md) | Multi-organización desde el modelo de datos | Aprobado | Amplía 10.4.7 · D-02 |
| [0006](0006-proveedor-de-ia-conmutable-con-asignacion-de-respaldo.md) | Proveedor de IA conmutable con asignación de respaldo | Aprobado | Amplía 1.7 / 6.4.1 / 10.2.3 · D-03, D-04 |
| [0007](0007-reglas-predictivas-parametrizadas-con-estrategias.md) | Reglas predictivas parametrizadas, resueltas con estrategias | Aprobado | — |
| [0008](0008-nivel-de-exigencia-en-carga-y-seguridad.md) | Nivel de exigencia en carga y seguridad | Aprobado | Amplía 10.2.2 RNF · D-08 |
| [0009](0009-patrones-de-resiliencia.md) | Patrones de resiliencia: circuit breaker y bulkhead | Aprobado | Amplía 10.3 · D-09 |
| [0010](0010-cifrado-reversible-separado-del-hash-de-contrasenas.md) | Cifrado reversible, separado del hash de contraseñas | Aprobado | Amplía 10.2.2 RNF-04 · D-16 |
| [0011](0011-el-motor-predictivo-se-dispara-solo.md) | El motor predictivo se dispara solo | Aprobado | Precisa 10.2.2 RF-11 · D-17 |
| [0012](0012-la-maquina-de-estados-de-la-incidencia-vive-en-la-bll.md) | La máquina de estados de la incidencia vive en la BLL | Aprobado | Amplía 10.2.3 CU-008 · D-20 |
| [0013](0013-reportes-en-pdf-generados-en-el-servidor.md) | Reportes en PDF generados en el servidor | Aprobado | Precisa 10.2.2 RF-14 · D-21 |
| [0014](0014-gestion-de-identidad-propia-sin-iam-externo.md) | Gestión de identidad propia, sin IAM externo | Aprobado | Amplía 10.3.3 · D-31 |
| [0015](0015-metricas-en-formato-prometheus.md) | Métricas en formato Prometheus, sin stack propio | Aprobado | Hace verificable 10.2.2 RNF-05 · D-53 |

## Cómo se relacionan con el resto de la documentación

- Cuando una decisión obliga a modificar el documento del TFI, queda anotada en
  [`docs/CAMBIOS-DOCUMENTO.md`](../CAMBIOS-DOCUMENTO.md) con la sección exacta.
- Cuando la decisión se tomó consultando a una herramienta de IA, la consulta y su
  validación quedan en
  [`docs/trazabilidad/PROMPTS-CRITICOS.md`](../trazabilidad/PROMPTS-CRITICOS.md).
