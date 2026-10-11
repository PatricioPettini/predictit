# Levanta PredictIT completo: base de datos, API y frontend.
#
# Sin acentos a proposito: PowerShell 5.1 lee los .ps1 sin BOM como ANSI y los
# rompe.
#
# Uso:
#   .\arrancar.ps1              levanta todo
#   .\arrancar.ps1 -SoloBase    solo el contenedor de SQL Server y los scripts
#   .\arrancar.ps1 -IaCaida     el proveedor de IA falla siempre (demo del CP-14)
#   .\arrancar.ps1 -Detener     baja la API y el frontend

param(
    [switch]$SoloBase,
    [switch]$IaCaida,
    [switch]$Detener
)

$ErrorActionPreference = 'Stop'
$raiz = $PSScriptRoot

function Paso($texto) { Write-Host "`n==> $texto" -ForegroundColor Cyan }
function Ok($texto)   { Write-Host "    $texto" -ForegroundColor Green }
function Aviso($texto){ Write-Host "    $texto" -ForegroundColor Yellow }

# ---------------------------------------------------------------- detener
if ($Detener) {
    Paso 'Deteniendo API y frontend'
    Get-Process -Name PredictIT.Api -ErrorAction SilentlyContinue | Stop-Process -Force
    Get-CimInstance Win32_Process -Filter "Name = 'node.exe'" |
        Where-Object { $_.CommandLine -like '*TFI\frontend*' } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    Ok 'Listo. El contenedor de SQL Server sigue andando (docker compose down para bajarlo).'
    exit 0
}

# ------------------------------------------------------------------ entorno
# Las contrasenas viven en .env, que esta en .gitignore. docker compose lo lee
# solo; la API y aplicar.ps1 corren en el host y no, asi que se cargan aca.
#
# Sin esto la API arranca sin PREDICTIT_DB_PASSWORD y falla al primer acceso a
# la base, con un mensaje que no dice cual es el problema real.
$archivoEnv = Join-Path $raiz '.env'
if (Test-Path $archivoEnv) {
    foreach ($linea in Get-Content $archivoEnv) {
        if ($linea -match '^\s*([A-Z_][A-Z0-9_]*)\s*=\s*(.*)$') {
            Set-Item -Path ("Env:" + $matches[1]) -Value $matches[2].Trim()
        }
    }
} else {
    throw "Falta .env en la raiz. Copiar .env.ejemplo y completar las claves."
}
foreach ($v in 'MSSQL_SA_PASSWORD', 'PREDICTIT_DB_PASSWORD') {
    if (-not (Get-Item "Env:$v" -ErrorAction SilentlyContinue)) {
        throw "Falta $v en .env. Esta en .env.ejemplo."
    }
}

# ------------------------------------------------------------ dependencias
Paso 'Verificando dependencias'

$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) {
    # No siempre esta en el PATH de la sesion.
    $candidato = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
    if (Test-Path $candidato) {
        $env:Path = (Split-Path $candidato) + ';' + $env:Path
        $dotnet = $candidato
    }
}
if (-not $dotnet) { throw 'No se encontro dotnet. Instalar el SDK de .NET 9.' }
Ok "dotnet: $dotnet"

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'No se encontro docker. Hace falta Docker Desktop andando.'
}
if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    throw 'No se encontro npm. Hace falta Node.js.'
}
Ok 'docker y npm presentes'

