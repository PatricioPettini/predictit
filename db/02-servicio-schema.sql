/* =========================================================================
   PredictIT — Base de SERVICIO (seguridad, bitácora, idioma, respaldos)
   Sistema de mantenimiento predictivo de equipos informáticos (TFI - UAI)

   Corresponde al "DER SERVICIO" de la sección 10.4.7 del documento.
   Está deliberadamente separada de PredictIT_Negocio: ningún DAO de negocio
   puede leerla, y viceversa. Las referencias a Usuario desde el negocio son
   lógicas (mismo GUID), nunca claves foráneas: son bases distintas.

   Idempotente: se puede correr las veces que haga falta.
   ========================================================================= */

USE [master];
GO
IF DB_ID(N'PredictIT_Servicio') IS NULL CREATE DATABASE [PredictIT_Servicio];
GO
USE [PredictIT_Servicio];
GO

/* ----------------------------------------------------------------------
   Multiidioma (Req. Arq. 001)
   ---------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Idioma', N'U') IS NULL
CREATE TABLE dbo.Idioma (
    id_idioma  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Idioma PRIMARY KEY,
    codigo     VARCHAR(10)      NOT NULL CONSTRAINT UQ_Idioma_codigo UNIQUE,  -- es-AR, en-US
    nombre     VARCHAR(60)      NOT NULL,
    activo     BIT              NOT NULL CONSTRAINT DF_Idioma_activo DEFAULT 1,
    es_default BIT              NOT NULL CONSTRAINT DF_Idioma_default DEFAULT 0
);
GO

IF OBJECT_ID(N'dbo.Traduccion', N'U') IS NULL
CREATE TABLE dbo.Traduccion (
    id_traduccion UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Traduccion PRIMARY KEY,
    id_idioma     UNIQUEIDENTIFIER NOT NULL,
    clave         VARCHAR(150)     NOT NULL,
    texto         VARCHAR(1000)    NOT NULL,
    CONSTRAINT FK_Traduccion_Idioma FOREIGN KEY (id_idioma) REFERENCES dbo.Idioma(id_idioma)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Traduccion_idioma_clave')
CREATE UNIQUE NONCLUSTERED INDEX UX_Traduccion_idioma_clave ON dbo.Traduccion(id_idioma, clave);
GO

/* ----------------------------------------------------------------------
   Permisos: Patente / Familia (patrón Composite)

   Patente = permiso atómico (hoja).
   Familia = agrupación (compuesto), y puede contener otras familias.
   Rol     = lo que se le asigna al usuario; agrupa familias y patentes sueltas.
   ---------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Patente', N'U') IS NULL
CREATE TABLE dbo.Patente (
    id_patente  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Patente PRIMARY KEY,
    nombre      VARCHAR(100)     NOT NULL,
    -- Clave estable que se usa en el código: [RequierePatente("EQUIPO_GESTIONAR")].
    data_key    VARCHAR(60)      NOT NULL CONSTRAINT UQ_Patente_data_key UNIQUE,
    descripcion VARCHAR(255)     NULL,
    modulo      VARCHAR(40)      NULL,
    -- Nivel de acceso que otorga la patente: 1 lectura, 2 escritura,
    -- 3 administración. El modelo de referencia declara el atributo y lo deja
    -- en 1 para todas; acá se puebla, así se puede explicar el permiso por
    -- nivel y no sólo enumerarlo.
    tipo_acceso INT              NOT NULL CONSTRAINT DF_Patente_tipo_acceso DEFAULT 1,
    CONSTRAINT CK_Patente_tipo_acceso CHECK (tipo_acceso IN (1,2,3))
);

-- La columna se agrega por separado para las bases que ya existían. Va con EXEC
-- a propósito: SQL Server compila el lote entero antes de ejecutarlo, así que un
-- CHECK sobre la columna recién agregada falla al compilar aunque el ALTER que la
-- crea esté unas líneas más arriba.
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.Patente') AND name = N'tipo_acceso')
BEGIN
    EXEC(N'ALTER TABLE dbo.Patente ADD tipo_acceso INT NOT NULL
               CONSTRAINT DF_Patente_tipo_acceso DEFAULT 1');
    EXEC(N'ALTER TABLE dbo.Patente ADD CONSTRAINT CK_Patente_tipo_acceso
               CHECK (tipo_acceso IN (1,2,3))');
END;
GO

IF OBJECT_ID(N'dbo.Familia', N'U') IS NULL
CREATE TABLE dbo.Familia (
    id_familia  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Familia PRIMARY KEY,
    nombre      VARCHAR(100)     NOT NULL CONSTRAINT UQ_Familia_nombre UNIQUE,
    descripcion VARCHAR(255)     NULL
);
GO

IF OBJECT_ID(N'dbo.Familia_Patente', N'U') IS NULL
CREATE TABLE dbo.Familia_Patente (
    id_familia UNIQUEIDENTIFIER NOT NULL,
    id_patente UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT PK_Familia_Patente PRIMARY KEY (id_familia, id_patente),
    CONSTRAINT FK_FamPat_Familia FOREIGN KEY (id_familia) REFERENCES dbo.Familia(id_familia),
    CONSTRAINT FK_FamPat_Patente FOREIGN KEY (id_patente) REFERENCES dbo.Patente(id_patente)
);
GO

/* Composición anidada de familias. La recursión la resuelve el SeguridadService,
   que además corta ciclos: nada impide a nivel de tabla armar A -> B -> A. */
