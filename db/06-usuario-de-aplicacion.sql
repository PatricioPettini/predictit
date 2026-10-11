/*
    El usuario con el que se conecta la aplicación.

    Hasta acá la API entraba como `sa`, que es administrador de todo el motor:
    puede leer y escribir cualquier base, crear logins, apagar el servidor y
    leer las credenciales cifradas de cualquier organización. Una inyección de
    SQL o una cadena de conexión filtrada valían el servidor entero y no una
    base. Es el A05 —Security Misconfiguration— de la revisión OWASP.

    `predictit_app` no tiene ningún rol de servidor: no existe fuera de las dos
    bases del sistema. Adentro de cada una es `db_owner`, y no `db_datareader`
    + `db_datawriter`, por una razón concreta: el servicio de respaldos hace
    `BACKUP DATABASE`, `RESTORE DATABASE ... WITH REPLACE` y `ALTER DATABASE ...
    SET SINGLE_USER`, y las tres piden ser dueño de la base. Con permisos de
    sólo lectura y escritura el respaldo dejaba de funcionar, que es un
    requisito del sistema (Req. Arq. 003).

    La diferencia con `sa` sigue siendo la que importa: el usuario no puede
    salir de estas dos bases, ni crear logins, ni tocar la instancia.

    Es idempotente, como los demás: se puede volver a correr y sólo actualiza la
    contraseña.

    Se ejecuta con la contraseña como variable, nunca escrita acá:

        sqlcmd ... -v APP_PASSWORD="..." -i 06-usuario-de-aplicacion.sql
*/
SET NOCOUNT ON;
:on error exit

IF '$(APP_PASSWORD)' IN ('', 'REEMPLAZAR')
BEGIN
    RAISERROR('Falta APP_PASSWORD: pasala con -v APP_PASSWORD="..."', 16, 1);
    SET NOEXEC ON;
END
GO

USE master;
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'predictit_app')
BEGIN
    CREATE LOGIN predictit_app
        WITH PASSWORD = '$(APP_PASSWORD)',
             CHECK_POLICY = ON,
             DEFAULT_DATABASE = PredictIT_Negocio;
    PRINT 'Login predictit_app creado.';
END
ELSE
BEGIN
    ALTER LOGIN predictit_app WITH PASSWORD = '$(APP_PASSWORD)';
    PRINT 'Login predictit_app ya existía: se actualizó la contraseña.';
END
GO

/*
    Un usuario por base, los dos sobre el mismo login. La separación de las dos
    bases la sigue haciendo la aplicación con dos cadenas de conexión distintas;
    esto es lo que impide que cualquiera de las dos alcance una tercera base.
*/
USE PredictIT_Negocio;
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'predictit_app')
    CREATE USER predictit_app FOR LOGIN predictit_app;
ALTER ROLE db_owner ADD MEMBER predictit_app;
GO

USE PredictIT_Servicio;
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'predictit_app')
    CREATE USER predictit_app FOR LOGIN predictit_app;
ALTER ROLE db_owner ADD MEMBER predictit_app;
GO

/*
    Y `dbcreator`, que es el único rol de servidor que se le da.

    Restaurar es una operación de servidor y no de base: SQL Server la trata
    como crear la base de nuevo, así que pide `CREATE DATABASE` en `master`
    aunque la base ya exista y se restaure con `WITH REPLACE`. Se comprobó:
    como `db_owner` el respaldo funciona y hasta `RESTORE VERIFYONLY` falla con
    «CREATE DATABASE permission denied».

    Qué habilita de más: crear y borrar bases en la instancia. Qué sigue sin
    poder hacer, y es lo que importa frente a `sa`: leer o escribir en una base
    ajena que no haya creado, crear o modificar logins, ver las credenciales
    cifradas de otra aplicación, cambiar la configuración del motor o apagarlo.

    La alternativa era sacar la restauración de la aplicación y dejarla como
    tarea del administrador de base. No se hizo porque el respaldo y su
    restauración son un requisito del sistema (Req. Arq. 003) y se demuestran
    en la defensa; el costo queda anotado en la revisión OWASP.
*/
USE master;
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'predictit_app')
    CREATE USER predictit_app FOR LOGIN predictit_app;
ALTER SERVER ROLE dbcreator ADD MEMBER predictit_app;
GO

PRINT 'Usuario de aplicación listo. La API ya no necesita entrar como sa.';
GO
