# PredictIT — instrucciones para Claude Code

Sistema de mantenimiento predictivo de equipos informáticos para PyMEs.
Trabajo Final de Ingeniería — UAI, Facultad de Tecnología Informática.
Alumno: Pettini, Patricio Ezequiel.

## Al arrancar la sesión

1. Leé `docs/CAMBIOS-DOCUMENTO.md`. Registra en qué se aparta el sistema del TFI
   y por qué. **Se actualiza en el momento**, no al final.
1b. Leé `docs/adr/README.md`. Las decisiones de arquitectura ya tomadas no se
   rediscuten: si hay que cambiar una, se escribe un ADR nuevo que la reemplaza.
2. El documento fuente es
   `C:\Users\Patricio\Downloads\Trabajo+Final+de+Ingenieria_Patricio+Pettini.pdf`
   (v2.0, 10/07/2026). El capítulo 10 es la especificación: RF-01…RF-14,
   RNF-01…RNF-07, CU-001…CU-013, CP-01…CP-14, los dos DER y las 16 pantallas.
3. Los diagramas y las pantallas del documento son **referencia, no contrato**.
   Se van a ir ajustando a lo que termine siendo el sistema.

## Arquitectura — las reglas que no se negocian

Es lo que evalúa la cátedra. Antes de escribir una línea, ubicá en qué capa va.

- `PredictIT.Api` sólo habla con `PredictIT.BLL`. **Nunca** toca DAO ni SQL.
- `PredictIT.BLL` orquesta, valida y decide. Devuelve DTOs, nunca entidades crudas.
- `PredictIT.DAO` es el único que conoce SQL. ADO.NET con `SqlParameter`, siempre.
- `PredictIT.Domain` no referencia a nadie.
- `PredictIT.Service` es transversal: lo consumen BLL y Api, nunca al revés.

Patrones que ya están decididos y hay que respetar: `IGenericDao<T>`,
`IObjectMapper<T>`, Abstract Factory (`FactoryDao` / `FactoryBusiness`),
Unit of Work (`SqlTransactRepository`), Composite (Familia/Patente),
Strategy (reglas del motor predictivo), Adapter (`IProveedorIA`).

## Las dos bases

`PredictIT_Negocio` y `PredictIT_Servicio`, con **cadenas de conexión separadas**.
Un DAO de negocio no puede leer la de seguridad ni viceversa: ése es el punto de
la separación. Las referencias a `Usuario` desde el negocio son lógicas (mismo
GUID), nunca claves foráneas.

Toda tabla de negocio lleva `id_organizacion` y el filtro se aplica **en el DAO**
a partir del contexto de sesión, no en el controller.

Ojo con T-SQL: `plan` y `Backup` son palabras reservadas, por eso las columnas y
tablas se llaman `plan_comercial` y `Respaldo`.

## Setup

```powershell
docker compose up -d          # SQL Server 2022
.\db\aplicar.ps1              # esquemas + datos iniciales (idempotente)
cd backend; dotnet test       # tiene que dar todo verde
```

Usuarios de demo: `admin`, `tecnico1..3`, `solicitante` y `partner`. Las
contraseñas están en el documento de la entrega, no en el repositorio:
éste es público y el sistema está desplegado. Para generar unas nuevas,
`.\db\cambiar-contrasenas-demo.ps1 -Si`.

## Entregables de transparencia (obligatorios, no opcionales)

La cátedra exige trazabilidad del uso de IA. Estos tres artefactos se mantienen
**durante** el trabajo, no se reconstruyen al final:

- **`docs/adr/`** — toda decisión de arquitectura significativa lleva su ADR en
  formato Nygard antes de escribir el código que depende de ella. El criterio de
  significatividad está en el ADR 0001.
- **`docs/trazabilidad/AI-USAGE-LOG.md`** — una entrada por entrega: tareas, qué se
  aceptó, qué se modificó o descartó, y cómo se validó. **Se escribe lo que pasó**,
  incluidos los errores encontrados: es un documento de integridad académica y un
  registro falso se cae en la defensa.
- **`docs/trazabilidad/PROMPTS-CRITICOS.md`** — las consultas que afectaron el
  diseño, en formato C4 (Contexto · Constraints · Criterio · Output), con su
  validación técnica.

Método para decisiones de arquitectura:

1. **Patrón A** — nunca una única solución: comparar alternativas bajo atributos de
   calidad explícitos, con tabla y consecuencias negativas de cada una.
2. **Patrón B** — antes de implementar, auditar el propio diseño: puntos únicos de
   falla, cuellos de botella a 300 % de la carga, acoplamientos no deseados.
3. **Patrón C** — formalizar el resultado como ADR.

Y el nivel de exigencia de verificación es **superior al estándar**, por la velocidad
que aporta la asistencia por IA (ADR 0008): analizadores en modo estricto con
`TreatWarningsAsErrors`, `dotnet list package --vulnerable` en cada entrega, pruebas
de carga con perfil definido, y perfilado de memoria bajo carga sostenida.

## Reglas de trabajo

- **Respuestas breves.** Sin narrativa extra ni recap al final.
- **Español rioplatense**, tono directo.
- **Pushear a `origin/main` después de cada commit.** El remoto es
  `https://github.com/PatricioPettini/trabajo-final-ingenier-a.git` y el usuario
  pidió que todo el trabajo quede ahí. No hace falta confirmar cada push.
- **Preferir editar archivos existentes** antes que crear nuevos.
- **Comentarios: el "por qué", no el "qué".** Sólo cuando la razón no es obvia.
- Cualquier operación destructiva (`git reset --hard`, `rm -rf`, `DROP`):
  **preguntar antes**.
- **Ninguna fase se da por terminada con tests en rojo.** Correr `dotnet test`
  antes de decir que algo está listo.
- Los `.ps1` van **sin acentos**: Windows PowerShell 5.1 lee los scripts sin BOM
  como ANSI y los rompe.

## Estructura

```
TFI/
├─ db/                 esquemas, datos iniciales y aplicar.ps1
├─ backend/            PredictIT.sln (Domain, DAO, BLL, Service, Api, Tests)
├─ frontend/           React 19 + TypeScript + Vite
├─ docs/               CAMBIOS-DOCUMENTO.md y documentación técnica
└─ docker-compose.yml  SQL Server 2022
```