IF OBJECT_ID(N'dbo.Familia_Familia', N'U') IS NULL
CREATE TABLE dbo.Familia_Familia (
    id_familia      UNIQUEIDENTIFIER NOT NULL,
    id_familia_hijo UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT PK_Familia_Familia PRIMARY KEY (id_familia, id_familia_hijo),
    CONSTRAINT FK_FamFam_Padre FOREIGN KEY (id_familia)      REFERENCES dbo.Familia(id_familia),
    CONSTRAINT FK_FamFam_Hijo  FOREIGN KEY (id_familia_hijo) REFERENCES dbo.Familia(id_familia),
    CONSTRAINT CK_FamFam_no_self CHECK (id_familia <> id_familia_hijo)
);
GO

IF OBJECT_ID(N'dbo.Rol', N'U') IS NULL
CREATE TABLE dbo.Rol (
    id_rol      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Rol PRIMARY KEY,
    nombre      VARCHAR(60)      NOT NULL CONSTRAINT UQ_Rol_nombre UNIQUE,
    descripcion VARCHAR(255)     NULL,
    -- Los roles del documento (10.3.2.4.1) no se pueden borrar desde la UI.
    es_sistema  BIT              NOT NULL CONSTRAINT DF_Rol_sistema DEFAULT 0
);
GO

IF OBJECT_ID(N'dbo.Rol_Familia', N'U') IS NULL
CREATE TABLE dbo.Rol_Familia (
    id_rol     UNIQUEIDENTIFIER NOT NULL,
    id_familia UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT PK_Rol_Familia PRIMARY KEY (id_rol, id_familia),
    CONSTRAINT FK_RolFam_Rol     FOREIGN KEY (id_rol)     REFERENCES dbo.Rol(id_rol),
    CONSTRAINT FK_RolFam_Familia FOREIGN KEY (id_familia) REFERENCES dbo.Familia(id_familia)
);
GO

IF OBJECT_ID(N'dbo.Rol_Patente', N'U') IS NULL
CREATE TABLE dbo.Rol_Patente (
    id_rol     UNIQUEIDENTIFIER NOT NULL,
    id_patente UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT PK_Rol_Patente PRIMARY KEY (id_rol, id_patente),
    CONSTRAINT FK_RolPat_Rol     FOREIGN KEY (id_rol)     REFERENCES dbo.Rol(id_rol),
    CONSTRAINT FK_RolPat_Patente FOREIGN KEY (id_patente) REFERENCES dbo.Patente(id_patente)
);
GO

