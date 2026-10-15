<#
    Deja la demostracion como recien instalada.

    `aplicar.ps1` es idempotente pero sus scripts solo insertan lo que falta:
    sirve para poner al dia una base, no para revertir lo que alguien toco.
    Si un visitante borra una organizacion o cambia los datos, volver a correr
    el seed no los repone. Este script si: borra las dos bases y las vuelve a
    armar desde cero.

    Existe porque el sistema esta publicado en internet con usuarios de
    demostracion. Las credenciales salieron del README publico para que no
    cualquiera entre, pero el que entre igual tiene que encontrarse con que
    restaurar cuesta un comando y no una tarde.

    ES DESTRUCTIVO Y A PROPOSITO. Sin -Si no hace nada: muestra que iba a
    hacer y termina. Nunca se corre contra una base que no sea la demo.

    Este archivo va sin acentos a proposito: Windows PowerShell 5.1 lee los
    .ps1 sin BOM como ANSI y los rompe.

    Uso:  .\db\restaurar-demo.ps1              (muestra el plan, no toca nada)
          .\db\restaurar-demo.ps1 -Si          (borra y rehace)
#>
param(
    [string]$Contenedor  = 'predictit-sql',
    [string]$Password    = $env:MSSQL_SA_PASSWORD,
    [string]$AppPassword = $env:PREDICTIT_DB_PASSWORD,
    [switch]$Si
)

$ErrorActionPreference = 'Stop'
$aqui = Split-Path -Parent $MyInvocation.MyCommand.Path
$bases = @('PredictIT_Negocio', 'PredictIT_Servicio')

if (-not $Password) {
    throw 'Falta la contrasena de sa. Pasala con -Password o pone MSSQL_SA_PASSWORD en el entorno.'
}

if (-not $Si) {
    Write-Host ''
    Write-Host 'RESTAURAR LA DEMOSTRACION' -ForegroundColor Yellow
    Write-Host ''
    Write-Host '  Esto BORRA las dos bases del contenedor y las vuelve a crear'
    Write-Host '  desde los scripts de db/. Se pierde todo lo que haya cargado'
    Write-Host '  cualquiera despues de la ultima restauracion.'
    Write-Host ''
    Write-Host ('  Contenedor : {0}' -f $Contenedor)
    Write-Host ('  Bases      : {0}' -f ($bases -join ', '))
    Write-Host ''
    Write-Host '  No se hizo nada. Para hacerlo de verdad:' -ForegroundColor Cyan
    Write-Host '      .\db\restaurar-demo.ps1 -Si'
    Write-Host ''
    exit 0
}

# Que el contenedor este ahi antes de prometer nada.
$estado = docker inspect -f '{{.State.Running}}' $Contenedor 2>$null
if ($LASTEXITCODE -ne 0) { throw "No encuentro el contenedor $Contenedor. Levantalo con: docker compose up -d" }
if ($estado -ne 'true') { throw "El contenedor $Contenedor existe pero no esta corriendo." }

# SINGLE_USER con rollback: si queda una sesion abierta -la API, SSMS- el DROP
# se cuelga esperando. Asi se corta lo que haya y se borra igual.
foreach ($b in $bases) {
    Write-Host ("-> borrando {0}" -f $b) -ForegroundColor Cyan
    $sql = @"
IF DB_ID(N'$b') IS NOT NULL
BEGIN
    ALTER DATABASE [$b] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$b];
END
"@
    docker exec $Contenedor /opt/mssql-tools18/bin/sqlcmd `
        -S localhost -U sa -P $Password -C -b -I -Q $sql
    if ($LASTEXITCODE -ne 0) { throw "No se pudo borrar $b (exit $LASTEXITCODE)" }
}

Write-Host ''
Write-Host 'Bases borradas. Rehaciendo esquemas y datos...' -ForegroundColor Cyan
Write-Host ''

$aplicar = Join-Path $aqui 'aplicar.ps1'
if (-not (Test-Path $aplicar)) { throw "No existe $aplicar" }
& $aplicar -Contenedor $Contenedor -Password $Password -AppPassword $AppPassword
if ($LASTEXITCODE -ne 0 -and $null -ne $LASTEXITCODE) { throw 'Fallo aplicar.ps1' }

Write-Host ''
Write-Host 'Demostracion restaurada.' -ForegroundColor Green
Write-Host 'Si la API estaba levantada, reinicia el contenedor para que tome las bases nuevas:' -ForegroundColor Yellow
Write-Host '    docker compose restart api'
