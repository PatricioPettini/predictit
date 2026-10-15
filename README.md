# PredictIT


Sistema web de gestión y **mantenimiento predictivo de equipos informáticos**
para pequeñas y medianas empresas.

Trabajo Final de Ingeniería — Universidad Abierta Interamericana, Facultad de
Tecnología Informática. Alumno: Pettini, Patricio Ezequiel.

> **El sistema, funcionando:
> https://predictit-uai.mexicocentral.cloudapp.azure.com** — con Swagger en
> [`/swagger`](https://predictit-uai.mexicocentral.cloudapp.azure.com/swagger) y
> los usuarios de demostración que el documento de la entrega detalla. Corre
> en Microsoft Azure con
> `docker compose`. Si la dirección no responde, la máquina está apagada
> para no consumir crédito.

En lugar de esperar a que un equipo falle, el sistema acumula el historial
técnico de cada activo —incidencias, mantenimientos, cambios de estado— y aplica
reglas configurables sobre ese historial para calcular un nivel de riesgo por
equipo y generar alertas accionables antes de que la falla ocurra.

## Qué hace

- **Inventario de activos** con ubicación, responsable, estado y garantía.
- **Gestión de incidencias** de punta a punta, con clasificación automática del
  texto libre del solicitante y asignación del técnico responsable mediante un
  servicio externo de IA (con reasignación manual y asignación de respaldo si el
  servicio no responde).
- **Mantenimientos** preventivos, correctivos y predictivos, vinculados al
  historial del equipo.
- **Motor predictivo**: scoring de riesgo por equipo y alertas priorizadas.
- **Dashboard y reportes** sobre el estado del parque.
- **Panel de administración** y **módulo del cliente**, con permisos
  diferenciados por perfil.

## Stack

| Capa | Tecnología |
|---|---|
| Backend | ASP.NET Core 9 (Web API), C# |
| Acceso a datos | ADO.NET (`Microsoft.Data.SqlClient`) |
| Base de datos | SQL Server 2022 — dos bases: negocio y servicio |
| Frontend | React 19 + TypeScript + Vite |
| Pruebas | xUnit + FluentAssertions |
| Entorno | Docker Compose |

## Cómo levantarlo

Hay dos caminos, para dos cosas distintas.

### Para probarlo — sólo Docker

No hace falta .NET ni Node instalados.

```powershell
cp .env.ejemplo .env      # y generar las dos claves como indica el archivo
docker compose up --build
```

| | |
|---|---|
| Aplicación | http://localhost:8080 |
| API y Swagger | http://localhost:8081/swagger |

Levanta cuatro contenedores: la base, un trabajo que aplica esquema y datos y
termina, la API, y el frontend detrás de nginx. El frontend pide a `/api` y nginx
lo proxea al contenedor de la API, así que la URL del backend no queda escrita en
el código del frontend ni hay CORS que resolver.

Los scripts de base son idempotentes —`MERGE` e `IF NOT EXISTS`—, así que volver
a levantar el stack reaplica y sale, sin duplicar nada.

Para la demostración del **CP-14** —caída del servicio de IA— alcanza con poner
`PREDICTIT_IA_FALLAR_CADA=1` en el `.env` y recrear la API.

### Para desarrollar — con recarga en caliente

Requisitos: [.NET SDK 9](https://dotnet.microsoft.com/download), Docker Desktop
y Node.js 20+.

```powershell
.\arrancar.ps1
```

Levanta sólo la base en Docker y corre la API y Vite en el host, así un cambio se
ve sin reconstruir imágenes. A mano sería:

```powershell
docker compose up -d sqlserver
.\db\aplicar.ps1
cd backend
dotnet test                                  # tiene que dar todo verde
dotnet run --project PredictIT.Api
```

### Verificación

```powershell
$env:PREDICTIT_DB_PASSWORD = "la del .env"   # las de integración usan la base real
cd backend;  dotnet test                     # 284 pruebas
cd frontend; npm test; npm run build         # 34 pruebas
```

Lo mismo corre solo en cada push y en cada pull request a `main`
([workflow](.github/workflows/ci.yml)), en tres trabajos separados: backend
contra un SQL Server real, frontend, y la construcción de las dos imágenes. Están
separados a propósito: con un solo trabajo, el primer fallo esconde todo lo demás.

### Usuarios de demo

Hay cuatro perfiles de prueba —Administrador, Responsable Técnico, Usuario
Solicitante y Partner— y conviene entrar con más de uno, porque cada uno ve un
sistema distinto: los permisos se resuelven con patentes y familias.

**Las credenciales no están en este archivo.** Van en el documento de la
entrega y en el mensaje a la cátedra. El motivo es simple: este repositorio es
público y el enlace de arriba apunta a un sistema que está corriendo, así que
publicar un usuario administrador al lado de su dirección es ofrecerle a
cualquiera que lo rompa. Los datos son de demostración y se pueden restaurar
—`db/restaurar-demo.ps1`, un comando—, pero restaurarlos no es gratis si pasa
el día de la corrección.

Si estás evaluando este trabajo y no tenés las credenciales a mano, están en la
primera página del documento que acompaña la entrega.

## Documentación

| | |
|---|---|
| [Decisiones de arquitectura](docs/adr/) | 15 ADR en formato Nygard |
| [Patrones y principios](docs/PATRONES-Y-PRINCIPIOS.md) | Dónde vive cada patrón, con archivo y línea. Y lo que **no** está aplicado |

Este repositorio es una copia del código fuente y de la documentación de
arquitectura. El documento del trabajo final, los manuales y la gestión del
proyecto se entregan a la cátedra por separado.

## Estructura

```
TFI/
├─ db/                 esquemas, datos iniciales y el script que los aplica
├─ backend/
│   ├─ PredictIT.Domain/    entidades del dominio
│   ├─ PredictIT.DAO/       acceso a datos (el único que conoce SQL)
│   ├─ PredictIT.BLL/       lógica de negocio
│   ├─ PredictIT.Service/   servicios transversales
│   ├─ PredictIT.Api/       API REST
│   └─ PredictIT.Tests/     pruebas
├─ frontend/           aplicación React
├─ docs/               decisiones de arquitectura y patrones
└─ docker-compose.yml
```

## Sobre la integración de IA

El sistema **no entrena modelos propios**. Consulta un servicio externo de
lenguaje para clasificar la incidencia y recomendar el técnico, y el resultado
siempre es editable por el técnico.

La integración está detrás de una interfaz con dos implementaciones: la real
contra la API de Anthropic y una simulada determinística, que es el modo por
defecto. Esto permite demostrar el sistema completo sin conexión ni crédito de
API, y es también lo que hace verificable el caso de prueba CP-14 (comportamiento
del sistema cuando el servicio de IA se cae).
