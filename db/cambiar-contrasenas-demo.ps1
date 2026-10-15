<#
    Cambia las contrasenas de los usuarios de demostracion.

    Para que las del sistema publicado dejen de ser las del seed, que estan en
    un repositorio publico. Genera contrasenas nuevas al azar, calcula el hash
    con el mismo algoritmo que usa la aplicacion -PBKDF2-SHA256, 600.000
    iteraciones, formato PBKDF2$iteraciones$salt$hash- y actualiza la base.

    Las contrasenas nuevas se muestran en pantalla y se escriben en un archivo
    que git ignora. No quedan en el repositorio ni en su historial: de ahi se
    copian al documento de la entrega y al mensaje a la catedra, y despues se
    borra el archivo.

    Este archivo va sin acentos a proposito: Windows PowerShell 5.1 lee los
    .ps1 sin BOM como ANSI y los rompe.

    Uso:  .\db\cambiar-contrasenas-demo.ps1              (muestra el plan)
          .\db\cambiar-contrasenas-demo.ps1 -Si          (las cambia)

    Contra el servidor publicado, correrlo adentro de esa maquina o apuntar
    -Contenedor y -Password al SQL de alla.

    Probado el 15/10/2026 contra el contenedor local: las seis contrasenas
    cambiaron, el login con la nueva devolvio token y con la vieja fue
    rechazado. Despues se restauro la demo para dejar la base como estaba.
#>
param(
    [string]$Contenedor = 'predictit-sql',
    [string]$Password   = $env:MSSQL_SA_PASSWORD,
    [string]$Salida     = 'credenciales-nuevas.txt',
    [switch]$Si
)

$ErrorActionPreference = 'Stop'
$aqui = Split-Path -Parent $MyInvocation.MyCommand.Path

# Los usuarios de demostracion y el perfil de cada uno, para el listado final.
$usuarios = @(
    @{ u = 'admin';       perfil = 'Administrador' },
    @{ u = 'tecnico1';    perfil = 'Responsable Tecnico' },
    @{ u = 'tecnico2';    perfil = 'Responsable Tecnico' },
    @{ u = 'tecnico3';    perfil = 'Responsable Tecnico' },
    @{ u = 'solicitante'; perfil = 'Usuario Solicitante' },
    @{ u = 'partner';     perfil = 'Partner' }
)

if (-not $Password) {
    throw 'Falta la contrasena de sa. Pasala con -Password o pone MSSQL_SA_PASSWORD en el entorno.'
}

if (-not $Si) {
    Write-Host ''
    Write-Host 'CAMBIAR LAS CONTRASENAS DE DEMOSTRACION' -ForegroundColor Yellow
    Write-Host ''
    Write-Host '  Genera una contrasena nueva para cada uno de estos usuarios y'
    Write-Host '  reemplaza su hash en la base. Las actuales dejan de servir.'
    Write-Host ''
    foreach ($x in $usuarios) { Write-Host ('    {0,-12} {1}' -f $x.u, $x.perfil) }
    Write-Host ''
    Write-Host '  Las nuevas se escriben en:' -ForegroundColor Cyan
    Write-Host ('      {0}' -f (Join-Path $aqui $Salida))
    Write-Host '  Ese archivo esta en .gitignore. Copia las credenciales al'
    Write-Host '  documento de la entrega y borralo.'
    Write-Host ''
    Write-Host '  ANTES de correrlo contra el sistema publicado: el documento y' -ForegroundColor Yellow
    Write-Host '  el mensaje a la catedra tienen que quedar con las nuevas, o el'
    Write-Host '  profesor no va a poder entrar.'
    Write-Host ''
    Write-Host '  No se hizo nada. Para hacerlo de verdad:' -ForegroundColor Cyan
    Write-Host '      .\db\cambiar-contrasenas-demo.ps1 -Si'
    Write-Host ''
    exit 0
}

# Una contrasena legible pero no adivinable: tres silabas, un numero y un
# simbolo. Sale del generador criptografico y no de Get-Random, que es
# predecible a partir de su semilla.
function Nueva-Contrasena {
    $silabas = 'ba','ce','di','fo','gu','la','me','ni','ro','su','ta','ve','zu','pa','qui','xe'
    $simbolos = '.','-','_','#'
    # RNGCryptoServiceProvider y no RandomNumberGenerator::Fill: Fill llego con
    # .NET Core y Windows PowerShell 5.1 corre sobre .NET Framework.
    $bytes = New-Object byte[] 8
    $rng = New-Object System.Security.Cryptography.RNGCryptoServiceProvider
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    $p = ''
    for ($i = 0; $i -lt 3; $i++) { $p += $silabas[$bytes[$i] % $silabas.Count] }
    $p = $p.Substring(0,1).ToUpper() + $p.Substring(1)
    $p += ($bytes[3] % 90) + 10
    $p += $simbolos[$bytes[4] % $simbolos.Count]
    $p += $silabas[$bytes[5] % $silabas.Count]
    return $p
}