/* ----------------------------------------------------------------------
   Usuarios
   ---------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Usuario', N'U') IS NULL
CREATE TABLE dbo.Usuario (
    id_usuario       UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Usuario PRIMARY KEY,
    nombre           VARCHAR(100)     NOT NULL,
    apellido         VARCHAR(100)     NOT NULL,
    email            VARCHAR(150)     NOT NULL,
    username         VARCHAR(60)      NOT NULL,
    -- Formato PBKDF2$<iteraciones>$<salt b64>$<hash b64>. Nunca texto plano.
    [password]       VARCHAR(300)     NOT NULL,
    telefono         VARCHAR(50)      NULL,
    activo           BIT              NOT NULL CONSTRAINT DF_Usuario_activo DEFAULT 1,
    fecha_alta       DATETIME2(0)     NOT NULL CONSTRAINT DF_Usuario_fecha DEFAULT SYSDATETIME(),
    -- Organización a la que pertenece. Referencia lógica a PredictIT_Negocio.
    -- NULL en el perfil Partner, que trabaja sobre varias (Usuario_Organizacion).
    id_organizacion  UNIQUEIDENTIFIER NULL,
    id_idioma        UNIQUEIDENTIFIER NULL,
    intentos_fallidos INT             NOT NULL CONSTRAINT DF_Usuario_intentos DEFAULT 0,
    bloqueado        BIT              NOT NULL CONSTRAINT DF_Usuario_bloqueado DEFAULT 0,
    ultimo_acceso    DATETIME2(0)     NULL,
    codigo_restablecimiento    VARCHAR(100) NULL,
    caducacion_restablecimiento DATETIME2(0) NULL,
    CONSTRAINT FK_Usuario_Idioma FOREIGN KEY (id_idioma) REFERENCES dbo.Idioma(id_idioma)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Usuario_email')
CREATE UNIQUE NONCLUSTERED INDEX UX_Usuario_email ON dbo.Usuario(email);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Usuario_username')
CREATE UNIQUE NONCLUSTERED INDEX UX_Usuario_username ON dbo.Usuario(username);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Usuario_organizacion')
CREATE NONCLUSTERED INDEX IX_Usuario_organizacion ON dbo.Usuario(id_organizacion) WHERE id_organizacion IS NOT NULL;
GO

IF OBJECT_ID(N'dbo.Usuario_Rol', N'U') IS NULL
CREATE TABLE dbo.Usuario_Rol (
    id_usuario UNIQUEIDENTIFIER NOT NULL,
    id_rol     UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT PK_Usuario_Rol PRIMARY KEY (id_usuario, id_rol),
    CONSTRAINT FK_UsuRol_Usuario FOREIGN KEY (id_usuario) REFERENCES dbo.Usuario(id_usuario),
    CONSTRAINT FK_UsuRol_Rol     FOREIGN KEY (id_rol)     REFERENCES dbo.Rol(id_rol)
);
GO

/* Perfil Partner (Segmento C): un mismo usuario gestiona el parque de varias
   PyMEs. El listado de organizaciones habilitadas vive acá. */
IF OBJECT_ID(N'dbo.Usuario_Organizacion', N'U') IS NULL
CREATE TABLE dbo.Usuario_Organizacion (
    id_usuario      UNIQUEIDENTIFIER NOT NULL,
    id_organizacion UNIQUEIDENTIFIER NOT NULL,   -- referencia lógica a PredictIT_Negocio
    CONSTRAINT PK_Usuario_Organizacion PRIMARY KEY (id_usuario, id_organizacion),
    CONSTRAINT FK_UsuOrg_Usuario FOREIGN KEY (id_usuario) REFERENCES dbo.Usuario(id_usuario)
);
GO

