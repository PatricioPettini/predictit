# Cómo ejecutar PredictIT

> Sistema de mantenimiento predictivo de equipos informáticos
> Trabajo Final de Ingeniería · UAI · Pettini, Patricio Ezequiel

Hay dos formas. **La primera necesita solamente Docker** y es la más corta: los
contenedores compilan el backend y el frontend por dentro, así que no hace falta
tener instalado .NET ni Node.

---

## Forma 1 · Sólo con Docker (recomendada)

**Requisitos:** Docker Desktop instalado **y abierto**. Nada más.

### 1. Preparar las claves

```
copy .env.ejemplo .env
```

Abrir `.env` y reemplazar los tres valores marcados con `REEMPLAZAR`. Las dos
claves se generan con esto, en PowerShell:

```powershell
# Clave de firma de los tokens (PREDICTIT_JWT_CLAVE) — 48 bytes
[Convert]::ToBase64String((1..48 | %{ Get-Random -Max 256 }))

# Clave de cifrado de credenciales (PREDICTIT_CLAVE_CIFRADO) — 32 bytes
[Convert]::ToBase64String((1..32 | %{ Get-Random -Max 256 }))
```

La tercera, `PREDICTIT_DB_PASSWORD`, es cualquier contraseña con mayúscula,
minúscula, número y símbolo: la política de SQL Server la exige y el contenedor
no arranca si no la cumple.

Son dos claves distintas a propósito: quien pueda firmar tokens no tiene por qué
poder leer las credenciales de los clientes.

### 2. Levantar

```
docker compose up -d
```

La primera vez tarda: descarga la imagen de SQL Server —alrededor de 1,5 GB— y
compila las dos imágenes del sistema. Las veces siguientes son segundos.

El arranque aplica solo los esquemas de las dos bases y los datos de
demostración; no hay que correr nada más.

### 3. Entrar

| | |
|---|---|
| Aplicación | <http://localhost:8080> |
| API y Swagger | <http://localhost:8081/swagger> |

| Usuario | Contraseña | Perfil |
|---|---|---|
| `admin` | `Admin.2026` | Administrador |
| `tecnico1` … `tecnico3` | `Tecnico.2026` | Responsable Técnico |
| `solicitante` | `Usuario.2026` | Usuario Solicitante |
| `partner` | `Partner.2026` | Partner (varias organizaciones) |

### 4. Apagar

```
docker compose down
```

Los datos quedan en un volumen: al volver a levantar está todo como estaba. Para
empezar de cero, `docker compose down -v`.

---

## Forma 2 · Para trabajar sobre el código

**Requisitos:** .NET SDK 9.0, Node.js 20 o superior, Docker Desktop y Git.

```powershell
.\arrancar.ps1
```

El script verifica las dependencias, genera `appsettings.Development.json` con
una clave de firma al azar, levanta SQL Server en Docker, aplica los esquemas,
compila el backend, instala las dependencias del frontend y arranca los dos.

La aplicación queda en <http://localhost:5173> y la API en
<http://localhost:5216>.

---

## Qué mirar para entender el sistema

- **Dashboard** — el estado del parque, con el puntaje de riesgo que calcula el
  motor predictivo.
- **Activos → cualquier equipo** — la ficha trae el **desglose del riesgo**: una
  tarjeta por regla, con cuánto aportó sobre cuánto podía aportar y por qué. Es
  el centro del trabajo: el puntaje no es una caja negra.
- **Activos, las píldoras de arriba de la tabla** — riesgo alto, preventivo
  vencido y garantía vencida, con su recuento.
- **Incidencias → una incidencia** — quién la atiende y **por qué**: el sistema
  deja escrita la justificación de la asignación y si la decidió la IA, la
  heurística o una persona.
- **Configuración → Integración de IA** — el proveedor y su estado.

El proveedor de IA que corre por omisión es el **simulado**, determinístico y sin
red: no hace falta ninguna clave de API para ver el sistema completo. Es además
lo que hace demostrable el caso de prueba CP-14, la caída del servicio de IA.

---

## Si algo falla

**«falta MSSQL_SA_PASSWORD»** — no se copió `.env.ejemplo` a `.env`.

**La API no levanta y dice que falta la clave** — la clave de firma tiene que
tener al menos 32 bytes. El sistema se niega a arrancar con una más corta en vez
de funcionar con una clave débil.

**El puerto 1433 está ocupado** — hay un SQL Server instalado en la máquina
escuchando ahí. Hay que detenerlo (Servicios → SQL Server → Detener) o cambiarle
el puerto: el sistema usa el suyo, en el contenedor.

**Docker dice que no puede conectarse** — Docker Desktop está instalado pero no
abierto.
