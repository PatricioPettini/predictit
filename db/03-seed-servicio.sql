/* =========================================================================
   PredictIT — Datos iniciales de la base de SERVICIO
   Idiomas, patentes, familias, roles, tipos de evento y usuarios de demo.

   Los GUID son fijos y legibles a propósito: hacen falta para referenciar
   usuarios desde la base de negocio (donde no hay FK posible) y para que el
   seed sea idempotente.

     A0000000-…  Organización (vive en PredictIT_Negocio)
     B0000000-…  Usuario          C0000000-…  Rol
     D0000000-…  Familia          E0000000-…  Patente
     F0000000-…  TipoEvento       10000000-…  Idioma
     20000000-…  Bitácora
   ========================================================================= */

USE [PredictIT_Servicio];
GO
SET NOCOUNT ON;
GO

/* ---------------------------------------------------------------- Idiomas */
MERGE dbo.Idioma AS d
USING (VALUES
    ('10000000-0000-0000-0000-000000000001','es-AR','Español (Argentina)',1,1),
    ('10000000-0000-0000-0000-000000000002','en-US','English (United States)',1,0)
) AS s(id,codigo,nombre,activo,es_default)
ON d.id_idioma = CAST(s.id AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN
    INSERT (id_idioma, codigo, nombre, activo, es_default)
    VALUES (CAST(s.id AS UNIQUEIDENTIFIER), s.codigo, s.nombre, s.activo, s.es_default);
GO

/* --------------------------------------------------------------- Patentes */
MERGE dbo.Patente AS d
USING (VALUES
    ('E0000000-0000-0000-0000-000000000001','Ver equipos',                      'EQUIPO_VER',                   'ACTIVOS', 1),
    ('E0000000-0000-0000-0000-000000000002','Gestionar equipos',                'EQUIPO_GESTIONAR',             'ACTIVOS', 2),
    ('E0000000-0000-0000-0000-000000000003','Cambiar estado de un equipo',      'EQUIPO_CAMBIAR_ESTADO',        'ACTIVOS', 2),
    ('E0000000-0000-0000-0000-000000000010','Ver todas las incidencias',        'INCIDENCIA_VER_TODAS',         'INCIDENCIAS', 1),
    ('E0000000-0000-0000-0000-000000000011','Ver las incidencias propias',      'INCIDENCIA_VER_PROPIAS',       'INCIDENCIAS', 1),
    ('E0000000-0000-0000-0000-000000000012','Registrar incidencias',            'INCIDENCIA_REGISTRAR',         'INCIDENCIAS', 2),
    ('E0000000-0000-0000-0000-000000000013','Atender incidencias',              'INCIDENCIA_ATENDER',           'INCIDENCIAS', 2),
    ('E0000000-0000-0000-0000-000000000014','Reasignar incidencias propias',    'INCIDENCIA_REASIGNAR_PROPIAS', 'INCIDENCIAS', 2),
    ('E0000000-0000-0000-0000-000000000015','Reasignar cualquier incidencia',   'INCIDENCIA_REASIGNAR_TODAS',   'INCIDENCIAS', 2),
    ('E0000000-0000-0000-0000-000000000020','Ver mantenimientos',               'MANTENIMIENTO_VER',            'MANTENIMIENTOS', 1),
    ('E0000000-0000-0000-0000-000000000021','Registrar mantenimientos',         'MANTENIMIENTO_REGISTRAR',      'MANTENIMIENTOS', 2),
    ('E0000000-0000-0000-0000-000000000022','Planificar mantenimientos',        'MANTENIMIENTO_PLANIFICAR',     'MANTENIMIENTOS', 2),
    ('E0000000-0000-0000-0000-000000000030','Ver historial de la organización', 'HISTORIAL_VER_ORGANIZACION',   'HISTORIAL', 1),
    ('E0000000-0000-0000-0000-000000000031','Ver historial de equipos propios', 'HISTORIAL_VER_PROPIO',         'HISTORIAL', 1),
    ('E0000000-0000-0000-0000-000000000040','Ver alertas predictivas',          'ALERTA_VER',                   'PREDICTIVO', 1),
    ('E0000000-0000-0000-0000-000000000041','Gestionar reglas predictivas',     'REGLA_GESTIONAR',              'PREDICTIVO', 3),
    ('E0000000-0000-0000-0000-000000000050','Ver el dashboard',                 'DASHBOARD_VER',                'DASHBOARD', 1),
    ('E0000000-0000-0000-0000-000000000051','Generar reportes',                 'REPORTE_GENERAR',              'REPORTES', 1),
    ('E0000000-0000-0000-0000-000000000060','Gestionar usuarios',               'USUARIO_GESTIONAR',            'SEGURIDAD', 3),
    ('E0000000-0000-0000-0000-000000000061','Gestionar roles y permisos',       'ROL_GESTIONAR',                'SEGURIDAD', 3),
    ('E0000000-0000-0000-0000-000000000070','Gestionar la organización',        'ORGANIZACION_GESTIONAR',       'CONFIGURACION', 3),
    ('E0000000-0000-0000-0000-000000000071','Configurar la integración de IA',  'IA_CONFIGURAR',                'CONFIGURACION', 3),
    ('E0000000-0000-0000-0000-000000000072','Consultar la bitácora',            'BITACORA_VER',                 'CONFIGURACION', 1),
    ('E0000000-0000-0000-0000-000000000073','Gestionar respaldos',              'BACKUP_GESTIONAR',             'CONFIGURACION', 3),
    ('E0000000-0000-0000-0000-000000000074','Gestionar idiomas y traducciones', 'IDIOMA_GESTIONAR',             'CONFIGURACION', 3),
    ('E0000000-0000-0000-0000-000000000075','Ver errores del sistema',          'ERROR_VER',                    'CONFIGURACION', 1),
    ('E0000000-0000-0000-0000-000000000076','Ver la facturación del servicio',  'FACTURACION_VER',              'FACTURACION', 1),
    ('E0000000-0000-0000-0000-000000000077','Gestionar la facturación',         'FACTURACION_GESTIONAR',        'FACTURACION', 3),
    ('E0000000-0000-0000-0000-000000000080','Cambiar de organización',          'ORGANIZACION_CAMBIAR',         'PARTNER', 2)
) AS s(id,nombre,data_key,modulo,tipo_acceso)
ON d.id_patente = CAST(s.id AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN
    INSERT (id_patente, nombre, data_key, modulo, tipo_acceso)
    VALUES (CAST(s.id AS UNIQUEIDENTIFIER), s.nombre, s.data_key, s.modulo, s.tipo_acceso)
WHEN MATCHED THEN
    UPDATE SET nombre = s.nombre, data_key = s.data_key, modulo = s.modulo,
               tipo_acceso = s.tipo_acceso;
GO

/* --------------------------------------------------------------- Familias */
MERGE dbo.Familia AS d
USING (VALUES
    ('D0000000-0000-0000-0000-000000000001','CONSULTA_TECNICA',    'Lectura del parque, historial, alertas y dashboard'),
    ('D0000000-0000-0000-0000-000000000002','OPERACION_TECNICA',   'Atención de incidencias y registro de mantenimientos'),
    ('D0000000-0000-0000-0000-000000000003','ADMINISTRACION',      'Configuración del sistema, usuarios y motor predictivo'),
    ('D0000000-0000-0000-0000-000000000004','AUTOGESTION_CLIENTE', 'Módulo del cliente: reportar y seguir incidencias propias')
) AS s(id,nombre,descripcion)
ON d.id_familia = CAST(s.id AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN
    INSERT (id_familia, nombre, descripcion)
    VALUES (CAST(s.id AS UNIQUEIDENTIFIER), s.nombre, s.descripcion);
GO

/* Familia dentro de familia: OPERACION_TECNICA hereda todo lo de CONSULTA_TECNICA.
   Es el caso que ejercita la recursión del Composite en el SeguridadService. */
MERGE dbo.Familia_Familia AS d
USING (VALUES
    ('D0000000-0000-0000-0000-000000000002','D0000000-0000-0000-0000-000000000001')
) AS s(padre,hijo)
ON d.id_familia = CAST(s.padre AS UNIQUEIDENTIFIER) AND d.id_familia_hijo = CAST(s.hijo AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN
    INSERT (id_familia, id_familia_hijo)
    VALUES (CAST(s.padre AS UNIQUEIDENTIFIER), CAST(s.hijo AS UNIQUEIDENTIFIER));
GO

MERGE dbo.Familia_Patente AS d
USING (VALUES
    -- CONSULTA_TECNICA
    ('D0000000-0000-0000-0000-000000000001','E0000000-0000-0000-0000-000000000001'),
    ('D0000000-0000-0000-0000-000000000001','E0000000-0000-0000-0000-000000000030'),
    ('D0000000-0000-0000-0000-000000000001','E0000000-0000-0000-0000-000000000040'),
    ('D0000000-0000-0000-0000-000000000001','E0000000-0000-0000-0000-000000000050'),
    ('D0000000-0000-0000-0000-000000000001','E0000000-0000-0000-0000-000000000020'),
    -- OPERACION_TECNICA
    ('D0000000-0000-0000-0000-000000000002','E0000000-0000-0000-0000-000000000003'),
    ('D0000000-0000-0000-0000-000000000002','E0000000-0000-0000-0000-000000000010'),
    ('D0000000-0000-0000-0000-000000000002','E0000000-0000-0000-0000-000000000012'),
    ('D0000000-0000-0000-0000-000000000002','E0000000-0000-0000-0000-000000000013'),
    ('D0000000-0000-0000-0000-000000000002','E0000000-0000-0000-0000-000000000014'),
    ('D0000000-0000-0000-0000-000000000002','E0000000-0000-0000-0000-000000000021'),
    ('D0000000-0000-0000-0000-000000000002','E0000000-0000-0000-0000-000000000051'),
    -- ADMINISTRACION
    ('D0000000-0000-0000-0000-000000000003','E0000000-0000-0000-0000-000000000002'),
    ('D0000000-0000-0000-0000-000000000003','E0000000-0000-0000-0000-000000000015'),
    ('D0000000-0000-0000-0000-000000000003','E0000000-0000-0000-0000-000000000022'),
    ('D0000000-0000-0000-0000-000000000003','E0000000-0000-0000-0000-000000000041'),
    ('D0000000-0000-0000-0000-000000000003','E0000000-0000-0000-0000-000000000060'),
    ('D0000000-0000-0000-0000-000000000003','E0000000-0000-0000-0000-000000000061'),
    ('D0000000-0000-0000-0000-000000000003','E0000000-0000-0000-0000-000000000070'),
    ('D0000000-0000-0000-0000-000000000003','E0000000-0000-0000-0000-000000000071'),
    ('D0000000-0000-0000-0000-000000000003','E0000000-0000-0000-0000-000000000072'),
    ('D0000000-0000-0000-0000-000000000003','E0000000-0000-0000-0000-000000000073'),
    ('D0000000-0000-0000-0000-000000000003','E0000000-0000-0000-0000-000000000074'),
    ('D0000000-0000-0000-0000-000000000003','E0000000-0000-0000-0000-000000000075'),
    ('D0000000-0000-0000-0000-000000000003','E0000000-0000-0000-0000-000000000076'),
    ('D0000000-0000-0000-0000-000000000003','E0000000-0000-0000-0000-000000000077'),
    -- AUTOGESTION_CLIENTE
    ('D0000000-0000-0000-0000-000000000004','E0000000-0000-0000-0000-000000000011'),
    ('D0000000-0000-0000-0000-000000000004','E0000000-0000-0000-0000-000000000012'),
    ('D0000000-0000-0000-0000-000000000004','E0000000-0000-0000-0000-000000000031')
) AS s(fam,pat)
ON d.id_familia = CAST(s.fam AS UNIQUEIDENTIFIER) AND d.id_patente = CAST(s.pat AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN
    INSERT (id_familia, id_patente)
    VALUES (CAST(s.fam AS UNIQUEIDENTIFIER), CAST(s.pat AS UNIQUEIDENTIFIER));
GO

/* ------------------------------------------------------------------ Roles
   Los tres perfiles de la sección 10.3.2.4.1, más PARTNER (Segmento C). */
MERGE dbo.Rol AS d
USING (VALUES
    ('C0000000-0000-0000-0000-000000000001','ADMINISTRADOR',       'Acceso completo a la organización',1),
    ('C0000000-0000-0000-0000-000000000002','RESPONSABLE_TECNICO', 'Atiende incidencias y registra mantenimientos',1),
    ('C0000000-0000-0000-0000-000000000003','USUARIO_SOLICITANTE', 'Módulo del cliente: reporta y sigue sus incidencias',1),
    ('C0000000-0000-0000-0000-000000000004','PARTNER',             'Proveedor de soporte que gestiona varias organizaciones',1)
) AS s(id,nombre,descripcion,sistema)
ON d.id_rol = CAST(s.id AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN
    INSERT (id_rol, nombre, descripcion, es_sistema)
    VALUES (CAST(s.id AS UNIQUEIDENTIFIER), s.nombre, s.descripcion, s.sistema);
GO

MERGE dbo.Rol_Familia AS d
USING (VALUES
    ('C0000000-0000-0000-0000-000000000001','D0000000-0000-0000-0000-000000000003'),
    ('C0000000-0000-0000-0000-000000000001','D0000000-0000-0000-0000-000000000002'),
    ('C0000000-0000-0000-0000-000000000002','D0000000-0000-0000-0000-000000000002'),
    ('C0000000-0000-0000-0000-000000000003','D0000000-0000-0000-0000-000000000004'),
    ('C0000000-0000-0000-0000-000000000004','D0000000-0000-0000-0000-000000000003'),
    ('C0000000-0000-0000-0000-000000000004','D0000000-0000-0000-0000-000000000002')
) AS s(rol,fam)
ON d.id_rol = CAST(s.rol AS UNIQUEIDENTIFIER) AND d.id_familia = CAST(s.fam AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN
    INSERT (id_rol, id_familia)
    VALUES (CAST(s.rol AS UNIQUEIDENTIFIER), CAST(s.fam AS UNIQUEIDENTIFIER));
GO

/* Patente suelta, fuera de toda familia: sólo el Partner puede cambiar de
   organización. Es el caso que justifica que Rol_Patente exista. */
MERGE dbo.Rol_Patente AS d
USING (VALUES
    ('C0000000-0000-0000-0000-000000000004','E0000000-0000-0000-0000-000000000080')
) AS s(rol,pat)
ON d.id_rol = CAST(s.rol AS UNIQUEIDENTIFIER) AND d.id_patente = CAST(s.pat AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN
    INSERT (id_rol, id_patente)
    VALUES (CAST(s.rol AS UNIQUEIDENTIFIER), CAST(s.pat AS UNIQUEIDENTIFIER));
GO

/* -------------------------------------------------------- Tipos de evento */
MERGE dbo.TipoEvento AS d
USING (VALUES
    ('F0000000-0000-0000-0000-000000000001','LOGIN_OK',            'Inicio de sesión exitoso',           'INFO'),
    ('F0000000-0000-0000-0000-000000000002','LOGIN_FALLIDO',       'Intento de acceso fallido',          'ADVERTENCIA'),
    ('F0000000-0000-0000-0000-000000000003','LOGOUT',              'Cierre de sesión',                   'INFO'),
    ('F0000000-0000-0000-0000-000000000004','ACCESO_DENEGADO',     'Acceso denegado por falta de permiso','ADVERTENCIA'),
    ('F0000000-0000-0000-0000-000000000010','ALTA',                'Alta de una entidad',                'INFO'),
    ('F0000000-0000-0000-0000-000000000011','MODIFICACION',        'Modificación de una entidad',        'INFO'),
    ('F0000000-0000-0000-0000-000000000012','BAJA',                'Baja de una entidad',                'ADVERTENCIA'),
    ('F0000000-0000-0000-0000-000000000020','CLASIFICACION_IA',    'Clasificación automática de incidencia','INFO'),
    ('F0000000-0000-0000-0000-000000000021','ASIGNACION_IA',       'Asignación recomendada por IA',      'INFO'),
    ('F0000000-0000-0000-0000-000000000022','ASIGNACION_RESPALDO', 'Asignación de respaldo por falla del servicio de IA','ADVERTENCIA'),
    ('F0000000-0000-0000-0000-000000000023','REASIGNACION_MANUAL', 'Reasignación manual de incidencia',  'INFO'),
    ('F0000000-0000-0000-0000-000000000030','REGLA_MODIFICADA',    'Cambio en una regla del motor predictivo','ADVERTENCIA'),
    ('F0000000-0000-0000-0000-000000000031','ALERTA_GENERADA',     'Alerta predictiva generada',         'INFO'),
    ('F0000000-0000-0000-0000-000000000040','BACKUP',              'Respaldo de base de datos',          'INFO'),
    ('F0000000-0000-0000-0000-000000000041','RESTORE',             'Restauración de base de datos',      'CRITICO'),
    ('F0000000-0000-0000-0000-000000000050','ERROR_SISTEMA',       'Excepción no controlada',            'ERROR'),
    -- Eventos de negocio. Sin estas filas el evento se descarta en silencio:
    -- BitacoraService resuelve el código contra este catálogo y, si no lo
    -- encuentra, avisa al logger y no escribe.
    ('F0000000-0000-0000-0000-000000000027','ASIGNACION_HEURISTICA','Asignación resuelta por heurística, sin IA','INFO'),
    ('F0000000-0000-0000-0000-000000000028','CLASIFICACION_HEURISTICA','Clasificación resuelta por heurística, sin IA','INFO'),
    ('F0000000-0000-0000-0000-00000000002E','GUIA_REPARACION_IA','Guía de reparación consultada a la IA','INFO'),
    ('F0000000-0000-0000-0000-00000000002F','GUIA_REPARACION_HEURISTICA','Guía de reparación resuelta por heurística, sin IA','INFO'),
    ('F0000000-0000-0000-0000-000000000024','INCIDENCIA_ALTA',     'Alta de una incidencia',             'INFO'),
    ('F0000000-0000-0000-0000-000000000025','INCIDENCIA_SIN_ASIGNAR','Incidencia registrada sin técnico asignado','ADVERTENCIA'),
    ('F0000000-0000-0000-0000-000000000026','MANTENIMIENTO_ALTA',  'Alta de un mantenimiento',           'INFO'),
    ('F0000000-0000-0000-0000-000000000051','MANTENIMIENTO_PROGRAMADO','Mantenimiento programado',       'INFO'),
    ('F0000000-0000-0000-0000-000000000052','MANTENIMIENTO_REPROGRAMADO','Mantenimiento reprogramado',   'INFO'),
    ('F0000000-0000-0000-0000-000000000053','MANTENIMIENTO_ANULADO','Mantenimiento programado anulado',  'INFO'),
    ('F0000000-0000-0000-0000-000000000054','COMPROBANTE_GENERADO','Comprobante del servicio generado',  'INFO'),
    ('F0000000-0000-0000-0000-000000000055','COMPROBANTE_AJUSTADO','Ajuste cargado sobre un comprobante','INFO'),
    ('F0000000-0000-0000-0000-000000000056','COMPROBANTE_EMITIDO', 'Comprobante emitido',                'INFO'),
    ('F0000000-0000-0000-0000-000000000057','COMPROBANTE_PAGADO',  'Pago de un comprobante registrado',  'INFO'),
    ('F0000000-0000-0000-0000-000000000058','COMPROBANTE_ANULADO', 'Comprobante anulado',                'ADVERTENCIA'),
    ('F0000000-0000-0000-0000-000000000032','ANALISIS_PREDICTIVO', 'Evaluación del motor predictivo',    'INFO'),
    ('F0000000-0000-0000-0000-000000000033','ALERTA_ATENDIDA',     'Alerta predictiva atendida o descartada','INFO'),
    -- Ciclo de vida de la incidencia. Sin estos codigos el avance de una
    -- incidencia no queda auditable, y el avance es justamente lo que el
    -- tecnico hace todo el dia.
    ('F0000000-0000-0000-0000-000000000029','INCIDENCIA_ATENDIDA', 'Avance en la atencion de una incidencia','INFO'),
    ('F0000000-0000-0000-0000-00000000002A','INCIDENCIA_RESUELTA', 'Incidencia resuelta',                'INFO'),
    ('F0000000-0000-0000-0000-00000000002B','INCIDENCIA_CERRADA',  'Incidencia cerrada',                 'INFO'),
    ('F0000000-0000-0000-0000-00000000002C','INCIDENCIA_ANULADA',  'Incidencia anulada',                 'ADVERTENCIA'),
    ('F0000000-0000-0000-0000-00000000002D','EQUIPO_CAMBIO_ESTADO','Cambio de estado operativo de un equipo','INFO'),
    -- Un reporte impreso circula fuera del sistema: saber que salio de aca,
    -- cuando y con que recorte es lo unico que despues permite explicarlo.
    ('F0000000-0000-0000-0000-000000000042','REPORTE_GENERADO',    'Reporte exportado a PDF',            'INFO')
) AS s(id,codigo,nombre,criticidad)
ON d.id_tipo_evento = CAST(s.id AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN
    INSERT (id_tipo_evento, codigo, nombre, criticidad)
    VALUES (CAST(s.id AS UNIQUEIDENTIFIER), s.codigo, s.nombre, s.criticidad);
GO

/* --------------------------------------------------------------- Usuarios
   Contraseñas de demo (PBKDF2-SHA256, 600.000 iteraciones, la
   recomendación de OWASP):
     admin / Admin.2026      · tecnico1..3 / Tecnico.2026
     solicitante / Usuario.2026   · partner / Partner.2026            */
MERGE dbo.Usuario AS d
USING (VALUES
    ('B0000000-0000-0000-0000-000000000001','Patricio','Pettini','admin@predictit.test','admin',
     'PBKDF2$600000$8szEPBPL0l/NNkT446YwKw==$xYCbucBPtOZb0EaZYNaYilY2+Bvvm7AsQqB/TFajmyY=',
     'A0000000-0000-0000-0000-000000000001'),
    ('B0000000-0000-0000-0000-000000000002','Martín','Acosta','martin.acosta@predictit.test','tecnico1',
     'PBKDF2$600000$WoAIl/N4/SWazt/GA/hP9g==$nMJVwKrtTEGe06ANA5aoiBbOFy02fkZ4VCmAuQhl3+g=',
     'A0000000-0000-0000-0000-000000000001'),
    ('B0000000-0000-0000-0000-000000000003','Julieta','Pérez','julieta.perez@predictit.test','tecnico2',
     'PBKDF2$600000$0qiF8nVZfVqnU1Fa+/Ac4Q==$xVS+Bh9FQRHxgmeus8DliTz/7PnhLAU9nBw1rlzJj/U=',
     'A0000000-0000-0000-0000-000000000001'),
    ('B0000000-0000-0000-0000-000000000004','Lucas','Romano','lucas.romano@predictit.test','tecnico3',
     'PBKDF2$600000$WEebtHujChiT8Oh9cfcTAw==$Qe0L8zSdUmBB9MkF7q8lAuxzGA8k9bTP8V+dUoAeKWo=',
     'A0000000-0000-0000-0000-000000000001'),
    ('B0000000-0000-0000-0000-000000000005','Gabriela','Suárez','gabriela.suarez@predictit.test','solicitante',
     'PBKDF2$600000$WEBpAfbJIPC7XFE/R2DzZg==$Wjs0HLPo3+4OAHVmwc+1wDOlS8ogY9jIQTfL6mopPl8=',
     'A0000000-0000-0000-0000-000000000001'),
    -- El Partner no pertenece a una organización: trabaja sobre varias.
    ('B0000000-0000-0000-0000-000000000006','Diego','Ferrari','diego.ferrari@soportepyme.test','partner',
     'PBKDF2$600000$xX3i3/kG+05iNd0cLP7QAQ==$GZnxI9vC3CjYPZ7qnts3ZwRntCDBzVrHKFOX4swF/cE=',
     NULL)
) AS s(id,nombre,apellido,email,username,pwd,org)
ON d.id_usuario = CAST(s.id AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN
    INSERT (id_usuario, nombre, apellido, email, username, [password], id_organizacion, id_idioma)
    VALUES (CAST(s.id AS UNIQUEIDENTIFIER), s.nombre, s.apellido, s.email, s.username, s.pwd,
            CAST(s.org AS UNIQUEIDENTIFIER), '10000000-0000-0000-0000-000000000001');
GO

MERGE dbo.Usuario_Rol AS d
USING (VALUES
    ('B0000000-0000-0000-0000-000000000001','C0000000-0000-0000-0000-000000000001'),
    ('B0000000-0000-0000-0000-000000000002','C0000000-0000-0000-0000-000000000002'),
    ('B0000000-0000-0000-0000-000000000003','C0000000-0000-0000-0000-000000000002'),
    ('B0000000-0000-0000-0000-000000000004','C0000000-0000-0000-0000-000000000002'),
    ('B0000000-0000-0000-0000-000000000005','C0000000-0000-0000-0000-000000000003'),
    ('B0000000-0000-0000-0000-000000000006','C0000000-0000-0000-0000-000000000004')
) AS s(usr,rol)
ON d.id_usuario = CAST(s.usr AS UNIQUEIDENTIFIER) AND d.id_rol = CAST(s.rol AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN
    INSERT (id_usuario, id_rol)
    VALUES (CAST(s.usr AS UNIQUEIDENTIFIER), CAST(s.rol AS UNIQUEIDENTIFIER));
GO

MERGE dbo.Usuario_Organizacion AS d
USING (VALUES
    ('B0000000-0000-0000-0000-000000000006','A0000000-0000-0000-0000-000000000001'),
    ('B0000000-0000-0000-0000-000000000006','A0000000-0000-0000-0000-000000000002')
) AS s(usr,org)
ON d.id_usuario = CAST(s.usr AS UNIQUEIDENTIFIER) AND d.id_organizacion = CAST(s.org AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN
    INSERT (id_usuario, id_organizacion)
    VALUES (CAST(s.usr AS UNIQUEIDENTIFIER), CAST(s.org AS UNIQUEIDENTIFIER));
GO

/* --------------------------------------------------------------- Bitácora
   Una base recién creada no tiene bitácora: se llena sola cuando el sistema se
   usa. Eso deja la pantalla de Bitácora y la de Errores vacías en una
   instalación nueva, y deja sin datos a las consultas que las prueban.

   Estas seis filas son el mínimo para que las dos pantallas muestren algo real
   y para que las tres criticidades queden representadas: INFO, ADVERTENCIA y
   ERROR con su traza. Las fechas son relativas al momento de la carga, así que
   caen dentro de los últimos treinta días que la pantalla trae por defecto.

   La tabla es inmutable por disparador —rechaza UPDATE y DELETE—, así que el
   MERGE sólo puede insertar. Con la clave fija, correr el seed de nuevo no
   duplica nada. */
MERGE dbo.Bitacora AS d
USING (VALUES
    -- Inicios de sesión: dan la entrada con usuario que necesita el filtro por
    -- usuario de CU.Arq.002.
    ('20000000-0000-0000-0000-000000000001','B0000000-0000-0000-0000-000000000001',NULL,
     'F0000000-0000-0000-0000-000000000001',-6,
     'Inicio de sesión de admin','Usuario','10.0.0.10',NULL),
    ('20000000-0000-0000-0000-000000000002','B0000000-0000-0000-0000-000000000002',NULL,
     'F0000000-0000-0000-0000-000000000001',-4,
     'Inicio de sesión de tecnico1','Usuario','10.0.0.24',NULL),
    ('20000000-0000-0000-0000-000000000003','B0000000-0000-0000-0000-000000000001',NULL,
     'F0000000-0000-0000-0000-000000000003',-4,
     'Cierre de sesión de admin','Usuario','10.0.0.10',NULL),
    -- Intento fallido: no tiene usuario, tiene lo que se tecleó, y es la
    -- advertencia que la pantalla de errores muestra sólo si se la pide.
    ('20000000-0000-0000-0000-000000000004',NULL,'admin2',
     'F0000000-0000-0000-0000-000000000002',-3,
     'Intento de acceso con un usuario inexistente',NULL,'190.55.12.4',NULL),
    ('20000000-0000-0000-0000-000000000005','B0000000-0000-0000-0000-000000000005',NULL,
     'F0000000-0000-0000-0000-000000000004',-2,
     'Acceso denegado a la pantalla de usuarios por falta de patente','Patente','10.0.0.31',NULL),
    -- Error con traza: es lo único que la vista de errores agrega a la
    -- bitácora, así que sin una fila así esa pantalla no se puede mirar.
    ('20000000-0000-0000-0000-000000000006',NULL,NULL,
     'F0000000-0000-0000-0000-000000000050',-1,
     'Excepción no controlada al generar un reporte','Reporte','10.0.0.10',
     'System.InvalidOperationException: La secuencia no contiene elementos'
     + CHAR(13) + CHAR(10)
     + '   en PredictIT.BLL.Implementations.ReporteBusiness.Generar(FiltroReporteDto)')
) AS s(id,usr,usr_texto,tipo,dias,descripcion,entidad,ip,traza)
ON d.id_bitacora = CAST(s.id AS UNIQUEIDENTIFIER)
WHEN NOT MATCHED THEN
    INSERT (id_bitacora, id_usuario, usuario_texto, id_tipo_evento, fecha,
            descripcion, entidad, id_organizacion, ip, traza)
    VALUES (CAST(s.id AS UNIQUEIDENTIFIER),
            CAST(s.usr AS UNIQUEIDENTIFIER),
            s.usr_texto,
            CAST(s.tipo AS UNIQUEIDENTIFIER),
            DATEADD(DAY, s.dias, SYSDATETIME()),
            s.descripcion,
            s.entidad,
            CAST('A0000000-0000-0000-0000-000000000001' AS UNIQUEIDENTIFIER),
            s.ip,
            s.traza);
GO

PRINT 'PredictIT_Servicio: datos iniciales cargados.';
GO