/* ----------------------------------------------------------------------
   Bitácora (Req. Arq. 002)
   ---------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.TipoEvento', N'U') IS NULL
CREATE TABLE dbo.TipoEvento (
    id_tipo_evento UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TipoEvento PRIMARY KEY,
    codigo         VARCHAR(50)      NOT NULL CONSTRAINT UQ_TipoEvento_codigo UNIQUE,
    nombre         VARCHAR(100)     NOT NULL,
    descripcion    VARCHAR(255)     NULL,
    criticidad     VARCHAR(15)      NOT NULL CONSTRAINT DF_TipoEvento_crit DEFAULT 'INFO',
    CONSTRAINT CK_TipoEvento_criticidad CHECK (criticidad IN ('INFO','ADVERTENCIA','ERROR','CRITICO'))
);
GO
-- La primera version tenia VARCHAR(10) y 'ADVERTENCIA' no entraba.
IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.TipoEvento') AND name = N'criticidad' AND max_length < 15)
    ALTER TABLE dbo.TipoEvento ALTER COLUMN criticidad VARCHAR(15) NOT NULL;
GO

IF OBJECT_ID(N'dbo.Bitacora', N'U') IS NULL
CREATE TABLE dbo.Bitacora (
    id_bitacora     UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Bitacora PRIMARY KEY,
    id_usuario      UNIQUEIDENTIFIER NULL,      -- NULL en eventos del sistema o login fallido
    usuario_texto   VARCHAR(100)     NULL,      -- lo tecleado en un login fallido
    id_tipo_evento  UNIQUEIDENTIFIER NOT NULL,
    fecha           DATETIME2(0)     NOT NULL CONSTRAINT DF_Bitacora_fecha DEFAULT SYSDATETIME(),
    descripcion     VARCHAR(1000)    NOT NULL,
    entidad         VARCHAR(100)     NULL,
    id_entidad      UNIQUEIDENTIFIER NULL,
    id_organizacion UNIQUEIDENTIFIER NULL,
    ip              VARCHAR(45)      NULL,
    traza           VARCHAR(MAX)     NULL,      -- stack trace de excepciones no controladas
    CONSTRAINT FK_Bitacora_Usuario    FOREIGN KEY (id_usuario)     REFERENCES dbo.Usuario(id_usuario),
    CONSTRAINT FK_Bitacora_TipoEvento FOREIGN KEY (id_tipo_evento) REFERENCES dbo.TipoEvento(id_tipo_evento)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Bitacora_fecha')
CREATE NONCLUSTERED INDEX IX_Bitacora_fecha ON dbo.Bitacora(fecha DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Bitacora_org_tipo')
CREATE NONCLUSTERED INDEX IX_Bitacora_org_tipo ON dbo.Bitacora(id_organizacion, id_tipo_evento, fecha DESC);
GO

/* La bitácora tiene que ser inmutable (Req. Arq. 002). Se bloquea a nivel de
   base: no alcanza con no exponer el UPDATE en la aplicación. */
IF OBJECT_ID(N'dbo.TR_Bitacora_Inmutable', N'TR') IS NOT NULL DROP TRIGGER dbo.TR_Bitacora_Inmutable;
GO
CREATE TRIGGER dbo.TR_Bitacora_Inmutable ON dbo.Bitacora
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 50001, 'La bitacora es inmutable: no admite modificaciones ni borrados.', 1;
END;
GO

/* ----------------------------------------------------------------------
   Respaldos (Req. Arq. 003)
   ---------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Respaldo', N'U') IS NULL
CREATE TABLE dbo.Respaldo (
    id_respaldo      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Respaldo PRIMARY KEY,
    base           VARCHAR(60)      NOT NULL,   -- PredictIT_Negocio | PredictIT_Servicio
    nombre_archivo VARCHAR(300)     NOT NULL,
    ruta           VARCHAR(500)     NOT NULL,
    fecha          DATETIME2(0)     NOT NULL CONSTRAINT DF_Respaldo_fecha DEFAULT SYSDATETIME(),
    tamano_bytes   BIGINT           NULL,
    tipo           VARCHAR(20)      NOT NULL CONSTRAINT DF_Respaldo_tipo DEFAULT 'MANUAL',
    id_usuario     UNIQUEIDENTIFIER NULL,
    resultado      VARCHAR(20)      NOT NULL CONSTRAINT DF_Respaldo_resultado DEFAULT 'OK',
    detalle        VARCHAR(1000)    NULL,
    CONSTRAINT FK_Respaldo_Usuario FOREIGN KEY (id_usuario) REFERENCES dbo.Usuario(id_usuario),
    CONSTRAINT CK_Respaldo_tipo      CHECK (tipo IN ('MANUAL','AUTOMATICO')),
    CONSTRAINT CK_Respaldo_resultado CHECK (resultado IN ('OK','ERROR'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Respaldo_fecha')
CREATE NONCLUSTERED INDEX IX_Respaldo_fecha ON dbo.Respaldo(fecha DESC);
GO

PRINT 'PredictIT_Servicio: esquema aplicado.';
GO
