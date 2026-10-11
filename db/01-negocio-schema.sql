/* =========================================================================
   PredictIT — Base de NEGOCIO
   Sistema de mantenimiento predictivo de equipos informáticos (TFI - UAI)

   Corresponde al "DER Negocio" de la sección 10.4.7 del documento, con dos
   agregados respecto de ese diagrama (ver docs/CAMBIOS-DOCUMENTO.md):
     · D-02: entidad Organizacion, raíz del modelo multi-cliente.
     · D-04: clasificación/triage de la incidencia por IA.

   Idempotente: se puede correr las veces que haga falta.
   ========================================================================= */

USE [master];
GO
IF DB_ID(N'PredictIT_Negocio') IS NULL CREATE DATABASE [PredictIT_Negocio];
GO
USE [PredictIT_Negocio];
GO

/* ----------------------------------------------------------------------
   Plan comercial

   Los planes de la sección 5.3.6 del documento, con su abono, los equipos que
   incluyen y el precio del equipo adicional. Está en la base y no en el código
   porque la sección 6.5.3 apoya la justificación económica del precio en estos
   valores, y el documento fija revisión trimestral de la lista: el sistema tiene
   que poder mostrar sobre qué números está operando.

   "PLAN" es palabra reservada en T-SQL, de ahí el nombre.
   ---------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.PlanComercial', N'U') IS NULL
CREATE TABLE dbo.PlanComercial (
    id_plan                 UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PlanComercial PRIMARY KEY,
    codigo                  VARCHAR(20)      NOT NULL CONSTRAINT UQ_PlanComercial_codigo UNIQUE,
    nombre                  VARCHAR(60)      NOT NULL,
    abono_mensual           DECIMAL(14,2)    NOT NULL,
    equipos_incluidos       INT              NOT NULL,
    precio_equipo_adicional DECIMAL(14,2)    NOT NULL,
    soporte                 VARCHAR(150)     NULL,
    vigente_desde           DATE             NOT NULL CONSTRAINT DF_PlanComercial_vigencia DEFAULT SYSDATETIME(),
    CONSTRAINT CK_PlanComercial_codigo CHECK (codigo IN ('INICIAL','ESTANDAR','PARTNER')),
    CONSTRAINT CK_PlanComercial_equipos CHECK (equipos_incluidos > 0)
);
GO

/* ----------------------------------------------------------------------
   Organización (raíz del aislamiento multi-cliente)
   ---------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Organizacion', N'U') IS NULL
CREATE TABLE dbo.Organizacion (
    id_organizacion  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Organizacion PRIMARY KEY,
    razon_social     VARCHAR(150)     NOT NULL,
    nombre_corto     VARCHAR(60)      NOT NULL,
    cuit             VARCHAR(13)      NULL,
    id_plan          UNIQUEIDENTIFIER NULL,
    activa           BIT              NOT NULL CONSTRAINT DF_Organizacion_activa DEFAULT 1,
    fecha_alta       DATETIME2(0)     NOT NULL CONSTRAINT DF_Organizacion_fecha DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Organizacion_PlanComercial FOREIGN KEY (id_plan)
        REFERENCES dbo.PlanComercial(id_plan)
);
GO

/* El plan empezó siendo una columna de texto suelta. Pasó a ser una referencia
   cuando el documento le puso límite de equipos y precio por equipo adicional:
   con un texto no se puede calcular nada. Migración para bases ya creadas. */
IF COL_LENGTH('dbo.Organizacion', 'id_plan') IS NULL
    ALTER TABLE dbo.Organizacion ADD id_plan UNIQUEIDENTIFIER NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Organizacion_PlanComercial')
    ALTER TABLE dbo.Organizacion WITH CHECK
    ADD CONSTRAINT FK_Organizacion_PlanComercial FOREIGN KEY (id_plan)
        REFERENCES dbo.PlanComercial(id_plan);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Organizacion_cuit')
CREATE UNIQUE NONCLUSTERED INDEX UX_Organizacion_cuit
    ON dbo.Organizacion(cuit) WHERE cuit IS NOT NULL;
GO