# El hash, con el mismo formato que HashContrasena.cs:
# PBKDF2$iteraciones$salt-en-base64$hash-en-base64
function Hash-Contrasena([string]$texto) {
    $iteraciones = 600000
    $salt = New-Object byte[] 16
    $rng = New-Object System.Security.Cryptography.RNGCryptoServiceProvider
    try { $rng.GetBytes($salt) } finally { $rng.Dispose() }
    $kdf = New-Object System.Security.Cryptography.Rfc2898DeriveBytes(
        $texto, $salt, $iteraciones, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
    try { $hash = $kdf.GetBytes(32) } finally { $kdf.Dispose() }
    return ('PBKDF2${0}${1}${2}' -f $iteraciones,
            [Convert]::ToBase64String($salt), [Convert]::ToBase64String($hash))
}

$estado = docker inspect -f '{{.State.Running}}' $Contenedor 2>$null
if ($LASTEXITCODE -ne 0) { throw "No encuentro el contenedor $Contenedor." }

$lineas = @('Credenciales de demostracion de PredictIT',
            ('Generadas el {0}' -f (Get-Date -Format 'dd/MM/yyyy HH:mm')),
            '',
            'Van al documento de la entrega y al mensaje a la catedra.',
            'Este archivo NO se versiona: copialas y borralo.',
            '')

# Los UPDATE van en un archivo y se ejecutan con -x, que apaga la sustitucion
# de variables de sqlcmd. Pasarlos con -v no sirve: el hash tiene el formato
# PBKDF2$iteraciones$salt$hash y el salt en base64 suele terminar en "==", asi
# que sqlcmd ve "==$..." y corta el valor por la mitad. El sintoma es un hash
# de 36 caracteres en una columna de 300 y un login que falla con la
# contrasena recien puesta.
$sql = @('SET NOCOUNT ON;')
$claras = @{}
foreach ($x in $usuarios) {
    $clara = Nueva-Contrasena
    $claras[$x.u] = $clara
    $hash = Hash-Contrasena $clara
    # Las columnas son `username` y `password`: el esquema esta en
    # db/02-servicio-schema.sql. base64 no trae comillas simples, asi que
    # embeber el hash como literal es seguro.
    $sql += ("UPDATE PredictIT_Servicio.dbo.Usuario SET password = '{0}', intentos_fallidos = 0 WHERE username = '{1}';" -f $hash, $x.u)
}

# El archivo de credenciales se escribe ANTES de tocar la base. Si el UPDATE o
# la limpieza fallan a mitad de camino, las contrasenas ya estan a salvo; al
# reves se aplican y se pierden, y hay que restaurar la demo para recuperar el
# acceso. Paso la primera vez que se corrio esto.
foreach ($x in $usuarios) {
    $lineas += ('{0,-12} {1,-22} {2}' -f $x.u, $claras[$x.u], $x.perfil)
}
$ruta = Join-Path $aqui $Salida
$lineas | Set-Content -Path $ruta -Encoding UTF8

$tmp = Join-Path $env:TEMP ('predictit-pass-{0}.sql' -f [guid]::NewGuid())
$sql | Set-Content -Path $tmp -Encoding ASCII
try {
    docker cp $tmp "${Contenedor}:/tmp/pass.sql" | Out-Null
    docker exec $Contenedor /opt/mssql-tools18/bin/sqlcmd `
        -S localhost -U sa -P $Password -C -b -I -x -i /tmp/pass.sql | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Fallo el cambio de contrasenas (exit $LASTEXITCODE)" }
} finally {
    Remove-Item $tmp -ErrorAction SilentlyContinue
    # Como root: el archivo entra por `docker cp` con dueno root y el usuario
    # del contenedor no puede borrarlo. Y envuelto en try, porque fallar al
    # limpiar un temporal no puede voltear un script que ya cambio las claves.
    try { docker exec -u root $Contenedor rm -f /tmp/pass.sql 2>&1 | Out-Null } catch { }
}

Write-Host ''
foreach ($x in $usuarios) {
    Write-Host ('  {0,-12} {1}' -f $x.u, $claras[$x.u]) -ForegroundColor Green
}
Write-Host ''
Write-Host ('Escritas tambien en: {0}' -f $ruta) -ForegroundColor Cyan
Write-Host ''
Write-Host 'Falta, y es lo que rompe la entrega si no se hace:' -ForegroundColor Yellow
Write-Host '  1. Poner estas credenciales en el documento de la entrega.'
Write-Host '  2. Ponerlas en el mensaje a la catedra.'
Write-Host '  3. Borrar el archivo de arriba.'
Write-Host '  4. Si cambiaste las del sistema publicado, proba entrar antes de mandar nada.'