# ------------------------------------------------- clave de firma del token
$devSettings = Join-Path $raiz 'backend\PredictIT.Api\appsettings.Development.json'
if (-not (Test-Path $devSettings)) {
    Paso 'Falta appsettings.Development.json: se crea desde la plantilla'
    $plantilla = "$devSettings.ejemplo"
    $clave = [Convert]::ToBase64String([byte[]](1..48 | ForEach-Object { Get-Random -Maximum 256 }))
    # AES-256 pide exactamente 32 bytes. Es otra clave y no la misma: quien
    # pueda firmar tokens no tiene por que poder leer credenciales guardadas.
    $claveCifrado = [Convert]::ToBase64String([byte[]](1..32 | ForEach-Object { Get-Random -Maximum 256 }))
    ((Get-Content $plantilla -Raw) -replace 'REEMPLAZAR-POR-UNA-CLAVE-DE-AL-MENOS-32-CARACTERES', $clave) `
        -replace 'REEMPLAZAR-POR-UNA-CLAVE-AES-256-EN-BASE64', $claveCifrado |
        Out-File $devSettings -Encoding utf8 -NoNewline
    Ok 'Creado con una clave de firma y una de cifrado nuevas.'
}

# ------------------------------------------------------------ base de datos
Paso 'Base de datos'
$corriendo = docker ps --filter 'name=predictit-sql' --format '{{.Names}}'
if (-not $corriendo) {
    Aviso 'El contenedor no esta arriba: se levanta con docker compose.'
    Push-Location $raiz
    docker compose up -d
    Pop-Location

    # SQL Server tarda en aceptar conexiones despues de arrancar.
    Write-Host '    Esperando a que SQL Server acepte conexiones' -NoNewline
    foreach ($i in 1..60) {
        $estado = docker inspect --format '{{.State.Health.Status}}' predictit-sql 2>$null
        if ($estado -eq 'healthy') { break }
        Write-Host '.' -NoNewline
        Start-Sleep -Seconds 2
    }
    Write-Host ''
}
Ok 'Contenedor predictit-sql arriba'

Paso 'Aplicando esquemas y datos de demostracion'
Push-Location (Join-Path $raiz 'db')
& .\aplicar.ps1
Pop-Location

if ($SoloBase) {
    Ok 'Listo (solo base).'
    exit 0
}

# -------------------------------------------------------------- compilacion
Paso 'Compilando el backend'
# Si la API quedo corriendo de antes, tiene el DLL tomado y la compilacion falla.
Get-Process -Name PredictIT.Api -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
& $dotnet build (Join-Path $raiz 'backend\PredictIT.Api') --nologo -v quiet
Ok 'Backend compilado'

Paso 'Instalando dependencias del frontend'
Push-Location (Join-Path $raiz 'frontend')
if (-not (Test-Path 'node_modules')) { npm install --silent } else { Ok 'node_modules ya esta' }
Pop-Location

# --------------------------------------------------------------- arranque
Paso 'Levantando la API en http://localhost:5099'
$argumentosApi = @(
    'run', '--project', (Join-Path $raiz 'backend\PredictIT.Api'),
    '--no-launch-profile', '--no-build'
)
$entorno = @{
    ASPNETCORE_ENVIRONMENT = 'Development'
    ASPNETCORE_URLS        = 'http://localhost:5099'
}
if ($IaCaida) {
    # Fuerza que el proveedor simulado falle en cada llamada: es la demostracion
    # del CP-14 y de las transiciones del disyuntor sobre el sistema andando.
    $entorno['PREDICTIT_IA_FALLAR_CADA'] = '1'
    Aviso 'MODO CP-14: el proveedor de IA va a fallar siempre.'
}
foreach ($k in $entorno.Keys) { Set-Item -Path "Env:$k" -Value $entorno[$k] }

Start-Process -FilePath $dotnet -ArgumentList $argumentosApi -WorkingDirectory $raiz `
              -WindowStyle Minimized

Write-Host '    Esperando a la API' -NoNewline
$arriba = $false
foreach ($i in 1..40) {
    Start-Sleep -Seconds 1
    try {
        Invoke-WebRequest -Uri 'http://localhost:5099/api/equipos' -UseBasicParsing -TimeoutSec 2 | Out-Null
        $arriba = $true; break
    } catch {
        # Un 401 significa que la API esta viva y rechazando al anonimo: alcanza.
        if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 401) { $arriba = $true; break }
    }
    Write-Host '.' -NoNewline
}
Write-Host ''
if (-not $arriba) { throw 'La API no respondio. Ver la ventana minimizada de dotnet.' }
Ok 'API respondiendo'

Paso 'Levantando el frontend en http://localhost:5173'
Start-Process -FilePath 'cmd.exe' -ArgumentList '/c', 'npm run dev' `
              -WorkingDirectory (Join-Path $raiz 'frontend') -WindowStyle Minimized
Start-Sleep -Seconds 4
Ok 'Frontend arriba'

Write-Host @'

--------------------------------------------------------------------
  PredictIT andando

  Frontend    http://localhost:5173
  API         http://localhost:5099
  Swagger     http://localhost:5099/swagger

  Usuarios de demostracion (verificados contra los hashes del seed):

    admin         Admin.2026      Administrador del Estudio Pettini
    tecnico1      Tecnico.2026    Responsable Tecnico
    solicitante   Usuario.2026    Usuario Solicitante (modulo del cliente)
    partner       Partner.2026    Partner: administra dos organizaciones

  Que mirar:
    /activos                52 equipos, filtros y detalle con garantia
    /incidencias            listado; la franja ambar son las de respaldo
    /incidencias/nueva      cargar una: el triage clasifica y asigna solo
    /analisis               reevaluar el parque y ver el ranking de riesgo
    /configuracion          el plan contratado y la cuenta del abono

  Para detener:  .\arrancar.ps1 -Detener
  Demo del CP-14: .\arrancar.ps1 -IaCaida
--------------------------------------------------------------------
'@ -ForegroundColor White