/* ----------------------------------------------------------------------
   Catálogos globales (no dependen de la organización)
   ---------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.TipoEquipo', N'U') IS NULL
CREATE TABLE dbo.TipoEquipo (
    id_tipo_equipo UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TipoEquipo PRIMARY KEY,
    nombre         VARCHAR(50)      NOT NULL CONSTRAINT UQ_TipoEquipo_nombre UNIQUE
);
GO

IF OBJECT_ID(N'dbo.EstadoEquipo', N'U') IS NULL
CREATE TABLE dbo.EstadoEquipo (
    id_estado_equipo UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EstadoEquipo PRIMARY KEY,
    nombre           VARCHAR(50)      NOT NULL CONSTRAINT UQ_EstadoEquipo_nombre UNIQUE,
    -- Un equipo dado de baja no participa del scoring ni admite incidencias nuevas.
    operativo        BIT              NOT NULL CONSTRAINT DF_EstadoEquipo_operativo DEFAULT 1,
    orden            INT              NOT NULL CONSTRAINT DF_EstadoEquipo_orden DEFAULT 0
);
GO

IF OBJECT_ID(N'dbo.EstadoIncidencia', N'U') IS NULL
CREATE TABLE dbo.EstadoIncidencia (
    id_estado_incidencia UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EstadoIncidencia PRIMARY KEY,
    nombre               VARCHAR(50)      NOT NULL CONSTRAINT UQ_EstadoIncidencia_nombre UNIQUE,
    es_final             BIT              NOT NULL CONSTRAINT DF_EstadoIncidencia_final DEFAULT 0,
    orden                INT              NOT NULL CONSTRAINT DF_EstadoIncidencia_orden DEFAULT 0
);
GO

IF OBJECT_ID(N'dbo.PrioridadIncidencia', N'U') IS NULL
CREATE TABLE dbo.PrioridadIncidencia (
    id_prioridad UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PrioridadIncidencia PRIMARY KEY,
    nombre       VARCHAR(50)      NOT NULL CONSTRAINT UQ_PrioridadIncidencia_nombre UNIQUE,
    nivel        INT              NOT NULL   -- 1 baja .. 4 crítica; pondera el scoring
);
GO

IF OBJECT_ID(N'dbo.CategoriaIncidencia', N'U') IS NULL
CREATE TABLE dbo.CategoriaIncidencia (
    id_categoria UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CategoriaIncidencia PRIMARY KEY,
    nombre       VARCHAR(60)      NOT NULL CONSTRAINT UQ_CategoriaIncidencia_nombre UNIQUE,
    descripcion  VARCHAR(255)     NULL
);
GO

IF OBJECT_ID(N'dbo.TipoMantenimiento', N'U') IS NULL
CREATE TABLE dbo.TipoMantenimiento (
    id_tipo_mantenimiento UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TipoMantenimiento PRIMARY KEY,
    nombre                VARCHAR(50)      NOT NULL CONSTRAINT UQ_TipoMantenimiento_nombre UNIQUE
);
GO

IF OBJECT_ID(N'dbo.EstadoAlerta', N'U') IS NULL
CREATE TABLE dbo.EstadoAlerta (
    id_estado_alerta UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EstadoAlerta PRIMARY KEY,
    nombre           VARCHAR(50)      NOT NULL CONSTRAINT UQ_EstadoAlerta_nombre UNIQUE,
    es_final         BIT              NOT NULL CONSTRAINT DF_EstadoAlerta_final DEFAULT 0
);
GO

IF OBJECT_ID(N'dbo.Especialidad', N'U') IS NULL
CREATE TABLE dbo.Especialidad (
    id_especialidad UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Especialidad PRIMARY KEY,
    nombre          VARCHAR(60)      NOT NULL CONSTRAINT UQ_Especialidad_nombre UNIQUE
);
GO

/* Tres columnas que el motor predictivo y la asignacion necesitan y que la
   primera version del esquema no tenia. Migracion con guarda para bases ya
   creadas.

   - TipoMantenimiento.es_preventivo: la regla MANTENIMIENTO_VENCIDO cuenta dias
     desde el ultimo mantenimiento *preventivo*. Un correctivo es una reparacion:
     no reinicia el contador, y contarlo dejaria de avisar justo en el equipo que
     mas se rompe.
   - CategoriaIncidencia.id_especialidad: sin esto la asignacion no sabe que
     especialidad resuelve la categoria de la incidencia, que es el dato central
     del contexto que se le manda al proveedor.
   - PrioridadIncidencia.horas_objetivo: el objetivo de resolucion pactado.
     Nullable a proposito: una prioridad sin objetivo no incumple nada. */
IF COL_LENGTH('dbo.TipoMantenimiento', 'es_preventivo') IS NULL
    ALTER TABLE dbo.TipoMantenimiento
        ADD es_preventivo BIT NOT NULL
            CONSTRAINT DF_TipoMantenimiento_preventivo DEFAULT 0;
GO

IF COL_LENGTH('dbo.CategoriaIncidencia', 'id_especialidad') IS NULL
    ALTER TABLE dbo.CategoriaIncidencia ADD id_especialidad UNIQUEIDENTIFIER NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CategoriaIncidencia_Especialidad')
    ALTER TABLE dbo.CategoriaIncidencia WITH CHECK
    ADD CONSTRAINT FK_CategoriaIncidencia_Especialidad FOREIGN KEY (id_especialidad)
        REFERENCES dbo.Especialidad(id_especialidad);
GO

IF COL_LENGTH('dbo.PrioridadIncidencia', 'horas_objetivo') IS NULL
    ALTER TABLE dbo.PrioridadIncidencia ADD horas_objetivo INT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PrioridadIncidencia_horas')
    ALTER TABLE dbo.PrioridadIncidencia WITH CHECK
    ADD CONSTRAINT CK_PrioridadIncidencia_horas
        CHECK (horas_objetivo IS NULL OR horas_objetivo > 0);
GO

