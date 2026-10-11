<#
    Aplica los scripts de base contra el SQL Server del contenedor.

    Los scripts son idempotentes, asi que se puede correr las veces que haga
    falta. Se copian adentro del contenedor porque sqlcmd corre ahi: la imagen
    de SQL Server trae las herramientas, el host no necesariamente.

    Este archivo va sin acentos a proposito: Windows PowerShell 5.1 lee los
    .ps1 sin BOM como ANSI y los rompe.

    Uso:  .\db\aplicar.ps1            (esquemas + datos iniciales)
          .\db\aplicar.ps1 -SoloEsquema
#>
param(
    [string]$Contenedor  = 'predictit-sql',
    [string]$Password    = $env:MSSQL_SA_PASSWORD,
    # Contrasena del usuario con el que se conecta la aplicacion. La crea el
    # script 06. Sin ella se aplican los esquemas y se avisa que falta.
    [string]$AppPassword = $env:PREDICTIT_DB_PASSWORD,
    [switch]$SoloEsquema
)

if (-not $Password) {
    throw 'Falta la contrasena de sa. Pasala con -Password o pone MSSQL_SA_PASSWORD en el entorno.'
}

$ErrorActionPreference = 'Stop'
$aqui = Split-Path -Parent $MyInvocation.MyCommand.Path

$scripts = @('01-negocio-schema.sql', '02-servicio-schema.sql')
if (-not $SoloEsquema) { $scripts += @('03-seed-servicio.sql', '04-seed-negocio.sql', '05-seed-traducciones.sql') }

docker exec $Contenedor mkdir -p /tmp/db | Out-Null

foreach ($s in $scripts) {
    $ruta = Join-Path $aqui $s
    if (-not (Test-Path $ruta)) { throw "No existe $ruta" }

    docker cp $ruta "${Contenedor}:/tmp/db/$s" | Out-Null

    Write-Host "-> $s" -ForegroundColor Cyan
    # -f 65001: los scripts tienen acentos y estan en UTF-8.
    # -C: confia en el certificado autofirmado del contenedor.
    # -b: corta y devuelve exit code != 0 ante el primer error.
    # -I: QUOTED_IDENTIFIER ON. sqlcmd lo deja en OFF por defecto y asi fallan
    #     los indices filtrados (WHERE ... IS NOT NULL). SSMS ya lo trae en ON.
    docker exec $Contenedor /opt/mssql-tools18/bin/sqlcmd `
        -S localhost -U sa -P $Password -C -b -I -f 65001 -i "/tmp/db/$s"

    if ($LASTEXITCODE -ne 0) { throw "Fallo $s (exit $LASTEXITCODE)" }
}

# El usuario de la aplicacion va al final: necesita que las dos bases existan.
# Se pasa la contrasena como variable de sqlcmd, nunca escrita en el .sql.
if ($AppPassword) {
    $s = '06-usuario-de-aplicacion.sql'
    docker cp (Join-Path $aqui $s) "${Contenedor}:/tmp/db/$s" | Out-Null
    Write-Host "-> $s" -ForegroundColor Cyan
    docker exec $Contenedor /opt/mssql-tools18/bin/sqlcmd `
        -S localhost -U sa -P $Password -C -b -I -f 65001 `
        -v APP_PASSWORD="$AppPassword" -i "/tmp/db/$s"
    if ($LASTEXITCODE -ne 0) { throw "Fallo $s (exit $LASTEXITCODE)" }
} else {
    Write-Host 'Sin PREDICTIT_DB_PASSWORD: no se creo el usuario de aplicacion.' -ForegroundColor Yellow
}

Write-Host "`nBases aplicadas correctamente." -ForegroundColor Green
