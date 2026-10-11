/* =========================================================================
   PredictIT — Datos iniciales de la base de NEGOCIO
   Catálogos, dos organizaciones de demo y un historial técnico suficiente
   para que el motor predictivo tenga de dónde sacar alertas.

   Las fechas son relativas a la fecha de ejecución, así el seed sigue siendo
   útil dentro de seis meses: las reglas de ventana temporal (últimos 30/90
   días) tienen que disparar igual.
   ========================================================================= */

USE [PredictIT_Negocio];
GO
SET NOCOUNT ON;
GO

/* ------------------------------------------------------- Planes comerciales
   Precios revisados (D-14). Los originales del documento —34.900 / 99.900 /
   27.900— equivalian a unos USD 1,2 por equipo por mes, alrededor de un tercio
   de lo que cobran las herramientas de RMM del mercado, y con sueldos a valor de
   mercado el negocio no cerraba en ningun año. Estos llevan el abono a entre
   USD 2,5 y USD 3,1 por equipo.

   El MERGE trae WHEN MATCHED a proposito: si solo insertara, una base ya creada
   se quedaria con los precios viejos y la pantalla de Organizacion mostraria una
   cuenta distinta a la del presupuesto financiero. */
MERGE dbo.PlanComercial AS d
USING (VALUES
    ('29000000-0000-0000-0000-000000000001','INICIAL', 'Plan Inicial',   89000.00, 15, 4200.00, 'Mail y chat, en horario comercial'),
    ('29000000-0000-0000-0000-000000000002','ESTANDAR','Plan Estándar', 239000.00, 50, 3500.00, 'Priorizado, con SLA de 8 horas hábiles'),
    ('29000000-0000-0000-0000-000000000003','PARTNER', 'Plan Partner',   69000.00, 30, 2800.00, 'Canal dedicado: el partner aporta la primera linea de soporte')
) AS s(id,codigo,nombre,abono,incluidos,adicional,soporte)
ON d.id_plan = CAST(s.id AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN
    INSERT (id_plan, codigo, nombre, abono_mensual, equipos_incluidos, precio_equipo_adicional, soporte)
    VALUES (CAST(s.id AS UNIQUEIDENTIFIER), s.codigo, s.nombre, s.abono, s.incluidos, s.adicional, s.soporte)
WHEN MATCHED THEN UPDATE SET
    abono_mensual = s.abono,
    equipos_incluidos = s.incluidos,
    precio_equipo_adicional = s.adicional,
    soporte = s.soporte;
GO

/* ---------------------------------------------------------- Organizaciones
   La primera es la del mockup del documento (10.5.1.1). La segunda existe
   para poder demostrar que el aislamiento multi-cliente funciona de verdad.

   Las dos quedan en Plan Inicial (15 equipos incluidos) a propósito: el Estudio
   administra 52 equipos, muy por encima de lo que incluye su plan, y ése es el
   escenario que la pantalla de Organización tiene que saber mostrar. La Clínica,
   con 2 equipos, es el caso contrario y sirve de contraste. */
MERGE dbo.Organizacion AS d
USING (VALUES
    ('A0000000-0000-0000-0000-000000000001','Estudio Pettini & Asociados S.R.L.','Estudio Pettini & Asoc.','30-71234567-9','INICIAL'),
    ('A0000000-0000-0000-0000-000000000002','Clínica San Isidro S.A.',           'Clínica San Isidro',     '30-70987654-3','INICIAL')
) AS s(id,razon,corto,cuit,codigo_plan)
ON d.id_organizacion = CAST(s.id AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN
    INSERT (id_organizacion, razon_social, nombre_corto, cuit, id_plan)
    VALUES (CAST(s.id AS UNIQUEIDENTIFIER), s.razon, s.corto, s.cuit,
            (SELECT id_plan FROM dbo.PlanComercial WHERE codigo = s.codigo_plan));
GO

/* Bases creadas antes de que el plan fuera una entidad: se completa la
   referencia desde la columna de texto y recién después se la retira. */
IF COL_LENGTH('dbo.Organizacion', 'plan_comercial') IS NOT NULL
BEGIN
    EXEC sp_executesql N'
        UPDATE o SET o.id_plan = p.id_plan
        FROM dbo.Organizacion o
        JOIN dbo.PlanComercial p ON p.codigo = o.plan_comercial
        WHERE o.id_plan IS NULL;';

    DECLARE @ck SYSNAME = (SELECT name FROM sys.check_constraints
                           WHERE parent_object_id = OBJECT_ID('dbo.Organizacion')
                             AND name = 'CK_Organizacion_plan');
    IF @ck IS NOT NULL EXEC('ALTER TABLE dbo.Organizacion DROP CONSTRAINT ' + @ck);

    DECLARE @df SYSNAME = (SELECT dc.name FROM sys.default_constraints dc
                           JOIN sys.columns c ON c.object_id = dc.parent_object_id
                                             AND c.column_id = dc.parent_column_id
                           WHERE dc.parent_object_id = OBJECT_ID('dbo.Organizacion')
                             AND c.name = 'plan_comercial');
    IF @df IS NOT NULL EXEC('ALTER TABLE dbo.Organizacion DROP CONSTRAINT ' + @df);

    ALTER TABLE dbo.Organizacion DROP COLUMN plan_comercial;
    PRINT 'Organizacion.plan_comercial migrado a id_plan y retirado.';
END
GO

/* ------------------------------------------------------ Catálogos globales */
MERGE dbo.TipoEquipo AS d
USING (VALUES
    ('20000000-0000-0000-0000-000000000001','PC de escritorio'),
    ('20000000-0000-0000-0000-000000000002','Notebook'),
    ('20000000-0000-0000-0000-000000000003','Servidor'),
    ('20000000-0000-0000-0000-000000000004','Impresora'),
    ('20000000-0000-0000-0000-000000000005','Monitor'),
    ('20000000-0000-0000-0000-000000000006','Router'),
    ('20000000-0000-0000-0000-000000000007','Switch'),
    ('20000000-0000-0000-0000-000000000008','UPS')
) AS s(id,nombre)
ON d.id_tipo_equipo = CAST(s.id AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN INSERT (id_tipo_equipo, nombre) VALUES (CAST(s.id AS UNIQUEIDENTIFIER), s.nombre);
GO

MERGE dbo.EstadoEquipo AS d
USING (VALUES
    ('21000000-0000-0000-0000-000000000001','Operativo',       1,1),
    ('21000000-0000-0000-0000-000000000002','En reparación',   1,2),
    ('21000000-0000-0000-0000-000000000003','En observación',  1,3),
    ('21000000-0000-0000-0000-000000000004','Fuera de servicio',0,4),
    ('21000000-0000-0000-0000-000000000005','Dado de baja',    0,5)
) AS s(id,nombre,operativo,orden)
ON d.id_estado_equipo = CAST(s.id AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN INSERT (id_estado_equipo, nombre, operativo, orden)
    VALUES (CAST(s.id AS UNIQUEIDENTIFIER), s.nombre, s.operativo, s.orden);
GO

MERGE dbo.EstadoIncidencia AS d
USING (VALUES
    ('22000000-0000-0000-0000-000000000001','Pendiente de asignación',0,1),
    ('22000000-0000-0000-0000-000000000002','Asignada',               0,2),
    ('22000000-0000-0000-0000-000000000003','En curso',               0,3),
    ('22000000-0000-0000-0000-000000000004','En espera de repuesto',  0,4),
    ('22000000-0000-0000-0000-000000000005','Resuelta',               1,5),
    ('22000000-0000-0000-0000-000000000006','Cerrada',                1,6),
    ('22000000-0000-0000-0000-000000000007','Anulada',                1,7)
) AS s(id,nombre,final,orden)
ON d.id_estado_incidencia = CAST(s.id AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN INSERT (id_estado_incidencia, nombre, es_final, orden)
    VALUES (CAST(s.id AS UNIQUEIDENTIFIER), s.nombre, s.final, s.orden);
GO

/* horas_objetivo es el objetivo de resolucion por prioridad. La prioridad Baja
   queda en NULL a proposito: no tiene objetivo pactado, y por eso no puede
   incumplirlo. */
MERGE dbo.PrioridadIncidencia AS d
USING (VALUES
    ('23000000-0000-0000-0000-000000000001','Baja',   1, NULL),
    ('23000000-0000-0000-0000-000000000002','Media',  2,   72),
    ('23000000-0000-0000-0000-000000000003','Alta',   3,   24),
    ('23000000-0000-0000-0000-000000000004','Crítica',4,    4)
) AS s(id,nombre,nivel,horas)
ON d.id_prioridad = CAST(s.id AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN INSERT (id_prioridad, nombre, nivel, horas_objetivo)
    VALUES (CAST(s.id AS UNIQUEIDENTIFIER), s.nombre, s.nivel, s.horas)
WHEN MATCHED THEN UPDATE SET nivel = s.nivel, horas_objetivo = s.horas;
GO

MERGE dbo.Especialidad AS d
USING (VALUES
    ('27000000-0000-0000-0000-000000000001','Hardware'),
    ('27000000-0000-0000-0000-000000000002','Redes'),
    ('27000000-0000-0000-0000-000000000003','Sistemas operativos'),
    ('27000000-0000-0000-0000-000000000004','Impresión'),
    ('27000000-0000-0000-0000-000000000005','Servidores')
) AS s(id,nombre)
ON d.id_especialidad = CAST(s.id AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN INSERT (id_especialidad, nombre) VALUES (CAST(s.id AS UNIQUEIDENTIFIER), s.nombre);
GO

/* Cada categoria apunta a la especialidad que la resuelve: es el dato central
   del contexto que se le manda al proveedor de asignacion. «Sin clasificar»
   queda sin especialidad porque justamente todavia no se sabe. */
MERGE dbo.CategoriaIncidencia AS d
USING (VALUES
    ('24000000-0000-0000-0000-000000000001','Hardware',       'Fallas físicas del equipo',        '27000000-0000-0000-0000-000000000001'),
    ('24000000-0000-0000-0000-000000000002','Software',       'Sistema operativo y aplicaciones', '27000000-0000-0000-0000-000000000003'),
    ('24000000-0000-0000-0000-000000000003','Red',            'Conectividad, cableado, wifi',     '27000000-0000-0000-0000-000000000002'),
    ('24000000-0000-0000-0000-000000000004','Periféricos',    'Impresoras, monitores, teclados',  '27000000-0000-0000-0000-000000000004'),
    ('24000000-0000-0000-0000-000000000005','Energía',        'Cortes, UPS, alimentación',        '27000000-0000-0000-0000-000000000001'),
    ('24000000-0000-0000-0000-000000000006','Rendimiento',    'Lentitud y degradación',           '27000000-0000-0000-0000-000000000003'),
    ('24000000-0000-0000-0000-000000000007','Sin clasificar', 'Pendiente de triage',              NULL)
) AS s(id,nombre,descripcion,id_esp)
ON d.id_categoria = CAST(s.id AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN INSERT (id_categoria, nombre, descripcion, id_especialidad)
    VALUES (CAST(s.id AS UNIQUEIDENTIFIER), s.nombre, s.descripcion, CAST(s.id_esp AS UNIQUEIDENTIFIER))
WHEN MATCHED THEN UPDATE SET id_especialidad = CAST(s.id_esp AS UNIQUEIDENTIFIER);
GO

/* «Predictivo» cuenta como preventivo: es una intervencion planificada antes de
   la falla, que es exactamente lo que reinicia el contador de la regla. */
MERGE dbo.TipoMantenimiento AS d
USING (VALUES
    ('25000000-0000-0000-0000-000000000001','Preventivo', 1),
    ('25000000-0000-0000-0000-000000000002','Correctivo', 0),
    ('25000000-0000-0000-0000-000000000003','Predictivo', 1)
) AS s(id,nombre,preventivo)
ON d.id_tipo_mantenimiento = CAST(s.id AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN INSERT (id_tipo_mantenimiento, nombre, es_preventivo)
    VALUES (CAST(s.id AS UNIQUEIDENTIFIER), s.nombre, s.preventivo)
WHEN MATCHED THEN UPDATE SET es_preventivo = s.preventivo;
GO

MERGE dbo.EstadoAlerta AS d
USING (VALUES
    ('26000000-0000-0000-0000-000000000001','Activa',    0),
    ('26000000-0000-0000-0000-000000000002','En revisión',0),
    ('26000000-0000-0000-0000-000000000003','Atendida',  1),
    ('26000000-0000-0000-0000-000000000004','Descartada',1)
) AS s(id,nombre,final)
ON d.id_estado_alerta = CAST(s.id AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN INSERT (id_estado_alerta, nombre, es_final)
    VALUES (CAST(s.id AS UNIQUEIDENTIFIER), s.nombre, s.final);
GO

/* ------------------------------------------------------------- Ubicaciones */
MERGE dbo.Ubicacion AS d
USING (VALUES
    ('28000000-0000-0000-0000-000000000001','A0000000-0000-0000-0000-000000000001','Administración','Planta baja, sector administrativo'),
    ('28000000-0000-0000-0000-000000000002','A0000000-0000-0000-0000-000000000001','Recepción',     'Ingreso principal'),
    ('28000000-0000-0000-0000-000000000003','A0000000-0000-0000-0000-000000000001','Gerencia',      'Primer piso'),
    ('28000000-0000-0000-0000-000000000004','A0000000-0000-0000-0000-000000000001','Sala de servidores','Primer piso, sector técnico'),
    ('28000000-0000-0000-0000-000000000005','A0000000-0000-0000-0000-000000000001','Comercial',     'Planta baja, sector comercial'),
    ('28000000-0000-0000-0000-000000000011','A0000000-0000-0000-0000-000000000002','Consultorios',  'Planta baja'),
    ('28000000-0000-0000-0000-000000000012','A0000000-0000-0000-0000-000000000002','Admisión',      'Ingreso')
) AS s(id,org,nombre,descripcion)
ON d.id_ubicacion = CAST(s.id AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN INSERT (id_ubicacion, id_organizacion, nombre, descripcion)
    VALUES (CAST(s.id AS UNIQUEIDENTIFIER), CAST(s.org AS UNIQUEIDENTIFIER), s.nombre, s.descripcion);
GO

/* --------------------------------------------------- Técnicos y su perfil
   Los id_tecnico son los usuarios de PredictIT_Servicio (referencia lógica).
     …0002 Martín Acosta   · …0003 Julieta Pérez · …0004 Lucas Romano       */
MERGE dbo.TecnicoEspecialidad AS d
USING (VALUES
    ('A0000000-0000-0000-0000-000000000001','B0000000-0000-0000-0000-000000000002','27000000-0000-0000-0000-000000000001',3),
    ('A0000000-0000-0000-0000-000000000001','B0000000-0000-0000-0000-000000000002','27000000-0000-0000-0000-000000000005',2),
    ('A0000000-0000-0000-0000-000000000001','B0000000-0000-0000-0000-000000000003','27000000-0000-0000-0000-000000000002',3),
    ('A0000000-0000-0000-0000-000000000001','B0000000-0000-0000-0000-000000000003','27000000-0000-0000-0000-000000000003',2),
    ('A0000000-0000-0000-0000-000000000001','B0000000-0000-0000-0000-000000000004','27000000-0000-0000-0000-000000000004',3),
    ('A0000000-0000-0000-0000-000000000001','B0000000-0000-0000-0000-000000000004','27000000-0000-0000-0000-000000000001',1)
) AS s(org,tec,esp,nivel)
ON d.id_organizacion = CAST(s.org AS UNIQUEIDENTIFIER)
   AND d.id_tecnico = CAST(s.tec AS UNIQUEIDENTIFIER)
   AND d.id_especialidad = CAST(s.esp AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN INSERT (id_organizacion, id_tecnico, id_especialidad, nivel)
    VALUES (CAST(s.org AS UNIQUEIDENTIFIER), CAST(s.tec AS UNIQUEIDENTIFIER), CAST(s.esp AS UNIQUEIDENTIFIER), s.nivel);
GO

/* ----------------------------------------------------------------- Equipos
   Los cinco primeros son los del mockup del dashboard (10.5.1.1), para que
   la demo del sistema y la del documento muestren lo mismo. */
MERGE dbo.Equipo AS d
USING (VALUES
    ('30000000-0000-0000-0000-000000000001','A0000000-0000-0000-0000-000000000001','PC-ADM-014','20000000-0000-0000-0000-000000000001','Dell','OptiPlex 3080','DL3080-014','28000000-0000-0000-0000-000000000001','21000000-0000-0000-0000-000000000003','B0000000-0000-0000-0000-000000000005',3,-1580,-485),
    ('30000000-0000-0000-0000-000000000002','A0000000-0000-0000-0000-000000000001','NB-COM-007','20000000-0000-0000-0000-000000000002','Lenovo','ThinkPad E14','LN-E14-007','28000000-0000-0000-0000-000000000005','21000000-0000-0000-0000-000000000003','B0000000-0000-0000-0000-000000000005',3,-1200,-105),
    ('30000000-0000-0000-0000-000000000003','A0000000-0000-0000-0000-000000000001','IMP-REC-002','20000000-0000-0000-0000-000000000004','HP','LaserJet Pro M404','HP404-002','28000000-0000-0000-0000-000000000002','21000000-0000-0000-0000-000000000001',NULL,2,-1750,-655),
    ('30000000-0000-0000-0000-000000000004','A0000000-0000-0000-0000-000000000001','PC-ADM-021','20000000-0000-0000-0000-000000000001','HP','ProDesk 400 G7','HP400-021','28000000-0000-0000-0000-000000000001','21000000-0000-0000-0000-000000000001','B0000000-0000-0000-0000-000000000005',2,-900,195),
    ('30000000-0000-0000-0000-000000000005','A0000000-0000-0000-0000-000000000001','NB-GER-001','20000000-0000-0000-0000-000000000002','Apple','MacBook Air M2','APL-M2-001','28000000-0000-0000-0000-000000000003','21000000-0000-0000-0000-000000000001','B0000000-0000-0000-0000-000000000001',3,-620,475),
    ('30000000-0000-0000-0000-000000000006','A0000000-0000-0000-0000-000000000001','SRV-CEN-001','20000000-0000-0000-0000-000000000003','Dell','PowerEdge T350','DLT350-001','28000000-0000-0000-0000-000000000004','21000000-0000-0000-0000-000000000001',NULL,4,-1100,265),
    ('30000000-0000-0000-0000-000000000007','A0000000-0000-0000-0000-000000000001','RTR-CEN-001','20000000-0000-0000-0000-000000000006','MikroTik','hEX S','MT-HEXS-001','28000000-0000-0000-0000-000000000004','21000000-0000-0000-0000-000000000001',NULL,4,-1400,-300),
    ('30000000-0000-0000-0000-000000000008','A0000000-0000-0000-0000-000000000001','UPS-CEN-001','20000000-0000-0000-0000-000000000008','APC','Back-UPS 1500','APC1500-001','28000000-0000-0000-0000-000000000004','21000000-0000-0000-0000-000000000002',NULL,4,-2100,-1000),
    ('30000000-0000-0000-0000-000000000009','A0000000-0000-0000-0000-000000000001','PC-COM-005','20000000-0000-0000-0000-000000000001','Lenovo','ThinkCentre M70','LN-M70-005','28000000-0000-0000-0000-000000000005','21000000-0000-0000-0000-000000000005',NULL,2,-400,690),
    ('30000000-0000-0000-0000-000000000010','A0000000-0000-0000-0000-000000000001','MON-ADM-030','20000000-0000-0000-0000-000000000005','Samsung','S24R650','SM-S24-030','28000000-0000-0000-0000-000000000001','21000000-0000-0000-0000-000000000001',NULL,1,-300,790),
    ('30000000-0000-0000-0000-000000000011','A0000000-0000-0000-0000-000000000001','SWI-CEN-002','20000000-0000-0000-0000-000000000007','TP-Link','TL-SG1024','TP-SG-002','28000000-0000-0000-0000-000000000004','21000000-0000-0000-0000-000000000001',NULL,3,-1900,-800),
    ('30000000-0000-0000-0000-000000000012','A0000000-0000-0000-0000-000000000001','NB-ADM-009','20000000-0000-0000-0000-000000000002','Dell','Latitude 3520','DL3520-009','28000000-0000-0000-0000-000000000001','21000000-0000-0000-0000-000000000001','B0000000-0000-0000-0000-000000000005',2,-750,340),
    -- Clínica San Isidro: sólo para verificar el aislamiento entre organizaciones.
    ('31000000-0000-0000-0000-000000000001','A0000000-0000-0000-0000-000000000002','PC-CON-001','20000000-0000-0000-0000-000000000001','Dell','OptiPlex 3000','CSI-DL-001','28000000-0000-0000-0000-000000000011','21000000-0000-0000-0000-000000000001',NULL,3,-500,600),
    ('31000000-0000-0000-0000-000000000002','A0000000-0000-0000-0000-000000000002','IMP-ADM-001','20000000-0000-0000-0000-000000000004','Brother','HL-L2360','CSI-BR-001','28000000-0000-0000-0000-000000000012','21000000-0000-0000-0000-000000000001',NULL,2,-800,100)
) AS s(id,org,codigo,tipo,marca,modelo,serie,ubic,estado,resp,crit,dias_adq,dias_gar)
ON d.id_equipo = CAST(s.id AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN
    INSERT (id_equipo, id_organizacion, codigo, id_tipo_equipo, marca, modelo, numero_serie,
            id_ubicacion, id_estado_equipo, id_responsable, criticidad,
            fecha_adquisicion, fecha_fin_garantia, fecha_alta)
    VALUES (CAST(s.id AS UNIQUEIDENTIFIER), CAST(s.org AS UNIQUEIDENTIFIER), s.codigo,
            CAST(s.tipo AS UNIQUEIDENTIFIER), s.marca, s.modelo, s.serie,
            CAST(s.ubic AS UNIQUEIDENTIFIER), CAST(s.estado AS UNIQUEIDENTIFIER),
            CAST(s.resp AS UNIQUEIDENTIFIER), s.crit,
            DATEADD(DAY, s.dias_adq, CAST(SYSDATETIME() AS DATE)),
            DATEADD(DAY, s.dias_gar, CAST(SYSDATETIME() AS DATE)),
            DATEADD(DAY, s.dias_adq, SYSDATETIME()))
WHEN MATCHED THEN
    -- Estos catorce equipos definen el escenario de demostración, así que el
    -- seed manda: si alguien los movió probando, volver a aplicarlo los repone.
    UPDATE SET id_estado_equipo = CAST(s.estado AS UNIQUEIDENTIFIER),
               id_ubicacion     = CAST(s.ubic AS UNIQUEIDENTIFIER),
               id_responsable   = CAST(s.resp AS UNIQUEIDENTIFIER),
               criticidad       = s.crit;
GO

/* ------------------------------------------- Resto del parque del Estudio
   Los doce equipos de arriba son los que tienen historial y nombre propio. El
   documento describe un parque de 52 equipos con 47 activos (10.5.1.1), y esa
   cifra importa por dos motivos: es la que muestran las pantallas del TFI, y es
   la que pone a la organización por encima de los 15 equipos que incluye su plan,
   que es el escenario que la pantalla de Organización tiene que saber mostrar.

   Estos cuarenta no tienen incidencias, que es lo normal: en un parque real la
   mayoría de los equipos no falla nunca. */
IF (SELECT COUNT(*) FROM dbo.Equipo
    WHERE id_organizacion = 'A0000000-0000-0000-0000-000000000001') < 52
BEGIN
    DECLARE @org_est UNIQUEIDENTIFIER = 'A0000000-0000-0000-0000-000000000001';

    ;WITH nums AS (
        SELECT TOP (40) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
        FROM sys.all_objects
    ), gen AS (
        SELECT n,
            CASE WHEN n <= 15 THEN 'PC-GEN'  WHEN n <= 25 THEN 'NB-GEN'
                 WHEN n <= 35 THEN 'MON-GEN' WHEN n <= 38 THEN 'IMP-GEN'
                 ELSE 'UPS-GEN' END AS prefijo,
            CASE WHEN n <= 15 THEN '20000000-0000-0000-0000-000000000001'
                 WHEN n <= 25 THEN '20000000-0000-0000-0000-000000000002'
                 WHEN n <= 35 THEN '20000000-0000-0000-0000-000000000005'
                 WHEN n <= 38 THEN '20000000-0000-0000-0000-000000000004'
                 ELSE '20000000-0000-0000-0000-000000000008' END AS tipo,
            -- Cuatro de baja: con PC-COM-005 son cinco, y quedan 47 activos.
            CASE WHEN n IN (7, 19, 31, 37) THEN '21000000-0000-0000-0000-000000000005'
                 ELSE '21000000-0000-0000-0000-000000000001' END AS estado,
            CASE (n % 5)
                 WHEN 0 THEN '28000000-0000-0000-0000-000000000001'
                 WHEN 1 THEN '28000000-0000-0000-0000-000000000002'
                 WHEN 2 THEN '28000000-0000-0000-0000-000000000003'
                 WHEN 3 THEN '28000000-0000-0000-0000-000000000004'
                 ELSE '28000000-0000-0000-0000-000000000005' END AS ubic
        FROM nums
    )
    INSERT INTO dbo.Equipo (id_equipo, id_organizacion, codigo, id_tipo_equipo,
                            marca, modelo, numero_serie, id_ubicacion, id_estado_equipo,
                            criticidad, fecha_adquisicion, fecha_fin_garantia, fecha_alta)
    SELECT NEWID(), @org_est,
           prefijo + '-' + RIGHT('000' + CAST(n AS VARCHAR(3)), 3),
           CAST(tipo AS UNIQUEIDENTIFIER),
           CASE (n % 4) WHEN 0 THEN 'Dell' WHEN 1 THEN 'HP' WHEN 2 THEN 'Lenovo' ELSE 'Samsung' END,
           'Modelo estándar',
           'SN-' + prefijo + '-' + RIGHT('000' + CAST(n AS VARCHAR(3)), 3),
           CAST(ubic AS UNIQUEIDENTIFIER), CAST(estado AS UNIQUEIDENTIFIER),
           1 + (n % 3),
           DATEADD(DAY, -(200 + n * 12), CAST(SYSDATETIME() AS DATE)),
           DATEADD(DAY, -(200 + n * 12) + 1095, CAST(SYSDATETIME() AS DATE)),
           DATEADD(DAY, -(200 + n * 12), SYSDATETIME())
    FROM gen;

    PRINT 'PredictIT_Negocio: parque del Estudio completado a 52 equipos.';
END
GO

/* ------------------------------------------------- Reglas del motor predictivo
   `condicion` es JSON y sus claves dependen del `tipo`. Cada estrategia del
   EvaluadorReglas sabe leer las suyas. */
MERGE dbo.ReglaAlerta AS d
USING (VALUES
    ('40000000-0000-0000-0000-000000000001','A0000000-0000-0000-0000-000000000001','Fallas recurrentes','RECURRENCIA_FALLAS',
     '{"minIncidencias":3,"ventanaDias":30}',30,
     'Tres o más incidencias sobre el mismo equipo en 30 días'),
    ('40000000-0000-0000-0000-000000000002','A0000000-0000-0000-0000-000000000001','Misma falla repetida','ACUMULACION_INCIDENCIAS','{"minIncidencias":2,"ventanaDias":90,"mismaCategoria":true}',25,
     'La misma categoría de falla se repite en el trimestre'),
    ('40000000-0000-0000-0000-000000000003','A0000000-0000-0000-0000-000000000001','Mantenimiento vencido','MANTENIMIENTO_VENCIDO','{"diasSinMantenimiento":180}',20,
     'Pasaron más de 180 días desde el último mantenimiento'),
    ('40000000-0000-0000-0000-000000000004','A0000000-0000-0000-0000-000000000001','Equipo con antigüedad alta','ANTIGUEDAD_EQUIPO','{"aniosUmbral":4}',15,
     'El equipo supera los cuatro años desde su adquisición'),
    ('40000000-0000-0000-0000-000000000005','A0000000-0000-0000-0000-000000000001','Garantía próxima a vencer','GARANTIA_POR_VENCER','{"diasAviso":60}',10,
     'La garantía vence dentro de los próximos 60 días'),
    ('41000000-0000-0000-0000-000000000001','A0000000-0000-0000-0000-000000000002','Fallas recurrentes','RECURRENCIA_FALLAS','{"minIncidencias":3,"ventanaDias":30}',30,
     'Tres o más incidencias sobre el mismo equipo en 30 días'),
    ('41000000-0000-0000-0000-000000000002','A0000000-0000-0000-0000-000000000002','Mantenimiento vencido','MANTENIMIENTO_VENCIDO','{"diasSinMantenimiento":180}',25,
     'Pasaron más de 180 días desde el último mantenimiento')
) AS s(id,org,nombre,tipo,condicion,peso,descripcion)
ON d.id_regla = CAST(s.id AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN
    INSERT (id_regla, id_organizacion, nombre, tipo, condicion, peso, descripcion, activa)
    VALUES (CAST(s.id AS UNIQUEIDENTIFIER), CAST(s.org AS UNIQUEIDENTIFIER), s.nombre,
            s.tipo, s.condicion, s.peso, s.descripcion, 1);
GO

/* -------------------------------------- Configuración de asignación por IA
   Arranca en SIMULADO: el sistema tiene que poder demostrarse sin conexión
   ni crédito de API. Cargando la clave se pasa a CLAUDE desde la pantalla. */
MERGE dbo.ConfiguracionAsignacion AS d
USING (VALUES
    ('A0000000-0000-0000-0000-000000000001','SIMULADO'),
    ('A0000000-0000-0000-0000-000000000002','SIMULADO')
) AS s(org,proveedor)
ON d.id_organizacion = CAST(s.org AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN
    INSERT (id_organizacion, proveedor_ia, modelo)
    VALUES (CAST(s.org AS UNIQUEIDENTIFIER), s.proveedor, 'claude-haiku-4-5');
GO

/* ------------------------------------------------------- Historial técnico
   Se genera sólo si la tabla está vacía: correr el seed dos veces no debe
   duplicar la historia ni falsear el scoring.

   El reparto no es uniforme a propósito. PC-ADM-014 concentra fallas en los
   últimos 30 días (dispara RECURRENCIA_FALLAS), NB-COM-007 repite la misma
   categoría en el trimestre (dispara ACUMULACION_INCIDENCIAS) y la impresora
   de recepción hace mucho que no recibe mantenimiento. */
IF NOT EXISTS (SELECT 1 FROM dbo.Incidencia)
BEGIN
    DECLARE @org UNIQUEIDENTIFIER = 'A0000000-0000-0000-0000-000000000001';
    DECLARE @cerrada UNIQUEIDENTIFIER = '22000000-0000-0000-0000-000000000006';
    DECLARE @encurso UNIQUEIDENTIFIER = '22000000-0000-0000-0000-000000000003';
    DECLARE @asignada UNIQUEIDENTIFIER = '22000000-0000-0000-0000-000000000002';

    ;WITH s(codigo, dias_atras, titulo, descripcion, categoria, prioridad, tecnico, estado) AS (
        SELECT * FROM (VALUES
        -- PC-ADM-014: 4 incidencias en los últimos 30 días
        ('PC-ADM-014',  3,'La PC se apaga sola','Se apaga sin aviso dos o tres veces por día, sobre todo a la tarde.','24000000-0000-0000-0000-000000000001','23000000-0000-0000-0000-000000000003','B0000000-0000-0000-0000-000000000002','ENCURSO'),
        ('PC-ADM-014',  9,'Pantalla azul al abrir el sistema de gestión','Aparece una pantalla azul y reinicia.','24000000-0000-0000-0000-000000000001','23000000-0000-0000-0000-000000000003','B0000000-0000-0000-0000-000000000002','CERRADA'),
        ('PC-ADM-014', 18,'Ruido del cooler','Hace un ruido fuerte y constante.','24000000-0000-0000-0000-000000000001','23000000-0000-0000-0000-000000000002','B0000000-0000-0000-0000-000000000002','CERRADA'),
        ('PC-ADM-014', 27,'Muy lenta al iniciar','Tarda más de cinco minutos en arrancar.','24000000-0000-0000-0000-000000000006','23000000-0000-0000-0000-000000000002','B0000000-0000-0000-0000-000000000003','CERRADA'),
        ('PC-ADM-014', 65,'No reconoce el pendrive','Ningún puerto USB del frente responde.','24000000-0000-0000-0000-000000000001','23000000-0000-0000-0000-000000000001','B0000000-0000-0000-0000-000000000002','CERRADA'),
        -- NB-COM-007: misma categoría (Hardware) repetida en el trimestre
        ('NB-COM-007',  5,'Se recalienta y se traba','Después de una hora se pone muy caliente y se traba.','24000000-0000-0000-0000-000000000001','23000000-0000-0000-0000-000000000003','B0000000-0000-0000-0000-000000000002','ASIGNADA'),
        ('NB-COM-007', 41,'Se recalienta','Segunda vez este trimestre, misma situación.','24000000-0000-0000-0000-000000000001','23000000-0000-0000-0000-000000000003','B0000000-0000-0000-0000-000000000002','CERRADA'),
        ('NB-COM-007', 72,'La batería no carga','Sólo funciona enchufada.','24000000-0000-0000-0000-000000000001','23000000-0000-0000-0000-000000000002','B0000000-0000-0000-0000-000000000004','CERRADA'),
        ('NB-COM-007',110,'No conecta al wifi','Se desconecta cada diez minutos.','24000000-0000-0000-0000-000000000003','23000000-0000-0000-0000-000000000002','B0000000-0000-0000-0000-000000000003','CERRADA'),
        -- IMP-REC-002
        ('IMP-REC-002', 12,'Atasca el papel','Atasca en casi todas las impresiones largas.','24000000-0000-0000-0000-000000000004','23000000-0000-0000-0000-000000000002','B0000000-0000-0000-0000-000000000004','CERRADA'),
        ('IMP-REC-002', 34,'Imprime con rayas','Sale una línea gris vertical en todas las hojas.','24000000-0000-0000-0000-000000000004','23000000-0000-0000-0000-000000000001','B0000000-0000-0000-0000-000000000004','CERRADA'),
        ('IMP-REC-002', 88,'No la toma la red','Desapareció de la lista de impresoras.','24000000-0000-0000-0000-000000000003','23000000-0000-0000-0000-000000000002','B0000000-0000-0000-0000-000000000003','CERRADA'),
        -- Resto del parque
        ('PC-ADM-021', 15,'Office no abre','Da error al abrir Excel.','24000000-0000-0000-0000-000000000002','23000000-0000-0000-0000-000000000002','B0000000-0000-0000-0000-000000000003','CERRADA'),
        ('PC-ADM-021', 58,'Anda lenta','Tarda en abrir cualquier programa.','24000000-0000-0000-0000-000000000006','23000000-0000-0000-0000-000000000001','B0000000-0000-0000-0000-000000000003','CERRADA'),
        ('NB-GER-001',  7,'No proyecta en la sala de reuniones','No detecta el proyector por HDMI.','24000000-0000-0000-0000-000000000004','23000000-0000-0000-0000-000000000002','B0000000-0000-0000-0000-000000000004','CERRADA'),
        ('SRV-CEN-001', 21,'Poco espacio en disco','Avisa que queda menos del 10%.','24000000-0000-0000-0000-000000000001','23000000-0000-0000-0000-000000000004','B0000000-0000-0000-0000-000000000002','ENCURSO'),
        ('RTR-CEN-001', 30,'Se corta internet','Se corta varias veces al día por unos minutos.','24000000-0000-0000-0000-000000000003','23000000-0000-0000-0000-000000000003','B0000000-0000-0000-0000-000000000003','CERRADA'),
        ('UPS-CEN-001',  2,'La UPS pita sin corte de luz','Suena continuo aunque no se cortó la luz.','24000000-0000-0000-0000-000000000005','23000000-0000-0000-0000-000000000004','B0000000-0000-0000-0000-000000000002','ASIGNADA'),
        ('UPS-CEN-001', 46,'No sostiene la carga','En el último corte aguantó menos de un minuto.','24000000-0000-0000-0000-000000000005','23000000-0000-0000-0000-000000000003','B0000000-0000-0000-0000-000000000002','CERRADA'),
        ('NB-ADM-009', 63,'Teclado con teclas que no responden','No andan la A y la S.','24000000-0000-0000-0000-000000000001','23000000-0000-0000-0000-000000000002','B0000000-0000-0000-0000-000000000004','CERRADA')
        ) v(codigo, dias_atras, titulo, descripcion, categoria, prioridad, tecnico, estado)
    )
    INSERT INTO dbo.Incidencia (id_incidencia, id_organizacion, numero, titulo, descripcion, fecha,
                                fecha_resolucion, id_equipo, id_estado_incidencia, id_prioridad,
                                id_categoria, id_tecnico, id_usuario_reportante, tipo_asignacion,
                                diagnostico, solucion)
    SELECT NEWID(), @org,
           ROW_NUMBER() OVER (ORDER BY s.dias_atras DESC),
           s.titulo, s.descripcion,
           DATEADD(DAY, -s.dias_atras, SYSDATETIME()),
           CASE WHEN s.estado = 'CERRADA' THEN DATEADD(DAY, -s.dias_atras + 1, SYSDATETIME()) END,
           e.id_equipo,
           CASE s.estado WHEN 'CERRADA' THEN @cerrada WHEN 'ENCURSO' THEN @encurso ELSE @asignada END,
           CAST(s.prioridad AS UNIQUEIDENTIFIER),
           CAST(s.categoria AS UNIQUEIDENTIFIER),
           CAST(s.tecnico AS UNIQUEIDENTIFIER),
           'B0000000-0000-0000-0000-000000000005',
           'IA',
           CASE WHEN s.estado = 'CERRADA' THEN 'Diagnóstico registrado por el técnico interviniente.' END,
           CASE WHEN s.estado = 'CERRADA' THEN 'Resuelto y verificado con el usuario.' END
    FROM s
    JOIN dbo.Equipo e ON e.codigo = s.codigo AND e.id_organizacion = @org;

    /* Mantenimientos. La impresora de recepción queda deliberadamente sin
       mantenimiento reciente para que dispare MANTENIMIENTO_VENCIDO. */
    ;WITH m(codigo, dias_atras, tipo, descripcion, resultado, tecnico) AS (
        SELECT * FROM (VALUES
        ('PC-ADM-014', 20,'25000000-0000-0000-0000-000000000002','Limpieza interna y cambio de pasta térmica.','Mejoró la temperatura, se deja en observación.','B0000000-0000-0000-0000-000000000002'),
        ('PC-ADM-014',150,'25000000-0000-0000-0000-000000000001','Mantenimiento preventivo semestral.','Sin observaciones.','B0000000-0000-0000-0000-000000000002'),
        ('NB-COM-007', 43,'25000000-0000-0000-0000-000000000002','Limpieza de ventilación.','Bajó la temperatura de trabajo.','B0000000-0000-0000-0000-000000000002'),
        ('NB-COM-007',200,'25000000-0000-0000-0000-000000000001','Preventivo anual.','Sin observaciones.','B0000000-0000-0000-0000-000000000004'),
        ('IMP-REC-002',260,'25000000-0000-0000-0000-000000000001','Preventivo: limpieza de rodillos.','Sin observaciones.','B0000000-0000-0000-0000-000000000004'),
        ('PC-ADM-021', 16,'25000000-0000-0000-0000-000000000002','Reinstalación de Office.','Resuelto.','B0000000-0000-0000-0000-000000000003'),
        ('SRV-CEN-001', 95,'25000000-0000-0000-0000-000000000001','Preventivo trimestral: limpieza y verificación de discos.','Un disco con SMART degradado, se recomienda reemplazo.','B0000000-0000-0000-0000-000000000002'),
        ('RTR-CEN-001', 29,'25000000-0000-0000-0000-000000000002','Actualización de firmware.','Se estabilizó el enlace.','B0000000-0000-0000-0000-000000000003'),
        ('UPS-CEN-001', 47,'25000000-0000-0000-0000-000000000002','Diagnóstico de batería.','Batería al 40% de su capacidad, se sugiere reemplazo.','B0000000-0000-0000-0000-000000000002'),
        ('NB-GER-001',  90,'25000000-0000-0000-0000-000000000001','Preventivo semestral.','Sin observaciones.','B0000000-0000-0000-0000-000000000004')
        ) v(codigo, dias_atras, tipo, descripcion, resultado, tecnico)
    )
    INSERT INTO dbo.Mantenimiento (id_mantenimiento, id_organizacion, id_equipo, id_tipo_mantenimiento,
                                   id_tecnico, fecha, descripcion, resultado)
    SELECT NEWID(), @org, e.id_equipo, CAST(m.tipo AS UNIQUEIDENTIFIER),
           CAST(m.tecnico AS UNIQUEIDENTIFIER),
           DATEADD(DAY, -m.dias_atras, SYSDATETIME()), m.descripcion, m.resultado
    FROM m
    JOIN dbo.Equipo e ON e.codigo = m.codigo AND e.id_organizacion = @org;

    PRINT 'PredictIT_Negocio: historial técnico de demo generado.';
END
ELSE
    PRINT 'PredictIT_Negocio: ya había incidencias cargadas, no se regeneró el historial.';
GO

/* --------------------------------------------- planes y agenda de preventivo

   Los planes vienen cargados en la demostracion porque sin plan la agenda
   esta vacia, y una agenda vacia no muestra nada de lo que el modulo hace.

   Las fechas son relativas a SYSDATETIME() y no fijas: el archivo se aplica
   en cualquier momento y tiene que dejar siempre lo mismo a la vista, dos
   trabajos vencidos y unos cuantos por venir. Con fechas fijas, la demo
   envejece y a los tres meses esta todo vencido.

   El bloque tiene su propio guard: los planes se pueden cargar aunque el
   historial tecnico ya estuviera, que es el caso de una base que venia de
   antes de que existiera el modulo. */
DECLARE @org UNIQUEIDENTIFIER = 'A0000000-0000-0000-0000-000000000001';

IF NOT EXISTS (SELECT 1 FROM dbo.PlanMantenimiento WHERE id_organizacion = @org)
BEGIN
    DECLARE @preventivo UNIQUEIDENTIFIER = '25000000-0000-0000-0000-000000000001';
    DECLARE @planNotebook  UNIQUEIDENTIFIER = '26000000-0000-0000-0000-000000000001';
    DECLARE @planServidor  UNIQUEIDENTIFIER = '26000000-0000-0000-0000-000000000002';
    DECLARE @planImpresora UNIQUEIDENTIFIER = '26000000-0000-0000-0000-000000000003';

    INSERT INTO dbo.PlanMantenimiento
        (id_plan, id_organizacion, id_equipo, id_tipo_equipo, id_tipo_mantenimiento,
         cada_dias, activo, descripcion)
    VALUES
        -- Por tipo: cubre todo el parque de esa clase, incluidos los equipos
        -- que se den de alta despues.
        (@planNotebook, @org, NULL, '20000000-0000-0000-0000-000000000002', @preventivo,
         180, 1, 'Limpieza interna, revisión de batería y actualización de firmware.'),
        (@planServidor, @org, NULL, '20000000-0000-0000-0000-000000000003', @preventivo,
          90, 1, 'Limpieza, verificación de discos y prueba de respaldo.'),
        -- Por equipo: el caso puntual. La impresora de recepcion es la que mas
        -- se usa y lleva su propio plan, mas seguido que el resto.
        (@planImpresora, @org,
         (SELECT id_equipo FROM dbo.Equipo WHERE codigo = 'IMP-REC-002' AND id_organizacion = @org),
         NULL, @preventivo, 120, 1, 'Limpieza de rodillos y calibración.');

    /* La agenda. Se siembra explicita y no se deja que la genere el sistema:
       generandola quedarian todas las fechas el mismo dia —el plan mide desde
       el ultimo preventivo, y en la demo casi ninguno lo tuvo—, y lo que hay
       que mostrar es una agenda repartida, con algo vencido y algo por venir. */
    ;WITH a(codigo, dias, plan_id) AS (
        SELECT * FROM (VALUES
        -- Vencidos: es lo que la pantalla tiene que hacer ver primero.
        ('IMP-REC-002', -34, '26000000-0000-0000-0000-000000000003'),
        ('SRV-CEN-001',  -9, '26000000-0000-0000-0000-000000000002'),
        -- Esta semana.
        ('NB-COM-007',    2, '26000000-0000-0000-0000-000000000001'),
        ('NB-ADM-009',    5, '26000000-0000-0000-0000-000000000001'),
        ('NB-GER-001',    6, '26000000-0000-0000-0000-000000000001'),
        -- Mas adelante.
        ('NB-GEN-016',   19, '26000000-0000-0000-0000-000000000001'),
        ('NB-GEN-017',   26, '26000000-0000-0000-0000-000000000001'),
        ('NB-GEN-018',   33, '26000000-0000-0000-0000-000000000001'),
        ('NB-GEN-020',   47, '26000000-0000-0000-0000-000000000001'),
        ('NB-GEN-021',   61, '26000000-0000-0000-0000-000000000001')
        ) v(codigo, dias, plan_id)
    )
    INSERT INTO dbo.MantenimientoProgramado
        (id_programado, id_organizacion, id_plan, id_equipo, id_tipo_mantenimiento,
         fecha_programada, estado)
    SELECT NEWID(), @org, CAST(a.plan_id AS UNIQUEIDENTIFIER), e.id_equipo, @preventivo,
           DATEADD(DAY, a.dias, CAST(SYSDATETIME() AS DATE)), 'PROGRAMADO'
    FROM a
    JOIN dbo.Equipo e ON e.codigo = a.codigo AND e.id_organizacion = @org;

    /* Uno ya ejecutado, enlazado al mantenimiento que lo cerro: es lo que
       muestra que la agenda y el historial son el mismo hecho visto desde dos
       lados, y no dos registros que alguien tiene que mantener a mano. */
    INSERT INTO dbo.MantenimientoProgramado
        (id_programado, id_organizacion, id_plan, id_equipo, id_tipo_mantenimiento,
         fecha_programada, estado, id_mantenimiento)
    SELECT TOP 1 NEWID(), @org, @planServidor, m.id_equipo, @preventivo,
           CAST(m.fecha AS DATE), 'EJECUTADO', m.id_mantenimiento
    FROM dbo.Mantenimiento m
    JOIN dbo.Equipo e ON e.id_equipo = m.id_equipo
    WHERE m.id_organizacion = @org
      AND e.codigo = 'SRV-CEN-001'
      AND m.id_tipo_mantenimiento = @preventivo
    ORDER BY m.fecha DESC;

    PRINT 'PredictIT_Negocio: planes de mantenimiento y agenda de demo cargados.';
END
ELSE
    PRINT 'PredictIT_Negocio: ya había planes cargados, no se regeneró la agenda.';
GO

/* ------------------------------------------ comprobantes del servicio (RF-17)

   La demostracion arranca con tres meses ya facturados: uno cobrado, uno
   emitido e impago —vencido, que es lo que el panel tiene que hacer ver— y el
   ultimo mes cerrado todavia en borrador, para que se pueda mostrar el paso de
   emitir sin tener que generar nada antes.

   Los periodos son relativos al mes en curso y no fechas fijas: con fechas
   fijas la demo envejece y a los seis meses muestra un historial que no llega
   hasta hoy.

   Los importes salen del plan de la organizacion y del parque real, igual que
   los calcula el sistema. No se escriben a mano: si el precio del plan cambia
   en el seed, estos comprobantes cambian con el. */
DECLARE @orgF UNIQUEIDENTIFIER = 'A0000000-0000-0000-0000-000000000001';

IF NOT EXISTS (SELECT 1 FROM dbo.Comprobante WHERE id_organizacion = @orgF)
BEGIN
    DECLARE @hoy DATE = CAST(SYSDATETIME() AS DATE);

    DECLARE @idPlanF     UNIQUEIDENTIFIER,
            @abono       DECIMAL(14,2),
            @incluidos   INT,
            @adicional   DECIMAL(14,2),
            @nombrePlan  VARCHAR(60);

    SELECT @idPlanF = p.id_plan, @abono = p.abono_mensual,
           @incluidos = p.equipos_incluidos, @adicional = p.precio_equipo_adicional,
           @nombrePlan = p.nombre
    FROM dbo.Organizacion o
    JOIN dbo.PlanComercial p ON p.id_plan = o.id_plan
    WHERE o.id_organizacion = @orgF;

    IF @idPlanF IS NOT NULL
    BEGIN
        DECLARE @equipos INT = (
            SELECT COUNT(*)
            FROM dbo.Equipo e
            JOIN dbo.EstadoEquipo ee ON ee.id_estado_equipo = e.id_estado_equipo
            WHERE e.id_organizacion = @orgF AND ee.nombre <> 'Dado de baja');

        DECLARE @adicionales INT = CASE WHEN @equipos > @incluidos THEN @equipos - @incluidos ELSE 0 END;
        DECLARE @costoAdic DECIMAL(14,2) = @adicionales * @adicional;
        DECLARE @sub DECIMAL(14,2) = @abono + @costoAdic;
        DECLARE @iva DECIMAL(14,2) = ROUND(@sub * 0.21, 2);

        /* Tres periodos: el mes cerrado y los dos anteriores. Los meses se
           mueven con DATEADD sobre el dia uno, que es la unica forma de cruzar
           el fin de anio sin hacer la cuenta a mano. */
        ;WITH c(meses, estado, numero, dias_emision, dias_pago) AS (
            SELECT * FROM (VALUES
                (-3, 'PAGADO',   1, 62, 55),   -- cobrado en termino
                (-2, 'EMITIDO',  2, 32, NULL), -- emitido e impago: vencido
                (-1, 'BORRADOR', NULL, NULL, NULL)
            ) v(meses, estado, numero, dias_emision, dias_pago)
        )
        INSERT INTO dbo.Comprobante
            (id_comprobante, id_organizacion, periodo, numero, id_plan,
             equipos_administrados, equipos_incluidos, subtotal, alicuota_iva,
             iva, total, estado, fecha_emision, fecha_vencimiento, fecha_pago)
        SELECT NEWID(), @orgF,
               FORMAT(DATEADD(MONTH, c.meses, @hoy), 'yyyy-MM'),
               c.numero, @idPlanF, @equipos, @incluidos, @sub, 21, @iva, @sub + @iva,
               c.estado,
               CASE WHEN c.dias_emision IS NOT NULL THEN DATEADD(DAY, -c.dias_emision, @hoy) END,
               CASE WHEN c.dias_emision IS NOT NULL THEN DATEADD(DAY, -c.dias_emision + 10, @hoy) END,
               CASE WHEN c.dias_pago IS NOT NULL THEN DATEADD(DAY, -c.dias_pago, @hoy) END
        FROM c;

        /* El desglose de cada uno: el abono siempre, y los equipos adicionales
           solo si los hay. Una linea de cero adicionales diria algo que no
           paso. */
        INSERT INTO dbo.LineaComprobante
            (id_linea, id_comprobante, orden, concepto, cantidad, precio_unitario, importe, es_ajuste)
        SELECT NEWID(), k.id_comprobante, 1,
               'Abono ' + @nombrePlan + ' — ' + k.periodo, 1, @abono, @abono, 0
        FROM dbo.Comprobante k
        WHERE k.id_organizacion = @orgF;

        IF @adicionales > 0
        INSERT INTO dbo.LineaComprobante
            (id_linea, id_comprobante, orden, concepto, cantidad, precio_unitario, importe, es_ajuste)
        SELECT NEWID(), k.id_comprobante, 2,
               'Equipos adicionales (' + CAST(@equipos AS VARCHAR(10)) + ' administrados, '
                                       + CAST(@incluidos AS VARCHAR(10)) + ' incluidos)',
               @adicionales, @adicional, @costoAdic, 0
        FROM dbo.Comprobante k
        WHERE k.id_organizacion = @orgF;

        PRINT 'PredictIT_Negocio: comprobantes de demo cargados.';
    END
    ELSE
        PRINT 'PredictIT_Negocio: la organizacion no tiene plan, no se generaron comprobantes.';
END
ELSE
    PRINT 'PredictIT_Negocio: ya había comprobantes cargados, no se regeneraron.';
GO

PRINT 'PredictIT_Negocio: datos iniciales cargados.';
GO