/* ----------------------------------------------------------------------
   Activos
   ---------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Ubicacion', N'U') IS NULL
CREATE TABLE dbo.Ubicacion (
    id_ubicacion    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Ubicacion PRIMARY KEY,
    id_organizacion UNIQUEIDENTIFIER NOT NULL,
    nombre          VARCHAR(100)     NOT NULL,
    descripcion     VARCHAR(255)     NULL,
    activa          BIT              NOT NULL CONSTRAINT DF_Ubicacion_activa DEFAULT 1,
    CONSTRAINT FK_Ubicacion_Organizacion FOREIGN KEY (id_organizacion)
        REFERENCES dbo.Organizacion(id_organizacion)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Ubicacion_org_nombre')
CREATE UNIQUE NONCLUSTERED INDEX UX_Ubicacion_org_nombre
    ON dbo.Ubicacion(id_organizacion, nombre);
GO

IF OBJECT_ID(N'dbo.Equipo', N'U') IS NULL
CREATE TABLE dbo.Equipo (
    id_equipo          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Equipo PRIMARY KEY,
    id_organizacion    UNIQUEIDENTIFIER NOT NULL,
    codigo             VARCHAR(50)      NOT NULL,   -- código interno de inventario
    id_tipo_equipo     UNIQUEIDENTIFIER NOT NULL,
    marca              VARCHAR(100)     NULL,
    modelo             VARCHAR(100)     NULL,
    numero_serie       VARCHAR(100)     NULL,
    descripcion_tecnica VARCHAR(500)    NULL,
    fecha_alta         DATETIME2(0)     NOT NULL CONSTRAINT DF_Equipo_fecha_alta DEFAULT SYSDATETIME(),
    fecha_adquisicion  DATE             NULL,
    fecha_fin_garantia DATE             NULL,
    proveedor          VARCHAR(150)     NULL,
    -- 1 baja .. 4 crítica. Pondera el scoring de riesgo: la misma cantidad de
    -- fallas pesa distinto en el servidor que en un monitor de recepción.
    criticidad         INT              NOT NULL CONSTRAINT DF_Equipo_criticidad DEFAULT 2,
    id_estado_equipo   UNIQUEIDENTIFIER NOT NULL,
    id_ubicacion       UNIQUEIDENTIFIER NULL,
    -- Referencia lógica a Usuario en PredictIT_Servicio (sin FK: otra base).
    id_responsable     UNIQUEIDENTIFIER NULL,
    CONSTRAINT FK_Equipo_Organizacion FOREIGN KEY (id_organizacion) REFERENCES dbo.Organizacion(id_organizacion),
    CONSTRAINT FK_Equipo_TipoEquipo   FOREIGN KEY (id_tipo_equipo)  REFERENCES dbo.TipoEquipo(id_tipo_equipo),
    CONSTRAINT FK_Equipo_EstadoEquipo FOREIGN KEY (id_estado_equipo) REFERENCES dbo.EstadoEquipo(id_estado_equipo),
    CONSTRAINT FK_Equipo_Ubicacion    FOREIGN KEY (id_ubicacion)    REFERENCES dbo.Ubicacion(id_ubicacion),
    CONSTRAINT CK_Equipo_criticidad   CHECK (criticidad BETWEEN 1 AND 4)
);
GO
-- RF-04: unicidad del código de inventario y del número de serie, por organización.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Equipo_org_codigo')
CREATE UNIQUE NONCLUSTERED INDEX UX_Equipo_org_codigo ON dbo.Equipo(id_organizacion, codigo);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Equipo_org_serie')
CREATE UNIQUE NONCLUSTERED INDEX UX_Equipo_org_serie
    ON dbo.Equipo(id_organizacion, numero_serie) WHERE numero_serie IS NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Equipo_org_estado')
CREATE NONCLUSTERED INDEX IX_Equipo_org_estado ON dbo.Equipo(id_organizacion, id_estado_equipo);
GO

/* CU-005: el cambio de estado deja rastro, no se pisa el valor anterior. */
IF OBJECT_ID(N'dbo.HistorialEstadoEquipo', N'U') IS NULL
CREATE TABLE dbo.HistorialEstadoEquipo (
    id_historial       UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_HistorialEstadoEquipo PRIMARY KEY,
    id_equipo          UNIQUEIDENTIFIER NOT NULL,
    id_estado_anterior UNIQUEIDENTIFIER NULL,
    id_estado_nuevo    UNIQUEIDENTIFIER NOT NULL,
    motivo             VARCHAR(500)     NULL,
    fecha              DATETIME2(0)     NOT NULL CONSTRAINT DF_HistEstadoEquipo_fecha DEFAULT SYSDATETIME(),
    id_usuario         UNIQUEIDENTIFIER NULL,
    CONSTRAINT FK_HistEstadoEquipo_Equipo   FOREIGN KEY (id_equipo)          REFERENCES dbo.Equipo(id_equipo),
    CONSTRAINT FK_HistEstadoEquipo_Anterior FOREIGN KEY (id_estado_anterior) REFERENCES dbo.EstadoEquipo(id_estado_equipo),
    CONSTRAINT FK_HistEstadoEquipo_Nuevo    FOREIGN KEY (id_estado_nuevo)    REFERENCES dbo.EstadoEquipo(id_estado_equipo)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_HistEstadoEquipo_equipo')
CREATE NONCLUSTERED INDEX IX_HistEstadoEquipo_equipo ON dbo.HistorialEstadoEquipo(id_equipo, fecha DESC);
GO

/* ----------------------------------------------------------------------
   Técnicos y especialidades (contexto de la asignación)
   ---------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.TecnicoEspecialidad', N'U') IS NULL
CREATE TABLE dbo.TecnicoEspecialidad (
    id_organizacion UNIQUEIDENTIFIER NOT NULL,
    id_tecnico      UNIQUEIDENTIFIER NOT NULL,   -- Usuario de PredictIT_Servicio
    id_especialidad UNIQUEIDENTIFIER NOT NULL,
    nivel           INT              NOT NULL CONSTRAINT DF_TecnicoEsp_nivel DEFAULT 2,
    CONSTRAINT PK_TecnicoEspecialidad PRIMARY KEY (id_organizacion, id_tecnico, id_especialidad),
    CONSTRAINT FK_TecnicoEsp_Organizacion FOREIGN KEY (id_organizacion) REFERENCES dbo.Organizacion(id_organizacion),
    CONSTRAINT FK_TecnicoEsp_Especialidad FOREIGN KEY (id_especialidad) REFERENCES dbo.Especialidad(id_especialidad),
    CONSTRAINT CK_TecnicoEsp_nivel CHECK (nivel BETWEEN 1 AND 3)
);
GO

/* ----------------------------------------------------------------------
   Incidencias
   ---------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Incidencia', N'U') IS NULL
CREATE TABLE dbo.Incidencia (
    id_incidencia        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Incidencia PRIMARY KEY,
    id_organizacion      UNIQUEIDENTIFIER NOT NULL,
    numero               INT              NOT NULL,   -- correlativo visible, por organización
    titulo               VARCHAR(150)     NOT NULL,
    descripcion          VARCHAR(2000)    NOT NULL,
    fecha                DATETIME2(0)     NOT NULL CONSTRAINT DF_Incidencia_fecha DEFAULT SYSDATETIME(),
    fecha_resolucion     DATETIME2(0)     NULL,
    id_equipo            UNIQUEIDENTIFIER NOT NULL,
    id_estado_incidencia UNIQUEIDENTIFIER NOT NULL,
    id_prioridad         UNIQUEIDENTIFIER NOT NULL,
    id_categoria         UNIQUEIDENTIFIER NULL,
    id_tecnico           UNIQUEIDENTIFIER NULL,       -- Usuario de PredictIT_Servicio
    id_usuario_reportante UNIQUEIDENTIFIER NOT NULL,  -- Usuario de PredictIT_Servicio
    -- PENDIENTE mientras no se asignó; IA / RESPALDO / MANUAL una vez asignada.
    tipo_asignacion      VARCHAR(20)      NOT NULL CONSTRAINT DF_Incidencia_tipo_asig DEFAULT 'PENDIENTE',
    -- CP-14: la asignación de respaldo marca la incidencia para que un humano la revise.
    pendiente_revision   BIT              NOT NULL CONSTRAINT DF_Incidencia_pend_rev DEFAULT 0,
    diagnostico          VARCHAR(2000)    NULL,
    solucion             VARCHAR(2000)    NULL,
    CONSTRAINT FK_Incidencia_Organizacion FOREIGN KEY (id_organizacion)      REFERENCES dbo.Organizacion(id_organizacion),
    CONSTRAINT FK_Incidencia_Equipo       FOREIGN KEY (id_equipo)            REFERENCES dbo.Equipo(id_equipo),
    CONSTRAINT FK_Incidencia_Estado       FOREIGN KEY (id_estado_incidencia) REFERENCES dbo.EstadoIncidencia(id_estado_incidencia),
    CONSTRAINT FK_Incidencia_Prioridad    FOREIGN KEY (id_prioridad)         REFERENCES dbo.PrioridadIncidencia(id_prioridad),
    CONSTRAINT FK_Incidencia_Categoria    FOREIGN KEY (id_categoria)         REFERENCES dbo.CategoriaIncidencia(id_categoria),
    CONSTRAINT CK_Incidencia_tipo_asig    CHECK (tipo_asignacion IN ('PENDIENTE','IA','HEURISTICA','RESPALDO','MANUAL'))
);
GO

/* Mismo motivo que en RecomendacionAsignacion: cuando la asignacion la resuelve
   el proveedor simulado, la decidieron heuristicas y no un modelo. Registrarla
   como IA falsearia la trazabilidad. Migracion para bases ya creadas. */
IF EXISTS (SELECT 1 FROM sys.check_constraints
           WHERE name = N'CK_Incidencia_tipo_asig'
             AND definition NOT LIKE '%HEURISTICA%')
BEGIN
    ALTER TABLE dbo.Incidencia DROP CONSTRAINT CK_Incidencia_tipo_asig;
    ALTER TABLE dbo.Incidencia WITH CHECK
        ADD CONSTRAINT CK_Incidencia_tipo_asig
            CHECK (tipo_asignacion IN ('PENDIENTE','IA','HEURISTICA','RESPALDO','MANUAL'));
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Incidencia_org_numero')
CREATE UNIQUE NONCLUSTERED INDEX UX_Incidencia_org_numero ON dbo.Incidencia(id_organizacion, numero);
GO
-- El motor predictivo consulta siempre por equipo y ventana temporal.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Incidencia_equipo_fecha')
CREATE NONCLUSTERED INDEX IX_Incidencia_equipo_fecha ON dbo.Incidencia(id_equipo, fecha DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Incidencia_tecnico_estado')
CREATE NONCLUSTERED INDEX IX_Incidencia_tecnico_estado ON dbo.Incidencia(id_tecnico, id_estado_incidencia);
GO

/* D-04: resultado del triage. Es una sugerencia, no pisa lo que decide el técnico. */
IF OBJECT_ID(N'dbo.ClasificacionIncidencia', N'U') IS NULL
CREATE TABLE dbo.ClasificacionIncidencia (
    id_clasificacion       UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ClasificacionIncidencia PRIMARY KEY,
    id_incidencia          UNIQUEIDENTIFIER NOT NULL,
    id_categoria_sugerida  UNIQUEIDENTIFIER NULL,
    id_prioridad_sugerida  UNIQUEIDENTIFIER NULL,
    id_incidencia_duplicada UNIQUEIDENTIFIER NULL,
    confianza              DECIMAL(4,3)     NULL,     -- 0.000 a 1.000
    origen                 VARCHAR(20)      NOT NULL, -- HEURISTICA | IA
    justificacion          VARCHAR(1000)    NULL,
    fecha                  DATETIME2(0)     NOT NULL CONSTRAINT DF_Clasificacion_fecha DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Clasificacion_Incidencia FOREIGN KEY (id_incidencia)           REFERENCES dbo.Incidencia(id_incidencia),
    CONSTRAINT FK_Clasificacion_Categoria  FOREIGN KEY (id_categoria_sugerida)   REFERENCES dbo.CategoriaIncidencia(id_categoria),
    CONSTRAINT FK_Clasificacion_Prioridad  FOREIGN KEY (id_prioridad_sugerida)   REFERENCES dbo.PrioridadIncidencia(id_prioridad),
    CONSTRAINT FK_Clasificacion_Duplicada  FOREIGN KEY (id_incidencia_duplicada) REFERENCES dbo.Incidencia(id_incidencia),
    CONSTRAINT CK_Clasificacion_origen     CHECK (origen IN ('HEURISTICA','IA')),
    CONSTRAINT CK_Clasificacion_confianza  CHECK (confianza IS NULL OR (confianza >= 0 AND confianza <= 1))
);
GO

/* CU-007: qué recomendó el servicio de IA y con qué contexto. Es la evidencia
   que permite defender por qué el sistema asignó a quien asignó. */
IF OBJECT_ID(N'dbo.RecomendacionAsignacion', N'U') IS NULL
CREATE TABLE dbo.RecomendacionAsignacion (
    id_recomendacion   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RecomendacionAsignacion PRIMARY KEY,
    id_incidencia      UNIQUEIDENTIFIER NOT NULL,
    id_tecnico_sugerido UNIQUEIDENTIFIER NULL,
    justificacion      VARCHAR(1000)    NULL,
    contexto_enviado   VARCHAR(MAX)     NULL,     -- JSON exacto que se envió al proveedor
    origen             VARCHAR(20)      NOT NULL, -- IA | RESPALDO
    motivo_respaldo    VARCHAR(500)     NULL,     -- por qué no respondió la IA
    fecha_hora         DATETIME2(0)     NOT NULL CONSTRAINT DF_RecomAsig_fecha DEFAULT SYSDATETIME(),
    CONSTRAINT FK_RecomAsig_Incidencia FOREIGN KEY (id_incidencia) REFERENCES dbo.Incidencia(id_incidencia),
    CONSTRAINT CK_RecomAsig_origen     CHECK (origen IN ('IA','RESPALDO','HEURISTICA'))
);
GO

/* El origen empezo admitiendo solo IA y RESPALDO. Falta HEURISTICA: cuando
   resuelve el proveedor simulado, la decision NO la tomo un modelo, y
   registrarla como IA seria falsear la trazabilidad de la asignacion, que es
   justamente lo que el sistema tiene que poder demostrar. Migracion para bases
   ya creadas. */
IF EXISTS (SELECT 1 FROM sys.check_constraints
           WHERE name = N'CK_RecomAsig_origen'
             AND definition NOT LIKE '%HEURISTICA%')
BEGIN
    ALTER TABLE dbo.RecomendacionAsignacion DROP CONSTRAINT CK_RecomAsig_origen;
    ALTER TABLE dbo.RecomendacionAsignacion WITH CHECK
        ADD CONSTRAINT CK_RecomAsig_origen
            CHECK (origen IN ('IA','RESPALDO','HEURISTICA'));
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RecomAsig_incidencia')
CREATE NONCLUSTERED INDEX IX_RecomAsig_incidencia ON dbo.RecomendacionAsignacion(id_incidencia, fecha_hora DESC);
GO

/* ----------------------------------------------------------------------
   Mantenimientos
   ---------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Mantenimiento', N'U') IS NULL
CREATE TABLE dbo.Mantenimiento (
    id_mantenimiento      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Mantenimiento PRIMARY KEY,
    id_organizacion       UNIQUEIDENTIFIER NOT NULL,
    id_equipo             UNIQUEIDENTIFIER NOT NULL,
    id_tipo_mantenimiento UNIQUEIDENTIFIER NOT NULL,
    id_tecnico            UNIQUEIDENTIFIER NOT NULL,  -- Usuario de PredictIT_Servicio
    id_incidencia         UNIQUEIDENTIFIER NULL,      -- correctivo originado en una incidencia
    fecha                 DATETIME2(0)     NOT NULL CONSTRAINT DF_Mantenimiento_fecha DEFAULT SYSDATETIME(),
    descripcion           VARCHAR(2000)    NOT NULL,
    resultado             VARCHAR(2000)    NULL,
    repuestos             VARCHAR(1000)    NULL,
    costo                 DECIMAL(14,2)    NULL,
    observaciones         VARCHAR(1000)    NULL,
    CONSTRAINT FK_Mantenimiento_Organizacion FOREIGN KEY (id_organizacion)       REFERENCES dbo.Organizacion(id_organizacion),
    CONSTRAINT FK_Mantenimiento_Equipo       FOREIGN KEY (id_equipo)             REFERENCES dbo.Equipo(id_equipo),
    CONSTRAINT FK_Mantenimiento_Tipo         FOREIGN KEY (id_tipo_mantenimiento) REFERENCES dbo.TipoMantenimiento(id_tipo_mantenimiento),
    CONSTRAINT FK_Mantenimiento_Incidencia   FOREIGN KEY (id_incidencia)         REFERENCES dbo.Incidencia(id_incidencia)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Mantenimiento_equipo_fecha')
CREATE NONCLUSTERED INDEX IX_Mantenimiento_equipo_fecha ON dbo.Mantenimiento(id_equipo, fecha DESC);
GO

/* ----------------------------------------------------------------------
   Mantenimiento programado

   Hasta acá el sistema sólo registraba lo que YA se había hecho, y el motor
   predictivo recomendaba «Programar el mantenimiento preventivo» sin que
   hubiera con qué programarlo. Estas dos tablas cierran ese bucle.

   El plan dice cada cuánto le toca a un equipo —o a todos los de un tipo— y
   de ahí salen las fechas concretas. Con el plan, la regla «Mantenimiento
   vencido» deja de medir contra un umbral suelto y pasa a medir contra lo que
   la organización se comprometió a hacer, que es lo que «vencido» significa.
   ---------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.PlanMantenimiento', N'U') IS NULL
CREATE TABLE dbo.PlanMantenimiento (
    id_plan               UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PlanMantenimiento PRIMARY KEY,
    id_organizacion       UNIQUEIDENTIFIER NOT NULL,
    -- Alcance: un equipo puntual o todos los de un tipo. Exactamente uno de los
    -- dos, y lo garantiza el CHECK: un plan sin alcance no se puede aplicar, y
    -- uno con los dos no se sabe cuál manda.
    id_equipo             UNIQUEIDENTIFIER NULL,
    id_tipo_equipo        UNIQUEIDENTIFIER NULL,
    id_tipo_mantenimiento UNIQUEIDENTIFIER NOT NULL,
    cada_dias             INT              NOT NULL,
    activo                BIT              NOT NULL CONSTRAINT DF_PlanMant_activo DEFAULT 1,
    descripcion           VARCHAR(500)     NULL,
    fecha_alta            DATETIME2(0)     NOT NULL CONSTRAINT DF_PlanMant_alta DEFAULT SYSDATETIME(),
    CONSTRAINT FK_PlanMant_Organizacion FOREIGN KEY (id_organizacion)       REFERENCES dbo.Organizacion(id_organizacion),
    CONSTRAINT FK_PlanMant_Equipo       FOREIGN KEY (id_equipo)             REFERENCES dbo.Equipo(id_equipo),
    CONSTRAINT FK_PlanMant_TipoEquipo   FOREIGN KEY (id_tipo_equipo)        REFERENCES dbo.TipoEquipo(id_tipo_equipo),
    CONSTRAINT FK_PlanMant_Tipo         FOREIGN KEY (id_tipo_mantenimiento) REFERENCES dbo.TipoMantenimiento(id_tipo_mantenimiento),
    -- Exactamente uno de los dos. T-SQL no compara expresiones booleanas
    -- entre si, asi que se cuentan con CASE.
    CONSTRAINT CK_PlanMant_alcance CHECK (
        (CASE WHEN id_equipo IS NULL THEN 0 ELSE 1 END)
      + (CASE WHEN id_tipo_equipo IS NULL THEN 0 ELSE 1 END) = 1),
    CONSTRAINT CK_PlanMant_cada_dias CHECK (cada_dias BETWEEN 7 AND 3650)
);
GO

IF OBJECT_ID(N'dbo.MantenimientoProgramado', N'U') IS NULL
CREATE TABLE dbo.MantenimientoProgramado (
    id_programado         UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_MantProgramado PRIMARY KEY,
    id_organizacion       UNIQUEIDENTIFIER NOT NULL,
    -- Null cuando se programó a mano y no desde un plan.
    id_plan               UNIQUEIDENTIFIER NULL,
    id_equipo             UNIQUEIDENTIFIER NOT NULL,
    id_tipo_mantenimiento UNIQUEIDENTIFIER NOT NULL,
    fecha_programada      DATE             NOT NULL,
    -- «Vencido» NO es un estado: es `PROGRAMADO` con la fecha pasada. Guardarlo
    -- obligaria a un proceso que recorra la tabla todos los dias para moverlo, y
    -- el dia que ese proceso no corra el sistema mostraria datos viejos como si
    -- fueran ciertos. Derivarlo de la fecha no puede quedar desactualizado.
    estado                VARCHAR(20)      NOT NULL CONSTRAINT DF_MantProg_estado DEFAULT 'PROGRAMADO',
    -- El mantenimiento que lo ejecuto, cuando se ejecuto.
    id_mantenimiento      UNIQUEIDENTIFIER NULL,
    motivo                VARCHAR(500)     NULL,
    fecha_alta            DATETIME2(0)     NOT NULL CONSTRAINT DF_MantProg_alta DEFAULT SYSDATETIME(),
    CONSTRAINT FK_MantProg_Organizacion  FOREIGN KEY (id_organizacion)       REFERENCES dbo.Organizacion(id_organizacion),
    CONSTRAINT FK_MantProg_Plan          FOREIGN KEY (id_plan)               REFERENCES dbo.PlanMantenimiento(id_plan),
    CONSTRAINT FK_MantProg_Equipo        FOREIGN KEY (id_equipo)             REFERENCES dbo.Equipo(id_equipo),
    CONSTRAINT FK_MantProg_Tipo          FOREIGN KEY (id_tipo_mantenimiento) REFERENCES dbo.TipoMantenimiento(id_tipo_mantenimiento),
    CONSTRAINT FK_MantProg_Mantenimiento FOREIGN KEY (id_mantenimiento)      REFERENCES dbo.Mantenimiento(id_mantenimiento),
    CONSTRAINT CK_MantProg_estado CHECK (estado IN ('PROGRAMADO','EJECUTADO','ANULADO'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MantProg_equipo_fecha')
CREATE NONCLUSTERED INDEX IX_MantProg_equipo_fecha
    ON dbo.MantenimientoProgramado(id_equipo, fecha_programada);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MantProg_org_estado')
CREATE NONCLUSTERED INDEX IX_MantProg_org_estado
    ON dbo.MantenimientoProgramado(id_organizacion, estado, fecha_programada);
GO

/* ----------------------------------------------------------------------
   Motor predictivo
   ---------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ReglaAlerta', N'U') IS NULL
CREATE TABLE dbo.ReglaAlerta (
    id_regla        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ReglaAlerta PRIMARY KEY,
    id_organizacion UNIQUEIDENTIFIER NOT NULL,
    nombre          VARCHAR(100)     NOT NULL,
    descripcion     VARCHAR(500)     NULL,
    -- Discrimina qué estrategia de evaluación se aplica (patrón Strategy).
    tipo            VARCHAR(40)      NOT NULL,
    -- Parámetros de la regla en JSON. Cada tipo define sus propias claves;
    -- guardarlos así evita una tabla de parámetros por cada tipo de regla.
    condicion       VARCHAR(MAX)     NOT NULL,
    peso            INT              NOT NULL CONSTRAINT DF_ReglaAlerta_peso DEFAULT 25,
    activa          BIT              NOT NULL CONSTRAINT DF_ReglaAlerta_activa DEFAULT 1,
    CONSTRAINT FK_ReglaAlerta_Organizacion FOREIGN KEY (id_organizacion) REFERENCES dbo.Organizacion(id_organizacion),
    CONSTRAINT CK_ReglaAlerta_tipo CHECK (tipo IN (
        'RECURRENCIA_FALLAS','MANTENIMIENTO_VENCIDO','ACUMULACION_INCIDENCIAS',
        'ANTIGUEDAD_EQUIPO','GARANTIA_POR_VENCER')),
    CONSTRAINT CK_ReglaAlerta_peso CHECK (peso BETWEEN 0 AND 100)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ReglaAlerta_org_nombre')
CREATE UNIQUE NONCLUSTERED INDEX UX_ReglaAlerta_org_nombre ON dbo.ReglaAlerta(id_organizacion, nombre);
GO

IF OBJECT_ID(N'dbo.AlertaPredictiva', N'U') IS NULL
CREATE TABLE dbo.AlertaPredictiva (
    id_alerta        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AlertaPredictiva PRIMARY KEY,
    id_organizacion  UNIQUEIDENTIFIER NOT NULL,
    id_equipo        UNIQUEIDENTIFIER NOT NULL,
    id_regla         UNIQUEIDENTIFIER NOT NULL,
    id_estado_alerta UNIQUEIDENTIFIER NOT NULL,
    fecha_generacion DATETIME2(0)     NOT NULL CONSTRAINT DF_Alerta_fecha DEFAULT SYSDATETIME(),
    motivo           VARCHAR(500)     NOT NULL,
    recomendacion    VARCHAR(1000)    NULL,
    nivel_riesgo     INT              NOT NULL,   -- 0..100, el score al momento de generarla
    fecha_atencion   DATETIME2(0)     NULL,
    id_usuario_atencion UNIQUEIDENTIFIER NULL,
    CONSTRAINT FK_Alerta_Organizacion FOREIGN KEY (id_organizacion)  REFERENCES dbo.Organizacion(id_organizacion),
    CONSTRAINT FK_Alerta_Equipo       FOREIGN KEY (id_equipo)        REFERENCES dbo.Equipo(id_equipo),
    CONSTRAINT FK_Alerta_Regla        FOREIGN KEY (id_regla)         REFERENCES dbo.ReglaAlerta(id_regla),
    CONSTRAINT FK_Alerta_Estado       FOREIGN KEY (id_estado_alerta) REFERENCES dbo.EstadoAlerta(id_estado_alerta),
    CONSTRAINT CK_Alerta_nivel        CHECK (nivel_riesgo BETWEEN 0 AND 100)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Alerta_org_estado_fecha')
CREATE NONCLUSTERED INDEX IX_Alerta_org_estado_fecha
    ON dbo.AlertaPredictiva(id_organizacion, id_estado_alerta, fecha_generacion DESC);
GO

/* Foto del scoring en cada evaluación. Sin esto no se puede explicar por qué
   un equipo estaba en rojo hace dos semanas y hoy está en verde. */
IF OBJECT_ID(N'dbo.EvaluacionRiesgo', N'U') IS NULL
CREATE TABLE dbo.EvaluacionRiesgo (
    id_evaluacion   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EvaluacionRiesgo PRIMARY KEY,
    id_organizacion UNIQUEIDENTIFIER NOT NULL,
    id_equipo       UNIQUEIDENTIFIER NOT NULL,
    fecha           DATETIME2(0)     NOT NULL CONSTRAINT DF_EvalRiesgo_fecha DEFAULT SYSDATETIME(),
    score           INT              NOT NULL,
    nivel           VARCHAR(10)      NOT NULL,   -- BAJO | MEDIO | ALTO
    detalle         VARCHAR(MAX)     NULL,       -- JSON: aporte de cada regla al score
    CONSTRAINT FK_EvalRiesgo_Organizacion FOREIGN KEY (id_organizacion) REFERENCES dbo.Organizacion(id_organizacion),
    CONSTRAINT FK_EvalRiesgo_Equipo       FOREIGN KEY (id_equipo)       REFERENCES dbo.Equipo(id_equipo),
    CONSTRAINT CK_EvalRiesgo_score        CHECK (score BETWEEN 0 AND 100),
    CONSTRAINT CK_EvalRiesgo_nivel        CHECK (nivel IN ('BAJO','MEDIO','ALTO'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EvalRiesgo_equipo_fecha')
CREATE NONCLUSTERED INDEX IX_EvalRiesgo_equipo_fecha ON dbo.EvaluacionRiesgo(id_equipo, fecha DESC);
GO

/* ----------------------------------------------------------------------
   Configuración de la asignación por organización (pantalla 10.5.1.7.7)
   ---------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ConfiguracionAsignacion', N'U') IS NULL
CREATE TABLE dbo.ConfiguracionAsignacion (
    id_organizacion      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ConfiguracionAsignacion PRIMARY KEY,
    proveedor_ia         VARCHAR(30)      NOT NULL CONSTRAINT DF_ConfAsig_proveedor DEFAULT 'SIMULADO',
    api_key_cifrada      VARCHAR(500)     NULL,
    modelo               VARCHAR(60)      NULL,
    -- Qué se le manda al servicio externo. El usuario lo decide: son datos de su gente.
    enviar_carga         BIT NOT NULL CONSTRAINT DF_ConfAsig_carga         DEFAULT 1,
    enviar_especialidad  BIT NOT NULL CONSTRAINT DF_ConfAsig_especialidad  DEFAULT 1,
    enviar_historial     BIT NOT NULL CONSTRAINT DF_ConfAsig_historial     DEFAULT 1,
    enviar_disponibilidad BIT NOT NULL CONSTRAINT DF_ConfAsig_disponib     DEFAULT 0,
    estrategia_respaldo  VARCHAR(30)      NOT NULL CONSTRAINT DF_ConfAsig_respaldo DEFAULT 'MENOR_CARGA',
    timeout_segundos     INT              NOT NULL CONSTRAINT DF_ConfAsig_timeout DEFAULT 15,
    ultima_consulta_ok   DATETIME2(0)     NULL,
    ultimo_error         VARCHAR(500)     NULL,
    CONSTRAINT FK_ConfAsig_Organizacion FOREIGN KEY (id_organizacion) REFERENCES dbo.Organizacion(id_organizacion),
    CONSTRAINT CK_ConfAsig_proveedor CHECK (proveedor_ia IN ('SIMULADO','CLAUDE')),
    CONSTRAINT CK_ConfAsig_respaldo  CHECK (estrategia_respaldo IN ('MENOR_CARGA','SIN_ASIGNAR')),
    CONSTRAINT CK_ConfAsig_timeout   CHECK (timeout_segundos BETWEEN 1 AND 120)
);
GO

/* ----------------------------------------------------------------------
   Facturación del servicio (RF-17)

   Lo que la organización paga por usar PredictIT, período a período. El
   importe sale del plan comercial y de los equipos administrados al cierre,
   que es el mismo cálculo que la pantalla de Organización muestra mes a mes;
   lo que agrega el comprobante es dejarlo asentado con su número, su fecha y
   su estado de cobro.

   No pretende ser un comprobante fiscal: no hay CAE, ni punto de venta, ni
   integración con AFIP. Es el registro interno del servicio, y por eso la
   tabla se llama Comprobante y no Factura.
   ---------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Comprobante', N'U') IS NULL
CREATE TABLE dbo.Comprobante (
    id_comprobante        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Comprobante PRIMARY KEY,
    id_organizacion       UNIQUEIDENTIFIER NOT NULL,
    -- El mes facturado, como 'AAAA-MM'. CHAR y no dos enteros: es lo que se
    -- muestra, lo que se filtra y lo que ordena bien alfabeticamente.
    periodo               CHAR(7)          NOT NULL,
    -- Correlativo por organizacion, asignado al emitir. Null mientras es
    -- borrador: numerar algo que todavia se puede borrar deja huecos, y un
    -- hueco en una numeracion correlativa es una pregunta que hay que poder
    -- contestar.
    numero                INT              NULL,
    -- El plan y sus numeros quedan copiados en el comprobante. Si se leyeran
    -- del plan al consultar, cambiar el precio reescribiria los comprobantes
    -- del anio pasado, y eso no es un detalle contable: es el registro de lo
    -- que la organizacion efectivamente pago.
    id_plan               UNIQUEIDENTIFIER NOT NULL,
    equipos_administrados INT              NOT NULL,
    equipos_incluidos     INT              NOT NULL,
    subtotal              DECIMAL(14,2)    NOT NULL,
    alicuota_iva          DECIMAL(5,2)     NOT NULL CONSTRAINT DF_Comprobante_iva DEFAULT 21,
    iva                   DECIMAL(14,2)    NOT NULL,
    total                 DECIMAL(14,2)    NOT NULL,
    -- «Vencido» no es un estado: es EMITIDO con la fecha de vencimiento
    -- pasada, igual que en la agenda de mantenimiento.
    estado                VARCHAR(20)      NOT NULL CONSTRAINT DF_Comprobante_estado DEFAULT 'BORRADOR',
    fecha_emision         DATE             NULL,
    fecha_vencimiento     DATE             NULL,
    fecha_pago            DATE             NULL,
    motivo                VARCHAR(500)     NULL,
    fecha_alta            DATETIME2(0)     NOT NULL CONSTRAINT DF_Comprobante_alta DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Comprobante_Organizacion FOREIGN KEY (id_organizacion) REFERENCES dbo.Organizacion(id_organizacion),
    CONSTRAINT FK_Comprobante_Plan         FOREIGN KEY (id_plan)         REFERENCES dbo.PlanComercial(id_plan),
    CONSTRAINT CK_Comprobante_estado CHECK (estado IN ('BORRADOR','EMITIDO','PAGADO','ANULADO')),
    -- Un comprobante por organizacion y periodo: generar dos veces el mismo
    -- mes no duplica nada. Lo garantiza la base y no solo el negocio, porque
    -- dos pedidos simultaneos pasarian los dos por la validacion.
    CONSTRAINT UQ_Comprobante_periodo UNIQUE (id_organizacion, periodo)
);
GO

/* El correlativo es unico por organizacion, pero solo entre los que tienen
   numero. Va como indice filtrado y no como UNIQUE: SQL Server trata el NULL
   como un valor mas en una restriccion de unicidad, asi que un UNIQUE sobre
   (organizacion, numero) admite un unico borrador por organizacion y el
   segundo revienta. Paso al generar el borrador de dos meses distintos. */
IF EXISTS (SELECT 1 FROM sys.objects WHERE name = N'UQ_Comprobante_numero')
    ALTER TABLE dbo.Comprobante DROP CONSTRAINT UQ_Comprobante_numero;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_Comprobante_numero')
CREATE UNIQUE NONCLUSTERED INDEX UQ_Comprobante_numero
    ON dbo.Comprobante(id_organizacion, numero)
    WHERE numero IS NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Comprobante_org_estado')
CREATE NONCLUSTERED INDEX IX_Comprobante_org_estado
    ON dbo.Comprobante(id_organizacion, estado, periodo DESC);
GO

/* El desglose. Va en lineas y no en columnas del comprobante porque no se sabe
   cuantos conceptos va a tener: el abono y los equipos adicionales son
   siempre, pero un descuento comercial o el ajuste de un mes mal facturado son
   cuantos hagan falta. */
IF OBJECT_ID(N'dbo.LineaComprobante', N'U') IS NULL
CREATE TABLE dbo.LineaComprobante (
    id_linea        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_LineaComprobante PRIMARY KEY,
    id_comprobante  UNIQUEIDENTIFIER NOT NULL,
    orden           INT              NOT NULL,
    concepto        VARCHAR(200)     NOT NULL,
    cantidad        INT              NOT NULL CONSTRAINT DF_LineaComp_cantidad DEFAULT 1,
    precio_unitario DECIMAL(14,2)    NOT NULL,
    -- Guardado, no calculado al leer: ver el comentario de arriba sobre el
    -- precio del plan.
    importe         DECIMAL(14,2)    NOT NULL,
    -- Un ajuste lo cargo una persona; el resto sale del plan.
    es_ajuste       BIT              NOT NULL CONSTRAINT DF_LineaComp_ajuste DEFAULT 0,
    -- Las lineas se borran con el comprobante: no existen sin el, y un
    -- borrador se regenera entero cada vez que se recalcula.
    CONSTRAINT FK_LineaComp_Comprobante FOREIGN KEY (id_comprobante)
        REFERENCES dbo.Comprobante(id_comprobante) ON DELETE CASCADE,
    CONSTRAINT CK_LineaComp_cantidad CHECK (cantidad <> 0)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LineaComp_comprobante')
CREATE NONCLUSTERED INDEX IX_LineaComp_comprobante
    ON dbo.LineaComprobante(id_comprobante, orden);
GO

PRINT 'PredictIT_Negocio: esquema aplicado.';
GO
