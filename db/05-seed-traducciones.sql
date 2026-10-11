/* =====================================================================
   Traducciones de la interfaz (Req. Arq. 001 · CU.Arq.004 Traducir)
   =====================================================================

   Las traducciones viven en la base y no en archivos de recursos del
   frontend. Es una decision con consecuencias:

   - A favor: se pueden corregir sin recompilar ni desplegar, y agregar un
     idioma es cargar filas. El requerimiento habla de "adaptar los textos
     mostrados segun la configuracion establecida", y la configuracion vive
     en la base.
   - En contra: la interfaz necesita una consulta antes de poder dibujar el
     primer texto. Se resuelve trayendo el diccionario completo de una sola
     vez al iniciar sesion y guardandolo en memoria; son unas doscientas
     filas, no un catalogo de miles.

   La clave usa puntos como separador de ambito —`nav.activos`,
   `incidencia.titulo`— para que se pueda ver de un vistazo a que pantalla
   pertenece cada texto.

   Idempotente: el MERGE actualiza el texto si la clave ya existe, asi una
   correccion de redaccion se aplica volviendo a correr el script.
   ===================================================================== */

USE [PredictIT_Servicio];
GO

DECLARE @es UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000001';
DECLARE @en UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000002';

/* Las claves y sus dos textos en una sola tabla derivada: tenerlos al lado
   hace evidente cuando falta una traduccion. */
DECLARE @t TABLE (clave VARCHAR(150), es VARCHAR(1000), en VARCHAR(1000));

INSERT INTO @t (clave, es, en) VALUES
-- ------------------------------------------------------------ aplicacion
('app.nombre',                 'PredictIT',                        'PredictIT'),
('app.bajada',                 'Gestión y mantenimiento predictivo de equipos informáticos',
                               'Predictive management and maintenance of IT assets'),
('app.pie',                    'Trabajo Final de Ingeniería · Universidad Abierta Interamericana',
                               'Final Engineering Project · Universidad Abierta Interamericana'),

-- ----------------------------------------------------------------- login
('login.titulo',               'Iniciar sesión',                   'Sign in'),
('login.usuario',              'Usuario',                          'Username'),
('login.contrasena',           'Contraseña',                       'Password'),
('login.entrar',               'Ingresar',                         'Sign in'),
('login.entrando',             'Ingresando…',                      'Signing in…'),
('login.credenciales',         'Usuario o contraseña incorrectos.','Incorrect username or password.'),

-- ------------------------------------------------------------ navegacion
('nav.dashboard',              'Dashboard',                        'Dashboard'),
('nav.activos',                'Activos',                          'Assets'),
('nav.misEquipos',             'Mis equipos',                      'My equipment'),
('nav.incidencias',            'Incidencias',                      'Incidents'),
('nav.mantenimientos',         'Mantenimientos',                   'Maintenance'),
('nav.analisis',               'Análisis predictivo',              'Predictive analysis'),
('nav.historial',              'Historial del parque',             'Fleet history'),
('nav.reportes',               'Reportes',                         'Reports'),
('nav.usuarios',               'Usuarios',                         'Users'),
('nav.configuracion',          'Configuración',                    'Settings'),
('nav.salir',                  'Cerrar sesión',                    'Sign out'),
-- La abreviatura de la tarjeta de organizacion. En ingles tampoco se escribe
-- entero: al lado del nombre del plan, en un ancho de 240 px, no entra.
('nav.equiposIncluidos',       '{n} eq.',                          '{n} eq.'),
('nav.equiposDeIncluidos',     '{n}/{incluidos} eq.',              '{n}/{incluidos} eq.'),
('activo.desgloseRiesgo',      'Desglose del riesgo',              'Risk breakdown'),
('activo.segmentos',           'Segmentos del inventario',         'Inventory segments'),
('incidencia.vista',           'Cómo ver las incidencias',         'How to view the incidents'),
('incidencia.vistaLista',      'Lista',                            'List'),
('incidencia.vistaTablero',    'Tablero',                          'Board'),
('incidencia.terminadas',      'Terminadas',                       'Finished'),
('incidencia.columnaVacia',    'Nada acá',                         'Nothing here'),
('incidencia.yMasTerminadas',  'y {n} más',                        'and {n} more'),
('incidencia.tableroRecortado','El tablero muestra las {n} más recientes de {total}. Filtrá para ver el resto.',
                               'The board shows the {n} most recent of {total}. Filter to see the rest.'),
('activo.segTodos',            'Todos',                            'All'),
('activo.segRiesgoAlto',       'Riesgo alto',                      'High risk'),
('activo.segPreventivo',       'Preventivo vencido',               'Overdue preventive'),
('activo.segGarantia',         'Garantía vencida',                 'Expired warranty'),
('incidencia.altaContexto',    'El sistema propone categoría, prioridad y técnico; el técnico puede corregirlos.',
                               'The system proposes category, priority and technician; the technician can correct them.'),

-- ---------------------------------------------------- nav de configuracion
('config.titulo',              'Configuración',                    'Settings'),
('config.organizacion',        'Organización',                     'Organization'),
('config.reglas',              'Reglas predictivas',               'Predictive rules'),
('config.notificaciones',      'Notificaciones',                   'Notifications'),
('config.respaldos',           'Respaldos',                        'Backups'),
('config.bitacora',            'Bitácora',                         'Audit log'),
('config.errores',             'Errores del sistema',              'System errors'),
('config.integraciones',       'Integraciones',                    'Integrations'),
('config.ia',                  'Integración de IA',                'AI integration'),
('config.apariencia',          'Apariencia',                       'Appearance'),
('config.idioma',              'Idioma',                           'Language'),

-- ------------------------------------------------------------- generales
('comun.buscar',               'Buscar',                           'Search'),
('comun.limpiar',              'Limpiar',                          'Clear'),
('comun.guardar',              'Guardar',                          'Save'),
('comun.guardando',            'Guardando…',                       'Saving…'),
('comun.cancelar',             'Cancelar',                         'Cancel'),
('comun.cerrar',               'Cerrar',                           'Close'),
('comun.ver',                  'Ver',                              'View'),
('comun.editar',              'Editar',                            'Edit'),
('comun.estado',               'Estado',                           'Status'),
('comun.tipo',                 'Tipo',                             'Type'),
('comun.fecha',                'Fecha',                            'Date'),
('comun.cargando',             'Cargando…',                        'Loading…'),
('comun.sinResultados',        'No se encontraron resultados con los criterios aplicados.',
                               'No results found for the applied filters.'),
('comun.deTotal',              'de',                               'of'),
('comun.pagina',               'página',                           'page'),
('comun.anterior',             'Anterior',                         'Previous'),
('comun.siguiente',            'Siguiente',                        'Next'),

-- ---------------------------------------------------------------- activos
('activo.titulo',              'Activos',                          'Assets'),
('activo.codigo',              'Código',                           'Code'),
('activo.marcaModelo',         'Marca y modelo',                   'Make and model'),
('activo.ubicacion',           'Ubicación',                        'Location'),
('activo.responsable',         'Responsable',                      'Owner'),
('activo.serie',               'N.º de serie',                     'Serial number'),
('activo.criticidad',          'Criticidad',                       'Criticality'),
('activo.garantia',            'Fin de garantía',                  'Warranty end'),
('activo.antiguedad',          'Antigüedad',                       'Age'),
('activo.buscarPlaceholder',   'Código, serie, marca o responsable…',
                               'Code, serial, make or owner…'),
('activo.registrados',         'equipos registrados',              'assets registered'),
('activo.registrado1',         'equipo registrado',                'asset registered'),
('activo.nuevo',               'Nuevo equipo',                     'New asset'),
('activo.sinResultados',       'No se encontraron equipos con los criterios aplicados.',
                               'No assets found for the applied filters.'),

-- ------------------------------------------------------------ incidencias
('incidencia.titulo',          'Incidencias',                      'Incidents'),
('incidencia.numero',          'N.º',                              'No.'),
('incidencia.asunto',          'Título',                           'Subject'),
('incidencia.equipo',          'Equipo',                           'Asset'),
('incidencia.categoria',       'Categoría',                        'Category'),
('incidencia.prioridad',       'Prioridad',                        'Priority'),
('incidencia.tecnico',         'Técnico',                          'Technician'),
('incidencia.asignacion',      'Asignación',                       'Assignment'),
('incidencia.antiguedad',      'Antigüedad',                       'Age'),
('incidencia.registrar',       'Registrar incidencia',             'Report incident'),
('incidencia.sinAsignar',      'Sin asignar',                      'Unassigned'),
('incidencia.porRespaldo',     'Respaldo · revisar',               'Fallback · review'),
('incidencia.fueraObjetivo',   'Fuera del objetivo de resolución', 'Past resolution target'),
('incidencia.descripcion',     'Descripción',                      'Description'),
('incidencia.reportadaPor',    'Reportada por',                    'Reported by'),
('incidencia.justificacion',   'Justificación',                    'Rationale'),
('incidencia.clasificacion',   'Clasificación sugerida',           'Suggested classification'),
('incidencia.confianza',       'Confianza',                        'Confidence'),
('incidencia.duplicado',       'Posible duplicado',                'Possible duplicate'),
('incidencia.reasignar',       'Reasignar',                        'Reassign'),

-- --------------------------------------------------------------- analisis
('analisis.titulo',            'Análisis predictivo',              'Predictive analysis'),
('analisis.reevaluar',         'Reevaluar ahora',                  'Re-evaluate now'),
('analisis.evaluando',         'Evaluando…',                       'Evaluating…'),
('analisis.distribucion',      'Distribución del parque por nivel de riesgo',
                               'Asset distribution by risk level'),
('analisis.riesgoAlto',        'Riesgo alto',                      'High risk'),
('analisis.riesgoMedio',       'Riesgo medio',                     'Medium risk'),
('analisis.riesgoBajo',        'Riesgo bajo',                      'Low risk'),
('analisis.ranking',           'Ranking de equipos por score',     'Assets ranked by score'),
('analisis.score',             'Score',                            'Score'),
('analisis.reglasDisparadas',  'Reglas que se dispararon',         'Rules triggered'),
('analisis.sinUmbral',         'Ninguna alcanzó su umbral',        'None reached its threshold'),
('analisis.alertas',           'Alertas activas',                  'Active alerts'),
('analisis.atender',           'Atender',                          'Resolve'),
('analisis.descartar',         'Descartar',                        'Dismiss'),
('analisis.equiposEvaluados',  'equipos evaluados',                'assets evaluated'),

-- ----------------------------------------------------------- dashboard
('dash.titulo',                'Dashboard',                        'Dashboard'),
('dash.equiposActivos',        'Equipos activos',                  'Active assets'),
('dash.incidenciasAbiertas',   'Incidencias abiertas',             'Open incidents'),
('dash.alertas',               'Alertas predictivas',              'Predictive alerts'),
('dash.enRiesgoAlto',          'Equipos en riesgo alto',           'Assets at high risk'),
('dash.porRespaldo',           'Asignadas por respaldo',           'Assigned by fallback'),
('dash.mayorRiesgo',           'Equipos con mayor riesgo',         'Highest-risk assets'),
('dash.verAnalisis',           'Ver análisis completo',            'View full analysis'),
('dash.alertasRecientes',      'Alertas recientes',                'Recent alerts'),
('dash.motivoPrincipal',       'Motivo principal',                 'Main reason'),
('dash.reevaluarRiesgo',       'Reevaluar riesgo',                 'Re-evaluate risk'),
-- Los detalles de los indicadores. Los arma el backend eligiendo la clave
-- segun la cantidad, y el numero viaja aparte: en ingles va delante del
-- sustantivo y en castellano detras, asi que concatenar no alcanza.
('dash.deRegistrados',         'de {total} registrados',           'of {total} on record'),
('dash.todasAsignadas',        'todas asignadas',                  'all assigned'),
('dash.unaSinAsignar',         '1 sin asignar',                    '1 unassigned'),
('dash.variasSinAsignar',      '{cantidad} sin asignar',           '{cantidad} unassigned'),
('dash.unaEstaSemana',         '1 generada esta semana',           '1 raised this week'),
('dash.variasEstaSemana',      '{cantidad} generadas esta semana', '{cantidad} raised this week'),
('dash.sinIntervenciones',     'sin intervenciones pendientes',    'no action pending'),
('dash.requierenIntervencion', 'requieren intervención',           'need attention'),
('dash.pendientesDeRevision',  'pendientes de revisión',           'pending review'),

-- ---------------------------------------------------------- mantenimientos
('mant.titulo',                'Mantenimientos',                   'Maintenance'),
('mant.registrar',             'Registrar mantenimiento',          'Log maintenance'),
('mant.queSeHizo',             'Qué se hizo',                      'Work performed'),
('mant.resultado',             'Resultado',                        'Outcome'),
('mant.repuestos',             'Repuestos',                        'Parts'),
('mant.costo',                 'Costo',                            'Cost'),
('mant.soloPreventivos',       'Sólo preventivos',                 'Preventive only'),
('mant.preventivo',            'Preventivo',                       'Preventive'),
('mant.correctivo',            'Correctivo',                       'Corrective'),

-- ---------------------------------------------------------------- usuarios
('usuario.titulo',             'Usuarios',                         'Users'),
('usuario.nombre',             'Nombre',                           'Name'),
('usuario.roles',              'Roles',                            'Roles'),
('usuario.permisos',           'Permisos',                         'Permissions'),
('usuario.ultimoAcceso',       'Último acceso',                    'Last sign-in'),
('usuario.activo',             'Activo',                           'Active'),
('usuario.inactivo',           'Inactivo',                         'Inactive'),
('usuario.bloqueado',          'Bloqueado',                        'Locked'),
('usuario.activar',            'Activar',                          'Activate'),
('usuario.desactivar',         'Desactivar',                       'Deactivate'),
('usuario.desbloquear',        'Desbloquear',                      'Unlock'),
('usuario.permisosEfectivos',  'Permisos efectivos',               'Effective permissions'),
('usuario.loDa',               'Lo da',                            'Granted by'),
('usuario.nunca',              'nunca',                            'never'),

-- ---------------------------------------------------------------- bitacora
('bitacora.titulo',            'Bitácora',                         'Audit log'),
('bitacora.evento',            'Evento',                           'Event'),
('bitacora.criticidad',        'Criticidad',                       'Severity'),
('bitacora.desde',             'Desde',                            'From'),
('bitacora.hasta',             'Hasta',                            'To'),
('bitacora.todosEventos',      'Todos los eventos',                'All events'),
('bitacora.todosUsuarios',     'Todos los usuarios',               'All users'),
('bitacora.soloRelevantes',    'Sólo avisos y errores',            'Warnings and errors only'),
('bitacora.inmutable',         'La bitácora es de sólo lectura y no puede alterarse.',
                               'The audit log is read-only and cannot be altered.'),
('bitacora.informativo',       'Informativo',                      'Info'),
('bitacora.advertencia',       'Advertencia',                      'Warning'),
('bitacora.error',             'Error',                            'Error'),
('bitacora.critico',           'Crítico',                          'Critical'),

-- ---------------------------------------------------------------- respaldos
('respaldo.titulo',            'Respaldos',                        'Backups'),
('respaldo.estadoPorBase',     'Estado por base',                  'Status by database'),
('respaldo.respaldarAhora',    'Respaldar ahora',                  'Back up now'),
('respaldo.respaldando',       'Respaldando…',                     'Backing up…'),
('respaldo.alDia',             'Al día',                           'Up to date'),
('respaldo.atrasado',          'Atrasado',                         'Overdue'),
('respaldo.sinRespaldo',       'Sin respaldo',                     'No backup'),
('respaldo.historial',         'Historial',                        'History'),
('respaldo.archivo',           'Archivo',                          'File'),
('respaldo.tamano',            'Tamaño',                           'Size'),

-- ---------------------------------------------------------------------- IA
('ia.titulo',                  'Integración de IA',                'AI integration'),
('ia.proveedor',              'Proveedor',                         'Provider'),
('ia.estadoProveedor',         'Estado del proveedor',             'Provider status'),
('ia.respondiendo',            'Respondiendo',                     'Responding'),
('ia.sinRespuesta',            'Sin respuesta',                    'Not responding'),
('ia.probando',                'Probando',                         'Probing'),
('ia.queDatos',                'Qué datos se le envían',           'What data is sent'),
('ia.quienDecidio',            'Quién decidió las asignaciones',   'Who decided the assignments'),
('ia.porIa',                   'Proveedor de IA',                  'AI provider'),
('ia.porHeuristica',           'Reglas internas',                  'Built-in rules'),
('ia.porRespaldo',             'Regla de respaldo',                'Fallback rule'),

-- ------------------------------------------------------------ organizacion
('org.titulo',                 'Organización',                     'Organization'),
('org.razonSocial',            'Razón social',                     'Legal name'),
('org.nombreCorto',            'Nombre corto',                     'Short name'),
('org.plan',                   'Plan contratado',                  'Subscription plan'),
('org.abonoBase',              'Abono base',                       'Base fee'),
('org.equiposIncluidos',       'Equipos incluidos',                'Assets included'),
('org.equiposAdministrados',   'Equipos administrados',            'Assets managed'),
('org.equiposAdicionales',     'Equipos adicionales',              'Additional assets'),
('org.totalMensual',           'Total mensual estimado',           'Estimated monthly total'),

-- ------------------------------------------------------------------ reglas
('regla.titulo',               'Reglas predictivas',               'Predictive rules'),
('regla.queHace',              'Qué hace',                         'What it does'),
('regla.pesoRelativo',         'Peso relativo',                    'Relative weight'),
('regla.activa',               'Activa',                           'Active'),
('regla.inactiva',             'Inactiva',                         'Inactive'),
('regla.parametros',           'Parámetros',                       'Parameters'),
('regla.jsonInvalido',         'La condición no es un JSON válido.','The condition is not valid JSON.'),

-- =================================================================== H-72
-- El cuerpo de las pantallas. Hasta aca estaba traducido el marco —menu,
-- login, titulos— y el contenido de cada pantalla seguia en castellano fijo
-- escrito en el JSX. Las claves llevan el ambito de la pantalla, igual que
-- las de arriba.

-- ------------------------------------------------------------------ activos
('activo.codigoObligatorio',   'Código *',                         'Code *'),
('activo.numeroSerie',         'Número de serie',                  'Serial number'),
('activo.descripcionTecnica',  'Descripción técnica',              'Technical description'),
('activo.fechaAdquisicion',    'Fecha de adquisición',             'Purchase date'),
('activo.criticidadAyuda',     'Pondera el riesgo que calcula el motor predictivo.',
                               'Weights the risk calculated by the predictive engine.'),
('activo.tipoEquipo',          'Tipo de equipo',                   'Asset type'),
('activo.cargaMasivaFuera',    'Fuera del alcance de esta versión: la carga masiva necesita una plantilla y una previsualización que no están definidas',
                               'Out of scope for this version: bulk upload needs a template and a preview that are not defined yet'),
('activo.elegirEstado',        'Elegir el nuevo estado…',          'Choose the new status…'),
('activo.motivoBitacora',      'Motivo — queda en la bitácora',    'Reason — recorded in the audit log'),
('activo.historialTecnico',    'Historial técnico',                'Service history'),
('activo.cargandoHistorial',   'Cargando el historial…',           'Loading the history…'),
('activo.cargando',            'Cargando el equipo…',              'Loading the asset…'),
('activo.fichaTecnica',        'Ficha técnica',                    'Technical sheet'),
('activo.altaSistema',         'Alta en el sistema',               'Added to the system'),
('activo.adquisicion',         'Adquisición',                      'Purchase'),

-- -------------------------------------------------------------------- comun
('comun.sinDefinir',           'Sin definir',                      'Not set'),
('comun.quePaso',              'Qué pasó',                         'What happened'),
('comun.porQue',               'Por qué',                          'Why'),

-- ---------------------------------------------------------------------- nav
('nav.organizacionActiva',     'Organización activa',              'Active organization'),

-- ------------------------------------------------------------- incidencias
('incidencia.atencion',        'Atención',                         'Handling'),
('incidencia.diagnosticoLabel','Diagnóstico — qué se encontró',    'Diagnosis — what was found'),
('incidencia.solucionLabel',   'Solución — qué se hizo',           'Resolution — what was done'),
('incidencia.cargandoTransiciones', 'Cargando lo que se puede hacer…',
                               'Loading available actions…'),
('incidencia.diagnosticoEjemplo', 'Ventilador del disipador trabado por acumulación de polvo.',
                               'Heatsink fan jammed by dust build-up.'),
('incidencia.solucionEjemplo', 'Se reemplazó el ventilador y se limpió el disipador.',
                               'The fan was replaced and the heatsink cleaned.'),
('incidencia.pendienteRevision','Pendiente de revisión',           'Pending review'),
('incidencia.proveedorSinRespuesta', 'El proveedor no respondió.', 'The provider did not respond.'),
('incidencia.cargando',        'Cargando la incidencia…',          'Loading the incident…'),
('incidencia.elegirTecnico',   'Elegir técnico…',                  'Choose a technician…'),
('incidencia.queHizoElSistema','Qué hizo el sistema',              'What the system did'),
('incidencia.clasificacionSeccion', 'Clasificación',               'Classification'),
('incidencia.queLoSugieraElSistema', 'Que lo sugiera el sistema',  'Let the system suggest it'),
('incidencia.tituloEjemplo',   'No imprime en red',                'Cannot print over the network'),
('incidencia.descripcionEjemplo', 'Contá qué pasa, desde cuándo y qué probaste.',
                               'Describe what happens, since when, and what you already tried.'),
('incidencia.buscarPlaceholder','Número, título o equipo…',        'Number, title or asset…'),

-- ----------------------------------------------------------------- analisis
('analisis.rendimientoReglas', 'Rendimiento de las reglas',        'Rule performance'),
('analisis.genero',            'Generó',                           'Generated'),
('analisis.calculando',        'Calculando el riesgo…',            'Calculating risk…'),
('analisis.sinReglasAplicables','Ninguna regla es aplicable a este equipo.',
                               'No rule applies to this asset.'),
('analisis.delScore',          'Del score',                        'Of the score'),
('analisis.alcanzoUmbral',     'alcanzó su umbral',                'reached its threshold'),
('analisis.sinEvaluaciones',   'No hay evaluaciones todavía.',     'No evaluations yet.'),
('analisis.proveedorAsignacion','Proveedor de asignación',         'Assignment provider'),
('analisis.ultimaConsultaOk',  'Última consulta OK',               'Last successful call'),

-- --------------------------------------------------------------- apariencia
('config.temaAutomatico',      'Automática',                       'Automatic'),
('config.queNoCambia',         'Qué no cambia',                    'What does not change'),

-- ----------------------------------------------------------------- bitacora
('bitacora.buscarPlaceholder', 'Descripción, usuario o evento…',   'Description, user or event…'),
('bitacora.buscar',            'Buscar en la bitácora',            'Search the audit log'),
('bitacora.tipoEvento',        'Tipo de evento',                   'Event type'),

-- ---------------------------------------------------------------- dashboard
('dash.cargando',              'Cargando el panel…',               'Loading the dashboard…'),
('dash.sinAlertas',            'No hay alertas activas.',          'No active alerts.'),

-- ------------------------------------------------------------------ errores
('error.sinTraza',             'Sin traza',                        'No stack trace'),
('error.buscar',               'Buscar en los errores',            'Search the errors'),

-- -------------------------------------------------------- fuera de alcance
('alcance.queHaria',           'Qué haría esta sección',           'What this section would do'),
('alcance.porQueNo',           'Por qué no está',                  'Why it is not here'),
('alcance.queFaltaria',        'Qué haría falta para implementarla','What it would take to build it'),

-- ---------------------------------------------------------------- historial
('historial.soloIncidencias',  'Sólo incidencias',                 'Incidents only'),
('historial.soloMantenimientos','Sólo mantenimientos',             'Maintenance only'),
('historial.buscarPlaceholder','Equipo, descripción o persona…',   'Asset, description or person…'),
('historial.buscar',           'Buscar en el historial',           'Search the history'),
('historial.tipoHecho',        'Tipo de hecho',                    'Event type'),

-- ----------------------------------------------------------------------- IA
('ia.cargando',                'Cargando la configuración…',       'Loading the configuration…'),
('ia.quienDecide',             'Quién decide la asignación',       'Who decides the assignment'),
('ia.claude',                  'Claude — API de Anthropic',        'Claude — Anthropic API'),
('ia.cuandoNoResponde',        'Cuando el proveedor no responde',  'When the provider does not respond'),
('ia.estrategiaRespaldo',      'Estrategia de respaldo',           'Fallback strategy'),
('ia.dejarSinAsignar',         'Dejar la incidencia sin asignar',  'Leave the incident unassigned'),
('ia.ultimaConsultaOk',        'Última consulta correcta',         'Last successful call'),
('ia.sinAsignaciones',         'Todavía no hay asignaciones registradas.',
                               'No assignments recorded yet.'),

-- ------------------------------------------------------------ mantenimientos
('mant.descripcionEjemplo',    'Limpieza interna, cambio de pasta térmica y actualización de firmware.',
                               'Internal cleaning, thermal paste replacement and firmware update.'),
('mant.repuestoEjemplo',       'Pasta térmica',                    'Thermal paste'),
('mant.buscarPlaceholder',     'Equipo, técnico o descripción…',   'Asset, technician or description…'),

-- -------------------------------------------------------------- organizacion
('org.datos',                  'Datos de la organización',         'Organization details'),
('org.cargandoPlan',           'Cargando el plan…',                'Loading the plan…'),
('org.consumoIa',              'Consumo del servicio de IA',       'AI service usage'),

-- --------------------------------------------------------------------- reglas
('regla.comoCalibrar',         'Cómo calibrarlas',                 'How to tune them'),

-- ------------------------------------------------------------------- reportes
('reporte.equiposTodos',       'Equipos: todos los estados',       'Assets: all statuses'),
('reporte.incidenciasTodos',   'Incidencias: todos los estados',   'Incidents: all statuses'),
('reporte.estadoEquipo',       'Estado del equipo',                'Asset status'),
('reporte.estadoIncidencia',   'Estado de la incidencia',          'Incident status'),

-- ------------------------------------------------------------------ respaldos
('respaldo.porQueImporta',     'Por qué importa acá',              'Why it matters here'),
('respaldo.comoSeHace',        'Cómo se hace',                     'How it works'),

-- ------------------------------------------------------------------- usuarios
('usuario.telefono',           'Teléfono',                         'Phone'),
('usuario.sinPermisos',        'Este usuario no tiene ningún permiso.',
                               'This user has no permissions.'),

-- ------------------------- H-72, segunda tanda: las notas al pie y los avisos
-- Son los parrafos que explican por que la pantalla hace lo que hace. Estaban
-- fijos en el JSX igual que los titulos.
('comun.limpiarFiltros',       'Limpiar los filtros',              'Clear the filters'),
('comun.verTodas',             'Ver todas',                        'View all'),

('activo.darDeBaja',           'Dar de baja',                      'Decommission'),
('activo.ahoraEsta',           'Ahora está',                       'It is now'),
('activo.noOperativoNota',     '. El motor predictivo no evalúa los equipos que no están operativos.',
                               '. The predictive engine does not evaluate assets that are not operational.'),
('activo.sinEvaluar',          'Este equipo todavía no fue evaluado por el motor predictivo.',
                               'This asset has not been evaluated by the predictive engine yet.'),
('activo.misEquiposNota',      'Acá aparecen sólo los equipos de los que figurás como responsable. Para reportar una falla de cualquiera de ellos, usá «Reportar un problema».',
                               'Only the assets you are listed as responsible for appear here. To report a fault on any of them, use “Report a problem”.'),

('analisis.scoreNota',         'El score es el promedio de las reglas ponderado por su peso, ajustado por la criticidad del equipo. Una regla aporta desde antes de alcanzar su umbral.',
                               'The score is the weighted average of the rules, adjusted by the criticality of the asset. A rule contributes before reaching its threshold.'),
('analisis.aciertosNota',      '«Aciertos» son las atendidas sobre las que ya se resolvieron. Queda vacío mientras no haya ninguna resuelta: un porcentaje sobre cero se lee como cero, y no es lo mismo que una regla falle siempre a que todavía no se haya evaluado.',
                               '“Hits” are the alerts acted on out of those already resolved. It stays empty while none are resolved: a percentage over zero reads as zero, and a rule that always fails is not the same as one that has not been evaluated yet.'),
('analisis.sinEvaluarParque',  'El parque todavía no se evaluó. Usá «Reevaluar ahora» para calcular el riesgo de cada equipo.',
                               'The asset base has not been evaluated yet. Use “Re-evaluate now” to calculate the risk of each asset.'),

('dash.sinEvaluar',            'Todavía no se evaluó el parque. Usá «Reevaluar riesgo» para calcular el score de cada equipo.',
                               'The asset base has not been evaluated yet. Use “Re-evaluate risk” to calculate the score of each asset.'),

('config.accionPrincipal',     'Acción principal',                 'Primary action'),
('config.faltanEnEsteIdioma',  'En este idioma faltan',            'Missing in this language:'),
('config.idiomasNoLeidos',     'No se pudo leer la lista de idiomas.',
                               'The list of languages could not be loaded.'),
('config.ejemploNotificacion', '6 incidencias fueron asignadas por respaldo y están pendientes de revisión.',
                               '6 incidents were assigned by the fallback rule and are pending review.'),

('error.origenNota',           'Estas entradas salen de la misma bitácora inmutable que la pantalla de auditoría, filtradas por criticidad. Lo que se agrega acá es la traza técnica, que se muestra sólo a quien tiene la patente para verla.',
                               'These entries come from the same immutable audit log as the auditing screen, filtered by severity. What is added here is the technical stack trace, shown only to whoever holds the permission to see it.'),

('incidencia.estadoFinalNota', 'y no admite más cambios de estado. Es un estado final.',
                               'and admits no further status changes. It is a final state.'),
('incidencia.fueraDeObjetivo', 'Fuera de objetivo',                'Past target'),
('incidencia.verLaIncidencia', 'Ver la incidencia',                'View the incident'),
('incidencia.volverAlListado', 'Volver al listado',                'Back to the list'),
('incidencia.dejarVacioNota',  'Si no sabés qué elegir, dejalo vacío: el sistema lo sugiere a partir de lo que escribiste y el técnico lo puede corregir.',
                               'If you are not sure what to pick, leave it empty: the system suggests it from what you wrote and the technician can correct it.'),
('incidencia.soloRespaldo',    'Mostrando sólo las asignadas por respaldo.',
                               'Showing only those assigned by the fallback rule.'),
('incidencia.franjaAmbarNota', 'La franja ámbar marca las asignaciones de respaldo pendientes de revisión.',
                               'The amber stripe marks fallback assignments that are pending review.'),

('ia.claveGuardadaNota',       'Hay una clave de API guardada y cifrada para esta organización. No se muestra ni puede recuperarse: para cambiarla hay que cargar una nueva.',
                               'There is an API key stored and encrypted for this organization. It is never shown and cannot be retrieved: to change it, enter a new one.'),
('ia.claveDelEntornoNota',     'Se está usando la clave del entorno del servidor. Si cargás una acá, esa pasa a tener prioridad y queda bajo el control de esta organización.',
                               'The key from the server environment is being used. If you enter one here, it takes precedence and stays under this organization’s control.'),
('ia.sinClaveNota',            'No hay clave de API. El sistema va a seguir asignando con las reglas internas y lo va a dejar registrado en la bitácora.',
                               'There is no API key. The system will keep assigning with the built-in rules and will record it in the audit log.'),
('ia.borrarClave',             'Borrar la clave guardada',         'Delete the stored key'),
('ia.reglasInternasOpcion',    'Reglas internas — determinístico, sin salir a internet',
                               'Built-in rules — deterministic, no internet access'),
('ia.datosNoViajanNota',       'Lo que se desactiva no viaja. Son datos de tu gente y la decisión es tuya.',
                               'Whatever is switched off is not sent. This is your people’s data and the decision is yours.'),
('ia.respaldoMenosCarga',      'Asignar al técnico con menos incidencias abiertas',
                               'Assign to the technician with the fewest open incidents'),
('ia.ultimoError',             'Último error',                     'Last error'),
('ia.disyuntorNota',           'Tras cinco fallos seguidos el circuito se abre y el sistema deja de llamar al proveedor durante sesenta segundos, para no pagar la espera en cada incidencia.',
                               'After five consecutive failures the circuit opens and the system stops calling the provider for sixty seconds, so the wait is not paid on every incident.'),
('ia.reglasNoEsIaNota',        '«Reglas internas» no es lo mismo que «proveedor de IA»: el proveedor determinístico resuelve por coincidencia de términos y puntaje, no con un modelo. Registrarlos como lo mismo falsearía de dónde salió cada decisión.',
                               '“Built-in rules” is not the same as “AI provider”: the deterministic provider resolves by term matching and scoring, not with a model. Recording them as the same thing would misstate where each decision came from.'),

('reporte.pdfNota',            'El PDF se arma en el momento y no se guarda en el servidor. Lleva impresa la fecha de emisión, quién lo generó y los filtros aplicados: un reporte que circula fuera del sistema tiene que poder explicarse solo.',
                               'The PDF is built on the spot and is not stored on the server. It carries the issue date, who generated it and the filters applied: a report that travels outside the system has to explain itself.'),

('respaldo.sinRespaldos',      'Todavía no se hizo ningún respaldo. Usá «Respaldar ahora» en cada base.',
                               'No backup has been taken yet. Use “Back up now” on each database.'),
('respaldo.confianzaNota',     ', porque un respaldo que no se puede leer es peor que no tener respaldo: da confianza.',
                               ', because a backup that cannot be read is worse than no backup: it gives false confidence.'),
('respaldo.restauracionNota',  'La restauración no está en esta pantalla a propósito. Es una operación destructiva que reemplaza los datos actuales y corta todas las sesiones abiertas, y ponerla detrás de un botón web es pedir un accidente. El procedimiento está en el manual de administración.',
                               'Restoring is deliberately not on this screen. It is a destructive operation that replaces the current data and drops every open session, and putting it behind a web button is asking for an accident. The procedure is in the administration manual.'),
('respaldo.probarNota',        'Un respaldo que no se probó restaurar no es un respaldo: conviene verificarlo cada tanto sobre una base de prueba, nunca sobre la de producción.',
                               'A backup that has never been restored is not a backup: it should be verified now and then against a test database, never against production.'),

('usuario.propioUsuarioNota',  'Es tu propio usuario. El sistema no te va a dejar quitarte la gestión de usuarios: quedarías sin poder revertirlo.',
                               'This is your own account. The system will not let you remove your own user management permission: you would have no way back.'),
('usuario.altaDesdeBaseNota',  'El alta de usuarios se hace desde la base de datos: crear una cuenta implica establecer una contraseña, y hacerlo desde una pantalla sin un circuito de invitación por correo dejaría la contraseña inicial en manos de quien la crea.',
                               'Users are created from the database: creating an account means setting a password, and doing it from a screen without an email invitation flow would leave the initial password in the hands of whoever creates it.'),

-- ---------------------------------------------- H-72, lo que quedaba al final
('incidencia.laIncidenciaEsta','La incidencia está',               'The incident is'),
('incidencia.sinAsignacion',   'Esta incidencia todavía no pasó por el proceso de asignación.',
                               'This incident has not gone through the assignment process yet.'),
('incidencia.reasignarQuitaMarca', 'Reasignar a mano quita la marca de pendiente de revisión: ya la revisó una persona.',
                               'Reassigning by hand clears the pending-review flag: a person has already reviewed it.'),
('analisis.ajustarUmbrales',   'Para ajustar los umbrales con el histórico y no a ojo',
                               'To tune the thresholds against history instead of by eye'),
('activo.importarPlanilla',    'Importar desde planilla',          'Import from spreadsheet'),
('activo.reportarProblema',    'Reportar un problema',             'Report a problem'),
('config.coloresNota',         'Los cuatro colores de estado —alto, medio, bajo e IA— se conservan como familias en todos los temas. El riesgo alto es rojizo siempre: cambiarle el color a un semáforo lo vuelve inútil. Un tema cambia las superficies, el texto y el color de marca, nunca el significado.',
                               'The four status colours —high, medium, low and AI— are kept as families across every theme. High risk is always reddish: changing the colour of a traffic light makes it useless. A theme changes surfaces, text and brand colour, never meaning.'),
('bitacora.inmutableNota',     'La bitácora es de sólo lectura y no puede alterarse. No es una restricción de esta pantalla: la tabla tiene un disparador que rechaza cualquier modificación o borrado, incluso conectándose directamente a la base de datos.',
                               'The audit log is read-only and cannot be altered. This is not a restriction of this screen: the table has a trigger that rejects any update or delete, even when connecting directly to the database.'),
('ia.loDecideTuOrganizacion',  'Lo decide tu organización',        'Your organization decides'),
('ia.respaldoRevisionNota',    'En los dos casos la incidencia queda marcada para revisión: el respaldo asigna por carga, sin mirar la especialidad, así que alguien tiene que confirmar que el técnico sea el adecuado.',
                               'In both cases the incident is flagged for review: the fallback assigns by workload, without looking at specialty, so someone has to confirm the technician is the right one.'),
('org.consumoNota',            'Cada consulta queda asentada en la bitácora con su origen. Las que resuelve el propio sistema no salen a la red y no tienen costo; las de respaldo son las veces que el proveedor no respondió.',
                               'Every call is recorded in the audit log with its origin. The ones the system resolves on its own never reach the network and cost nothing; the fallback ones are the times the provider did not respond.'),
('reporte.todosLosTecnicos',   'Todos los técnicos',               'All technicians'),
('respaldo.quien',             'Quién',                            'Who'),
('respaldo.seUsa',             'Se usa',                           'Uses'),
('respaldo.historialNota',     'El valor del análisis predictivo depende del historial técnico acumulado por cada equipo. Perder ese historial no es perder registros administrativos: es perder la capacidad de anticipar fallas, y se recupera sólo con meses de operación.',
                               'The value of the predictive analysis depends on the service history accumulated by each asset. Losing that history is not losing administrative records: it is losing the ability to anticipate failures, and it only comes back after months of operation.'),

-- ------------------------- H-72: lo que el sistema dice cuando algo falla
-- Son los mensajes mas visibles: aparecen justo cuando la persona no
-- entiende que paso. El mensaje que viene del backend (`ex.message`) sigue
-- en castellano: esa es otra capa y esta anotada como limite.
('comun.nunca',                'nunca',                            'never'),
('aviso.claveBorrada',           'Clave borrada.',                      'Key deleted.'),
('aviso.claveGuardada',          'Clave guardada y cifrada.',           'Key saved and encrypted.'),
('aviso.configuracionGuardada',  'Configuración guardada.',             'Settings saved.'),
('error.cargarReglas',           'No se pudieron cargar las reglas.',   'The rules could not be loaded.'),
('error.cargarErrores',          'No se pudieron cargar los errores.',  'The errors could not be loaded.'),
('error.cargarMisEquipos',       'No se pudieron cargar tus equipos.',  'Your assets could not be loaded.'),
('error.guardarRoles',           'No se pudieron guardar los roles.',   'The roles could not be saved.'),
('error.actualizarAlerta',       'No se pudo actualizar la alerta.',    'The alert could not be updated.'),
('error.avanzarIncidencia',      'No se pudo avanzar la incidencia.',   'The incident could not be advanced.'),
('error.borrarClave',            'No se pudo borrar la clave.',         'The key could not be deleted.'),
('error.cambiarEstado',          'No se pudo cambiar el estado.',       'The status could not be changed.'),
('error.cargarAnalisis',         'No se pudo cargar el análisis.',      'The analysis could not be loaded.'),
('error.cargarEquipo',           'No se pudo cargar el equipo.',        'The asset could not be loaded.'),
('error.cargarHistorial',        'No se pudo cargar el historial.',     'The history could not be loaded.'),
('error.cargarListado',          'No se pudo cargar el listado.',       'The list could not be loaded.'),
('error.cargarPanel',            'No se pudo cargar el panel.',         'The dashboard could not be loaded.'),
('error.cargarPlan',             'No se pudo cargar el plan.',          'The plan could not be loaded.'),
('error.cargarRendimiento',      'No se pudo cargar el rendimiento de las reglas.', 'The rule performance could not be loaded.'),
('error.cargarUsuario',          'No se pudo cargar el usuario.',       'The user could not be loaded.'),
('error.cargarBitacora',         'No se pudo cargar la bitácora.',      'The audit log could not be loaded.'),
('error.cargarConfiguracion',    'No se pudo cargar la configuración.', 'The settings could not be loaded.'),
('error.cargarIncidencia',       'No se pudo cargar la incidencia.',    'The incident could not be loaded.'),
('error.operacion',              'No se pudo completar la operación.',  'The operation could not be completed.'),
('error.conectar',               'No se pudo conectar con el servidor.', 'The server could not be reached.'),
('error.darDeBaja',              'No se pudo dar de baja el equipo.',   'The asset could not be decommissioned.'),
('error.generarReporte',         'No se pudo generar el reporte.',      'The report could not be generated.'),
('error.guardarEquipo',          'No se pudo guardar el equipo.',       'The asset could not be saved.'),
('error.guardarClave',           'No se pudo guardar la clave.',        'The key could not be saved.'),
('error.guardarRegla',           'No se pudo guardar la regla.',        'The rule could not be saved.'),
('error.guardar',                'No se pudo guardar.',                 'It could not be saved.'),
('error.respaldar',              'No se pudo hacer el respaldo.',       'The backup could not be made.'),
('error.reasignar',              'No se pudo reasignar.',               'It could not be reassigned.'),
('error.reevaluar',              'No se pudo reevaluar el parque.',     'The asset base could not be re-evaluated.'),
('error.registrarIncidencia',    'No se pudo registrar la incidencia.', 'The incident could not be registered.'),
('error.registrar',              'No se pudo registrar.',               'It could not be registered.'),

-- ------------------ H-77 (2/3): ternarios, prosa de alcance, temas y reportes
('comun.sinDeterminar',        'No se pudo determinar',            'Could not be determined'),

('activo.cambiarEstado',       'Cambiar el estado',                'Change the status'),
('activo.sinHistorial',        'Este equipo no tiene incidencias ni mantenimientos registrados.',
                               'This asset has no incidents or maintenance on record.'),
('activo.sinPermisoHistorial', 'No tenés permiso para ver el historial de este equipo.',
                               'You do not have permission to see this asset''s history.'),

('incidencia.resolverExigeTextos', 'Resolver exige el diagnóstico y la solución.',
                               'Resolving requires the diagnosis and the resolution.'),
('incidencia.pasarA',          'Pasar a {estado}',                 'Move to {estado}'),
('incidencia.faltanTextos',    'Para resolverla hay que completar el diagnóstico y la solución.',
                               'To resolve it you have to fill in the diagnosis and the resolution.'),
('incidencia.sellaFecha',      'Al resolverla se sella la fecha de resolución: es el dato del que sale el tiempo de reparación.',
                               'Resolving it stamps the resolution date: that is where the repair time comes from.'),
('incidencia.abiertaHace',     'Abierta hace',                     'Open for'),
('incidencia.resolucion',      'Resolución',                       'Resolution'),
('incidencia.unaPorRespaldo',  '1 incidencia fue asignada por respaldo y está pendiente de revisión.',
                               '1 incident was assigned by the fallback rule and is pending review.'),
('incidencia.variasPorRespaldo', '{n} incidencias fueron asignadas por respaldo y están pendientes de revisión.',
                               '{n} incidents were assigned by the fallback rule and are pending review.'),

('ia.decididaPorIa',           'Decidida por el proveedor de IA',  'Decided by the AI provider'),
('ia.decididaPorReglas',       'Decidida por reglas automáticas',  'Decided by built-in rules'),

('analisis.parqueSinEvaluar',  'Todavía no se evaluó el parque',   'The asset base has not been evaluated yet'),

('config.idiomaDeFabrica',     'El idioma de fábrica del sistema.','The system''s factory language.'),
('config.idiomaCambia',        'Cambia la navegación y las pantallas traducidas.',
                               'Changes the navigation and the translated screens.'),

('respaldo.sinErrores',        'sin errores',                      'no errors'),
('usuario.cargando',           'Cargando el usuario…',             'Loading the user…'),

('reporte.filtraPor',          'Filtra por',                       'Filters by'),
('reporte.filtroPeriodo',      'período',                          'period'),
('reporte.filtroEstadoEquipo', 'estado del equipo',                'asset status'),
('reporte.filtroEstadoIncidencia', 'estado de la incidencia',      'incident status'),
('reporte.filtroTecnico',      'técnico',                          'technician'),
('reporte.parque',             'Parque informático',               'IT asset base'),
('reporte.parqueQueResponde',  'Qué equipos hay, dónde están, quién responde por cada uno y cuál está en riesgo.',
                               'What assets exist, where they are, who is responsible for each and which are at risk.'),
('reporte.incidenciasQueResponde', 'Qué se reportó en el período, cómo terminó y cuánto tardó. Trae el tiempo medio de resolución.',
                               'What was reported in the period, how it ended and how long it took. Includes the mean time to resolution.'),
('reporte.mantenimientosQueResponde', 'Qué se le hizo al parque en el período, a cuántos equipos y a qué costo.',
                               'What was done to the asset base in the period, to how many assets and at what cost.'),

-- ------------------------------------------------------------------- temas
-- Dos temas y no seis. Clásico, Grafito y Carbón se retiraron en el rediseño
-- 03 y sus claves salen con ellos: una fila que ninguna pantalla pide es una
-- pregunta sin buena respuesta en la defensa.
('tema.oscuro',                'Oscuro',                           'Dark'),
('tema.oscuroDetalle',         'Recomendado para salas con poca luz. Es el tema de fábrica.',
                               'Recommended for dimly lit rooms. The factory theme.'),
('tema.claro',                 'Claro',                            'Light'),
('tema.claroDetalle',          'Mejor para imprimir y para proyectar.',
                               'Better for printing and for projecting.'),
('tema.sistema',               'Seguir el sistema',                'Follow the system'),
('tema.sistemaDetalle',        'Cambia solo al anochecer si tu equipo lo hace.',
                               'Switches at dusk on its own if your machine does.'),
('tema.compacta',              'Compacta',                         'Compact'),
('tema.compactaDetalle',       'Fila de 34 px, para carga y auditoría.',
                               '34 px rows, for data entry and auditing.'),
('tema.normal',                'Normal',                           'Normal'),
('tema.normalDetalle',         'Fila de 48 px, la del diseño.',     '48 px rows, the design default.'),
('tema.amplia',                'Amplia',                           'Roomy'),
('tema.ampliaDetalle',         'Fila de 60 px, para pantallas grandes.',
                               '60 px rows, for large screens.'),

('alcance.fueraDeAlcance',     'Fuera del alcance de esta versión','Out of scope for this version'),

-- ------------------------------------------ consumo del servicio de IA
('org.consumoError',           'No se pudo leer el consumo.',      'The usage figures could not be read.'),
('org.consultasAlProveedor',   'Consultas al proveedor de IA',     'Calls to the AI provider'),
('org.clasificaciones',        'Clasificación de incidencias',     'Incident classification'),
('org.asignaciones',           'Asignación de técnico',            'Technician assignment'),
('org.guias',                  'Guías de reparación',              'Repair guides'),
('org.resueltasPorElSistema',  'Resueltas por el propio sistema',  'Resolved by the system itself'),
('org.porRespaldo',            'Veces que hubo que usar el respaldo',
                               'Times the fallback had to be used'),

-- ------------------------------------------- modulo del solicitante
('nav.misPedidos',             'Mis pedidos',                      'My requests'),

-- reportar un problema
('reportar.titulo',            'Reportar un problema',             'Report a problem'),
('reportar.pasoDe',            'Paso {n} de 3',                    'Step {n} of 3'),
('reportar.queEquipo',         '¿Qué equipo te está dando problemas?',
                               'Which machine is giving you trouble?'),
('reportar.sinEquipos',        'No figurás como responsable de ningún equipo, así que no hay de cuál reportar. Avisale al responsable técnico para que te asigne el tuyo.',
                               'You are not listed as responsible for any machine, so there is nothing to report on. Ask the technical lead to assign yours.'),
('reportar.contame',           'Contame qué pasa',                 'Tell us what is going on'),
('reportar.enUnaLinea',        'En una línea, ¿qué notás?',        'In one line, what do you notice?'),
('reportar.tituloEjemplo',     'Se apaga sola al abrir el sistema de gestión',
                               'It shuts down on its own when I open the management system'),
('reportar.contalo',           'Contámelo con tus palabras',       'Tell us in your own words'),
('reportar.descripcionEjemplo','Va lentísima y se apaga sola cuando abro facturación. Hoy me pasó tres veces.',
                               'It is very slow and shuts down on its own when I open billing. It happened three times today.'),
('reportar.sinTecnicismos',    'No hace falta que sepas de computadoras ni que uses palabras técnicas.',
                               'You do not need to know about computers or use technical words.'),
('reportar.podesTrabajar',     '¿Podés seguir trabajando?',        'Can you keep working?'),
('reportar.si',                'Sí',                               'Yes'),
('reportar.aMedias',           'Sí, a medias',                     'Yes, more or less'),
('reportar.no',                'No, estoy frenada',                'No, I am stuck'),
('reportar.fraseSi',           'Puedo seguir usando el equipo.',   'I can keep using the machine.'),
('reportar.fraseAMedias',      'Puedo seguir usándolo, pero con dificultad.',
                               'I can keep using it, but with difficulty.'),
('reportar.fraseNo',           'No puedo seguir usando el equipo.','I cannot keep using the machine.'),
('reportar.revisar',           'Revisá antes de enviar',           'Check before sending'),
('reportar.quePasa',           'Qué pasa',                         'What is happening'),
('reportar.contaste',          'Lo que contaste',                  'What you told us'),
('reportar.queVaAPasar',       'Cuando lo mandes, un técnico lo va a ver enseguida. Vas a poder seguirlo desde «Mis pedidos» con el número que te damos al enviar.',
                               'Once you send it, a technician will see it right away. You can follow it in «My requests» with the number we give you.'),
('reportar.corregir',          'Corregir algo',                    'Change something'),
('reportar.enviar',            'Enviar el reporte',                'Send the report'),
('reportar.cancelar',          'Cancelar y volver a mis equipos',  'Cancel and go back to my machines'),
('reportar.listo',             'Listo, ya lo mandamos',            'Done, we sent it'),
('reportar.quedoRegistrado',   'Tu pedido quedó registrado como',  'Your request was registered as'),
('reportar.anotaNumero',       'Anotá ese número si tenés que llamar.',
                               'Write that number down in case you need to call.'),
('reportar.queSigue',          'Qué pasa ahora',                   'What happens now'),
('reportar.sigue1',            'Un técnico lo revisa y lo toma.',  'A technician reviews it and takes it.'),
('reportar.sigue2',            'Te avisamos cuando empiece a arreglarlo.',
                               'We let you know when they start fixing it.'),
('reportar.sigue3',            'Cuando esté resuelto te lo confirmamos acá.',
                               'When it is fixed we confirm it here.'),
('reportar.verMiPedido',       'Ver mi pedido',                    'See my request'),
('reportar.volverAEquipos',    'Volver a mis equipos',             'Back to my machines'),

-- mis pedidos
('pedido.abiertos',            'abiertos',                         'open'),
('pedido.resueltos',           'resueltos',                        'resolved'),
('pedido.yaResueltos',         'Ya resueltos',                     'Already resolved'),
('pedido.sinPedidos',          'Todavía no reportaste nada',       'You have not reported anything yet'),
('pedido.sinPedidosDetalle',   'Cuando algo no ande, reportalo desde acá y vas a poder seguirlo en esta pantalla.',
                               'When something stops working, report it from here and you can follow it on this screen.'),
('pedido.noSePudo',            'No se pudieron cargar tus pedidos.','Your requests could not be loaded.'),
('pedido.noSePudoUno',         'No se pudo cargar el pedido.',     'The request could not be loaded.'),
('pedido.noExiste',            'Ese pedido no existe',             'That request does not exist'),
('pedido.noExisteDetalle',     'Puede que el enlace esté mal escrito.',
                               'The link may be mistyped.'),
('pedido.volver',              'Volver a mis pedidos',             'Back to my requests'),
('pedido.cargando',            'Cargando tu pedido…',              'Loading your request…'),
('pedido.paso',                'Paso',                             'Step'),
('pedido.novedades',           'Novedades',                        'Updates'),
('pedido.novedadRecibido',     'Recibimos tu pedido',              'We received your request'),
('pedido.novedadAsignado',     'Se lo asignamos a un técnico',     'We assigned it to a technician'),
('pedido.novedadResuelto',     'Nos avisaron que quedó resuelto',  'We were told it is resolved'),
('pedido.loQueEscribiste',     'Lo que escribiste',                'What you wrote'),
('pedido.queSeHizo',           'Qué se hizo',                      'What was done'),
('pedido.loEstaViendo',        'Lo está viendo',                   'It is being handled by'),
('pedido.delEquipoTecnico',    ', del equipo técnico.',            ', from the technical team.'),

-- los cuatro pasos del recorrido, contados como los ve quien reporto
('pedido.recibido',            'Lo recibimos',                     'We got it'),
('pedido.asignado',            'Se lo asignamos a un técnico',     'Assigned to a technician'),
('pedido.arreglando',          'Lo están arreglando',              'Being fixed'),
('pedido.esperandoRepuesto',   'Esperando un repuesto',            'Waiting for a part'),
('pedido.resuelto',            'Resuelto',                         'Resolved'),
('pedido.cerrado',             'Resuelto y cerrado',               'Resolved and closed'),
('pedido.anulado',             'Cancelado',                        'Cancelled'),

-- comunes que faltaban
('comun.volver',               'Volver',                           'Back'),
('comun.continuar',            'Continuar',                        'Continue'),
('comun.enviando',             'Enviando…',                        'Sending…'),

-- ------------------------------------------------- registro inexistente
('incidencia.noExiste',        'Esa incidencia no existe',         'That incident does not exist'),
('incidencia.noExisteDetalle', 'Puede que se haya anulado, o que el número del enlace esté mal escrito.',
                               'It may have been voided, or the number in the link may be mistyped.'),
('activo.noExiste',            'Ese equipo no existe',             'That asset does not exist'),
('activo.noExisteDetalle',     'Puede que se haya dado de baja, o que el enlace esté mal.',
                               'It may have been decommissioned, or the link may be wrong.'),

-- --------------------------------------------------- direccion inexistente
('error.404Titulo',            'Esta página no existe',            'This page does not exist'),
('error.404Contexto',          'La dirección que abriste no corresponde a ninguna pantalla del sistema',
                               'The address you opened does not match any screen in the system'),
('error.404Detalle',           'La dirección',                     'The address'),
('error.404Detalle2',          'no corresponde a nada del sistema. Puede que el registro se haya anulado, o que el número esté mal escrito.',
                               'does not match anything in the system. The record may have been voided, or the number may be mistyped.'),
('error.404Volver',            'Volver al inicio',                 'Back to the start'),
('error.404Buscar',            'Buscar en incidencias',            'Search in incidents'),
('error.404Aviso',             'Si llegaste desde un enlace del propio sistema, avisale al administrador: es un enlace roto y no un error tuyo.',
                               'If you got here from a link inside the system, let the administrator know: it is a broken link and not your mistake.'),

-- ------------------------------------------------- guia de reparacion
('guia.titulo',                'Guía de reparación',               'Repair guide'),
('guia.explicacion',           'El sistema puede armar una lista de pasos ordenados de menos a más invasivo, con el riesgo marcado en los que lo tienen. Se pide cuando la necesitás: cada consulta al proveedor tiene costo, así que no se genera sola al abrir la incidencia.',
                               'The system can build a list of steps ordered from least to most invasive, with the risk flagged on the ones that carry it. You ask for it when you need it: every call to the provider has a cost, so it is not generated on its own when the incident is opened.'),
('guia.generar',               'Generar guía de reparación',       'Generate repair guide'),
('guia.armando',               'Armando la guía…',                 'Building the guide…'),
('guia.seguiTrabajando',       'Podés seguir trabajando en la incidencia mientras tanto.',
                               'You can keep working on the incident in the meantime.'),
('guia.heuristica',            'Heurística',                       'Heuristic'),
('guia.conReglas',             'Guía armada con reglas, no con IA.',
                               'Guide built from rules, not from AI.'),
('guia.sinRespuesta',          'El proveedor no respondió.',       'The provider did not respond.'),
('guia.cuandoEscalar',         'Cuándo escalar',                   'When to escalate'),
('guia.volverAPedir',          'Volver a pedirla',                 'Ask again'),
('guia.reintentarIa',          'Reintentar con IA',                'Retry with AI'),
-- `comun.riesgo` y no `guia.riesgo`: la misma palabra encabeza tres tablas
-- y ademas marca el paso peligroso de la guia.
('comun.riesgo',               'Riesgo',                           'Risk'),
('guia.sinRiesgo',             'Sin riesgo',                       'No risk'),
('guia.noCambiaEstado',        'Es una sugerencia: marcar un paso no cambia el estado de la incidencia, y el sistema no ejecuta nada sobre el equipo.',
                               'It is a suggestion: ticking a step does not change the state of the incident, and the system does not run anything on the machine.'),
('guia.noSePudo',              'No se pudo pedir la guía.',        'The guide could not be requested.'),

-- ------------------------------------------------------------------ ingreso
('login.mostrar',              'Mostrar',                          'Show'),
('login.ocultar',              'Ocultar',                          'Hide'),
('login.ayuda',                '¿Problemas para entrar? Escribile al administrador de tu organización.',
                               'Trouble signing in? Write to your organisation''s administrator.'),
('login.bloqueadaTitulo',      'Cuenta bloqueada por intentos fallidos',
                               'Account locked after failed attempts'),
('login.bloqueadaDetalle',     'El bloqueo no se levanta solo ni con el tiempo: lo destraba un administrador de tu organización desde la pantalla de Usuarios.',
                               'The lock does not lift on its own or with time: an administrator of your organisation clears it from the Users screen.'),
('login.volver',               'Volver al ingreso',                'Back to sign in'),

-- ---------------------------------------------------------- apariencia
('config.temaUnificadoA',      'El tema',                          'The'),
('config.temaUnificadoB',      'se unificó con los dos que quedan. Tu configuración ya quedó migrada.',
                               'theme was merged into the two that remain. Your setting has already been migrated.'),
('config.entendido',           'Entendido',                        'Got it'),
('config.densidadNota',        'La densidad cambia el alto de fila y el espaciado, nunca el tamaño de la letra: achicar el texto es la forma más rápida de romper el contraste.',
                               'Density changes row height and spacing, never the type size: shrinking the text is the fastest way to break contrast.'),
('config.accesibilidad',       'Accesibilidad',                    'Accessibility'),
('config.reducirMovimiento',   'Reducir movimiento',               'Reduce motion'),
('config.reducirMovimientoDetalle',
                               'Saca el brillo que recorre los esqueletos de carga. Si ya lo pediste en el sistema operativo, esto no hace falta: se respeta igual.',
                               'Removes the sweep across the loading skeletons. If you already asked for it in your operating system this is not needed: it is honoured anyway.'),
('config.formaSiempre',        'El riesgo y la prioridad se leen además por forma y por texto, no sólo por color. Eso está siempre activo y no se puede desactivar: es lo que hace que la tabla sirva para alguien que no distingue rojo de verde.',
                               'Risk and priority are also conveyed by shape and by text, not only by colour. That is always on and cannot be turned off: it is what makes the table usable for someone who cannot tell red from green.'),

-- --------------------------------------------------- pantallas sin alcance
('alcance.notificacionesQueHaria', 'Definir a quién se le avisa y por qué medio cuando pasa algo que amerita atención: una alerta predictiva de riesgo alto, una incidencia crítica sin asignar, un respaldo que falló, o el proveedor de asignación caído.',
                               'Define who gets notified and by what channel when something worth attention happens: a high-risk predictive alert, an unassigned critical incident, a failed backup, or the assignment provider being down.'),
('alcance.notificacionesPorQueNo', 'El sistema no tiene un canal de salida. Configurar a quién avisar sin poder enviarle nada sería una pantalla de controles que no hacen nada, y eso es peor que no tenerla: alguien la configuraría y confiaría en que le va a llegar el aviso. Hoy los avisos que el sistema sí da son los de la propia interfaz —el indicador de asignaciones por respaldo, el estado del proveedor, las alertas del panel— y todos exigen que alguien entre a mirar.',
                               'The system has no outbound channel. Configuring who to notify without being able to send anything would be a screen of controls that do nothing, and that is worse than not having it: someone would configure it and trust the notice would arrive. The notices the system does give today are the ones in the interface itself —the fallback-assignment indicator, the provider status, the dashboard alerts— and all of them require someone to come and look.'),
('alcance.notificacionesFalta1', 'Un servicio de envío de correo, con su configuración de servidor y sus credenciales.',
                               'An email delivery service, with its server configuration and credentials.'),
('alcance.notificacionesFalta2', 'Una cola o un reintento: un aviso que se pierde porque el servidor de correo estaba caído no sirve.',
                               'A queue or a retry: a notice lost because the mail server was down is useless.'),
('alcance.notificacionesFalta3', 'Un registro de lo enviado, para poder responder «¿me avisaron?» con un dato y no con una suposición.',
                               'A log of what was sent, so “was I notified?” can be answered with a fact and not a guess.'),
('alcance.notificacionesFalta4', 'Una política de agrupación: cincuenta alertas en una hora no pueden ser cincuenta correos.',
                               'A grouping policy: fifty alerts in an hour cannot be fifty emails.'),
('alcance.integracionesQueHaria', 'Conectar el sistema con herramientas que ya usa la empresa: importar el inventario desde una planilla o desde un agente de red, exportar los indicadores a una herramienta de reportes, o sincronizar los usuarios con el directorio de la organización.',
                               'Connect the system to tools the company already uses: import the inventory from a spreadsheet or a network agent, export the indicators to a reporting tool, or sync users with the organization directory.'),
('alcance.integracionesPorQueNo', 'El descubrimiento automático de red y los agentes de monitoreo en tiempo real están declarados fuera de alcance en el documento (10.2.3), y son justamente el caso de integración que más valor tendría. Lo que queda —importar una planilla, exportar indicadores— no requiere una sección de configuración: son acciones puntuales que corresponden a las pantallas de Activos y de Análisis.',
                               'Automatic network discovery and real-time monitoring agents are declared out of scope in the document (10.2.3), and they are precisely the integration case that would carry the most value. What is left —importing a spreadsheet, exporting indicators— does not need a configuration section: they are one-off actions that belong to the Assets and Analysis screens.'),
('alcance.integracionesFalta1', 'Para la importación: un formato de planilla definido y un informe de qué filas entraron y qué filas no, con el motivo.',
                               'For the import: a defined spreadsheet format and a report of which rows went in and which did not, with the reason.'),
('alcance.integracionesFalta2', 'Para la exportación: decidir si es un archivo que se descarga o un endpoint que otra herramienta consulta, que son dos diseños distintos.',
                               'For the export: deciding whether it is a file to download or an endpoint another tool queries, which are two different designs.'),
('alcance.integracionesFalta3', 'Para el directorio de usuarios: un proveedor de identidad y el manejo de qué pasa cuando alguien se da de baja allá y tiene incidencias abiertas acá.',
                               'For the user directory: an identity provider and handling what happens when someone is removed there and has open incidents here.'),
-- ------------------------------- H-77 (2/3): criticidades y nav de config
('activo.criticidadBaja',        'Baja',                                'Low'),
('activo.criticidadMedia',       'Media',                               'Medium'),
('activo.criticidadAlta',        'Alta',                                'High'),
('activo.criticidadCritica',     'Crítica',                             'Critical'),
('activo.sinEquiposAsignados',   'No tenés equipos asignados',          'You have no assets assigned'),
('error.sinErroresPeriodo',      'Sin errores en el período',           'No errors in the period'),
('error.titulo',                 'Errores del sistema',                 'System errors'),

-- --------------------------------- H-77 (2/3): la regla explicada en palabras
-- Los numeros van como marcadores: en ingles el orden de la frase cambia y
-- una frase armada por concatenacion no se puede reordenar al traducirla.
('regla.jsonInvalidoDetalle',  'La condición no es un JSON válido: esta regla no se va a evaluar.',
                               'The condition is not valid JSON: this rule will not be evaluated.'),
('regla.describeRecurrencia',  'Avisa cuando un equipo acumula {minimo} o más incidencias en {dias} días.',
                               'Warns when an asset accumulates {minimo} or more incidents in {dias} days.'),
('regla.describeAcumulacionMismaCategoria', 'Avisa cuando un equipo acumula {minimo} o más incidencias de la misma categoría en {dias} días.',
                               'Warns when an asset accumulates {minimo} or more incidents of the same category in {dias} days.'),
('regla.describeAcumulacion',  'Avisa cuando un equipo acumula {minimo} o más incidencias, de cualquier categoría, en {dias} días.',
                               'Warns when an asset accumulates {minimo} or more incidents, of any category, in {dias} days.'),
('regla.describeMantenimiento','Avisa cuando pasaron {dias} días desde el último mantenimiento preventivo.',
                               'Warns when {dias} days have passed since the last preventive maintenance.'),
('regla.describeAntiguedad',   'Avisa cuando el equipo cumple {anios} años desde su compra.',
                               'Warns when the asset turns {anios} years old since purchase.'),
('regla.describeGarantia',     'Avisa cuando faltan {dias} días o menos para que venza la garantía.',
                               'Warns when the warranty expires in {dias} days or less.'),

-- --------------------------------- H-77 (3/3): subtitulos, plurales y sueltos
-- Cada forma es una frase completa con su {n} adentro. Armar la frase pegando
-- el numero a un sustantivo funciona en castellano y se rompe en cuanto el
-- idioma pone el numero en otro lugar.
('comun.paginaDeTotal',        'página {pagina} de {total}',       'page {pagina} of {total}'),
('comun.manual',               'Manual',                           'Manual'),
('comun.automatico',           'Automático',                       'Automatic'),
('comun.diaUno',               '1 día',                            '1 day'),
('comun.diaVarios',            '{n} días',                         '{n} days'),

('activo.registradoUno',       '1 equipo registrado',              '1 asset on record'),
('activo.registradoVarios',    '{n} equipos registrados',          '{n} assets on record'),
('activo.operativoEnPaginaUno','1 operativo en esta página',       '1 operational on this page'),
('activo.operativoEnPaginaVarios', '{n} operativos en esta página','{n} operational on this page'),
('activo.fueraDeServicioUno',  '1 fuera de servicio',              '1 out of service'),
('activo.fueraDeServicioVarios', '{n} fuera de servicio',          '{n} out of service'),
('activo.antiguedadAniosUno',  '1 año',                            '1 year'),
('activo.antiguedadAniosVarios', '{n} años',                       '{n} years'),
('activo.antiguedadMesesUno',  '1 mes',                            '1 month'),
('activo.antiguedadMesesVarios', '{n} meses',                      '{n} months'),

('incidencia.contadasUno',     '1 incidencia',                     '1 incident'),
('incidencia.contadasVarios',  '{n} incidencias',                  '{n} incidents'),
('incidencia.abiertaEnPaginaUno', '1 abierta en esta página',      '1 open on this page'),
('incidencia.abiertaEnPaginaVarios', '{n} abiertas en esta página','{n} open on this page'),
('incidencia.contadasSinAsignarUno', '1 sin asignar',              '1 unassigned'),
('incidencia.contadasSinAsignarVarios', '{n} sin asignar',         '{n} unassigned'),
('incidencia.contadasPorRespaldoUno', '1 asignada por respaldo',   '1 assigned by fallback'),
('incidencia.contadasPorRespaldoVarios', '{n} asignadas por respaldo', '{n} assigned by fallback'),

('bitacora.requiereAtencionUno', '1 requiere atención',            '1 needs attention'),
('bitacora.requiereAtencionVarios', '{n} requieren atención',      '{n} need attention'),

('analisis.equipoEvaluadoUno', '1 equipo evaluado',                '1 asset evaluated'),
('analisis.equipoEvaluadoVarios', '{n} equipos evaluados',         '{n} assets evaluated'),
('analisis.reglaActivaUno',    '1 regla activa',                   '1 active rule'),
('analisis.reglaActivaVarios', '{n} reglas activas',               '{n} active rules'),
('analisis.alertaSinAtenderUno', '1 alerta sin atender',           '1 alert pending'),
('analisis.alertaSinAtenderVarios', '{n} alertas sin atender',     '{n} alerts pending'),
('analisis.ultimaEvaluacion',  'última evaluación {fecha}',        'last evaluated {fecha}'),
('analisis.ultimaAlerta',      'última {fecha}',                   'last {fecha}'),
('analisis.nuncaDisparo',      'Nunca disparó',                    'Never fired'),
('analisis.pocosDatos',        'Pocos datos',                      'Not enough data'),
('analisis.umbralBajo',        'Umbral bajo',                      'Threshold too low'),
('analisis.bienCalibrada',     'Bien calibrada',                   'Well calibrated'),
('analisis.razonable',         'Razonable',                        'Reasonable'),

('ia.reemplazarClave',         'Reemplazar la clave',              'Replace the key'),
('ia.cargarClave',             'Cargar una clave',                 'Enter a key'),
('ia.automatica',              'Automática',                       'Automatic'),
('ia.porHeuristicaLargo',      'Reglas automáticas',               'Built-in rules'),

('respaldo.hecho',             'Respaldo de {base} hecho: {archivo} ({tamano}).',
                               'Backup of {base} done: {archivo} ({tamano}).'),
('respaldo.fallo',             'El respaldo de {base} falló: {detalle}',
                               'The backup of {base} failed: {detalle}'),
('respaldo.ultimoCorrecto',    'Último respaldo correcto: {fecha}','Last successful backup: {fecha}'),
('respaldo.haceDiaUno',        'hace 1 día',                       '1 day ago'),
('respaldo.haceDiaVarios',     'hace {n} días',                    '{n} days ago'),

('usuario.unoBloqueado',       '{nombre} está bloqueado por intentos fallidos.',
                               '{nombre} is locked out after failed attempts.'),
('usuario.variosBloqueados',   '{n} usuarios están bloqueados por intentos fallidos.',
                               '{n} users are locked out after failed attempts.'),
('usuario.unUsuario',          'Un usuario',                       'A user'),
('bitacora.mostrandoDeTotal',  'Mostrando {n} de {total} eventos',  'Showing {n} of {total} events'),
('bitacora.mostrando',         'Mostrando {n} eventos',             'Showing {n} events'),
-- --------------------------- H-77: cuanto hace, en una sola escala
('hace.minutos',               'hace {n} min',                     '{n} min ago'),
('hace.horas',                 'hace {n} h',                       '{n} h ago'),
('hace.dias',                  'hace {n} d',                       '{n} d ago'),
('hace.ayer',                  'ayer',                             'yesterday'),
('hace.hoyALas',               'hoy {hora}',                       'today {hora}'),
('hace.unMes',                 'hace 1 mes',                       '1 month ago'),
('hace.meses',                 'hace {n} meses',                   '{n} months ago'),
('analisis.reevaluarAhora',    'Reevaluar ahora',                  'Re-evaluate now'),
('bitacora.fechaHora',         'Fecha y hora',                     'Date and time'),

-- ------------- H-87: los rotulos que la deteccion no veia por no tener acento
--
-- Aparecieron mirando la aplicacion en ingles: no tienen acento ni articulo,
-- asi que la heuristica los daba por no traducibles y el recuento decia cero.
('comun.marca',                'Marca',                            'Brand'),
('comun.modelo',               'Modelo',                           'Model'),
('comun.regla',                'Regla',                            'Rule'),
('comun.detalle',              'Detalle',                          'Detail'),
('comun.opcional',             'Opcional',                         'Optional'),
('comun.acciones',             'Acciones',                         'Actions'),
('comun.correo',               'Correo',                           'Email'),
('comun.incidencia',           'Incidencia',                       'Incident'),
('comun.activo',               'Activo',                           'Enabled'),
('comun.inactivo',             'Inactivo',                         'Disabled'),
('comun.activar',              'Activar',                          'Enable'),
('comun.desactivar',           'Desactivar',                       'Disable'),
('comun.ocultar',              'Ocultar',                          'Hide'),
('comun.probando',             'Probando…',                        'Testing…'),
('calib.atendidas',            'Atendidas',                        'Acted on'),
('calib.descartadas',          'Descartadas',                      'Dismissed'),
('calib.aciertos',             'Aciertos',                         'Hit rate'),
('calib.lectura',              'Lectura',                          'Reading'),
('calib.inactiva',             '· inactiva',                       '· disabled'),
('activo.buscar',              'Buscar equipos',                   'Search assets'),
('activo.estadoOperativo',     'Estado operativo',                 'Operational status'),
('activo.nuevoEstado',         'Nuevo estado',                     'New status'),
('activo.datosAdministrativos','Datos administrativos',            'Administrative data'),
('apar.tema',                  'Tema',                             'Theme'),
('apar.densidad',              'Densidad',                         'Density'),
('apar.vistaPrevia',           'Vista previa',                     'Preview'),
('apar.ejemploAviso',          '47 equipos evaluados · 3 en riesgo alto y 5 en riesgo medio.',
                               '47 assets evaluated · 3 at high risk and 5 at medium risk.'),
('apar.ejemploSecundaria',     'Secundaria',                       'Secondary'),
('apar.ejemploPlana',          'Plana',                            'Flat'),
('garantia.vigente',           'Vigente',                          'Active'),
('garantia.vencida',           'Vencida',                          'Expired'),
('bitacora.origen',            'Origen',                           'Source'),
('errores.incluirAdvertencias','Incluir advertencias',             'Include warnings'),
('historial.ambos',            'Incidencias y mantenimientos',     'Incidents and maintenance'),
('incidencia.datos',           'Datos',                            'Details'),
('incidencia.cerradaEl',       'Cerrada',                          'Closed'),
('incidencia.revisarlas',      'Revisarlas',                       'Review them'),
('incidencia.buscar',          'Buscar incidencias',               'Search incidents'),
('ia.modelo',                  'Modelo',                           'Model'),
('ia.circuito',                'Circuito',                         'Circuit breaker'),
('ia.fallosSeguidos',          'Fallos seguidos',                  'Consecutive failures'),
('ia.guardarCifrada',          'Guardar cifrada',                  'Save encrypted'),
('mant.ejemploResultado',      'Temperatura normalizada',          'Temperature back to normal'),
('mant.buscar',                'Buscar mantenimientos',            'Search maintenance'),
('org.cuit',                   'CUIT',                             'CUIT'),
('predictivo.configurarReglas','Configurar reglas',                'Configure rules'),
('reglas.reglaActiva',         'Regla activa',                     'Rule enabled'),
('respaldos.base',             'Base',                             'Database'),
('respaldos.restaurar',        'Restaurar',                        'Restore'),
('usuarios.permiso',           'Permiso',                          'Permission'),
('usuarios.buscar',            'Buscar usuarios',                  'Search users'),
('activo.antiguedadAniosMeses','{anios} y {meses}',                '{anios} {meses}'),
('garantia.venceEn',           'Vence en {dias} días',             'Expires in {dias} days'),
('activo.crear',               'Crear equipo',                     'Create asset'),
('activo.editarCodigo',        'Editar {codigo}',                  'Edit {codigo}'),
('activo.editarFicha',         'Editar ficha',                     'Edit details'),
('analisis.mostrandoDiez',     'Mostrando 10 de {total}',          'Showing 10 of {total}'),
('analisis.mostrandoDoce',     'Mostrando los 12 de mayor riesgo, de {total} equipos evaluados',
                               'Showing the 12 highest-risk of {total} assets evaluated'),
('analisis.ningunaAlertaNueva','ninguna alerta nueva',             'no new alerts'),
('analisis.alertaNuevaUno',    '1 alerta nueva',                   '1 new alert'),
('analisis.alertaNuevaVarios', '{n} alertas nuevas',               '{n} new alerts'),
('analisis.resultadoEvaluacion','{equipos}: {alertas}, {altos} en riesgo alto y {medios} en riesgo medio.',
                               '{equipos}: {alertas}, {altos} at high risk and {medios} at medium risk.'),
('comun.guardarCambios',       'Guardar cambios',                  'Save changes'),
('dash.incAbreviado',          'Inc.',                             'Inc.'),
('dash.resultadoEvaluacion',   '{equipos} · {alertas}.',           '{equipos} · {alertas}.'),
('dash.alertaActivaUno',       '1 activa',                         '1 active'),
('dash.alertaActivaVarios',    '{n} activas',                      '{n} active'),
('errores.verTraza',           'Ver traza',                        'View trace'),
('ia.proveedorEs',             'Proveedor {proveedor}',            'Provider {proveedor}'),
('incidencia.registrando',     'Registrando…',                     'Submitting…'),
('reglas.activa',              'Activa',                           'Enabled'),
('reglas.inactiva',            'Inactiva',                         'Disabled'),
('usuarios.guardarRoles',      'Guardar roles',                    'Save roles'),

-- ------------ H-87: los titulos de pantalla vacia y los contextos de encabezado
--
-- Viajaban en props propias -`titulo`, `contexto`, `etiqueta`- y la deteccion
-- solo miraba `placeholder`, `title`, `aria-label` y `label`.
('vacio.sinAlertasRegla',      'Ninguna regla generó alertas todavía',
                               'No rule has raised an alert yet'),
('vacio.sinEquipos',           'Todavía no hay equipos cargados',  'No assets recorded yet'),
('vacio.equipoSinCoincidencia','Ningún equipo coincide con la búsqueda',
                               'No asset matches the search'),
('vacio.eventoSinCoincidencia','Ningún evento coincide con los criterios',
                               'No event matches the filters'),
('vacio.sinEventosEnRango',    'No hay eventos en ese rango',      'No events in that range'),
('vacio.sinErrores',           'No se registró ningún error',      'No errors recorded'),
('vacio.errorSinCoincidencia', 'Ningún error coincide con la búsqueda',
                               'No error matches the search'),
('vacio.sinHechos',            'No pasó nada en el período',       'Nothing happened in the period'),
('vacio.hechoSinCoincidencia', 'Ningún hecho coincide con los filtros',
                               'Nothing matches the filters'),
('vacio.incidenciaSinCoincidencia','Ninguna incidencia coincide con la búsqueda',
                               'No incident matches the search'),
('vacio.mantenimientoSinCoincidencia','Ningún mantenimiento coincide con los criterios',
                               'No maintenance record matches the filters'),
('vacio.sinMantenimientos',    'Todavía no hay mantenimientos registrados',
                               'No maintenance recorded yet'),
('vacio.sinEquiposPropios',    'No hay equipos a tu nombre',       'No assets under your name'),
('vacio.sinReglas',            'No hay reglas configuradas',       'No rules configured'),
('vacio.usuarioSinCoincidencia','Ningún usuario coincide con la búsqueda',
                               'No user matches the search'),
('apar.contexto',              'Preferencia de quien usa el sistema, guardada en este navegador',
                               'A preference of whoever is using the system, kept in this browser'),
('org.contexto',               'Datos de la empresa y plan contratado',
                               'Company details and current plan'),
('reportes.contexto',          'Tres reportes en PDF, con los filtros aplicados impresos',
                               'Three PDF reports, with the applied filters printed on them'),
('incidencia.quedoCargado',    'El reporte quedó cargado',         'The report was submitted'),
('ia.contextoCarga',           'Carga de trabajo de cada técnico', 'Each technician''s workload'),
('ia.contextoHistorial',       'Historial sobre el equipo',        'History on the asset'),
('ia.contextoEspecialidad',    'Especialidades y nivel',           'Specialities and level'),
('ia.contextoDisponibilidad',  'Disponibilidad horaria',           'Working hours'),

-- ------------------- H-87: las ayudas de pantalla vacia y las notas al pie
--
-- Viajaban en la prop `detalle`, y la de reglas tenia un punto y coma que la
-- deteccion leia como codigo.
('vacio.calibracionAyuda',
 'Cuando el motor empiece a generar alertas y alguien las atienda o las descarte, acá va a aparecer si cada umbral está bien puesto.',
 'Once the engine starts raising alerts and someone acts on or dismisses them, this will show whether each threshold is set right.'),
('vacio.equipoFiltroAyuda',
 'Probá con menos filtros, o limpiálos para ver el parque completo.',
 'Try fewer filters, or clear them to see the whole fleet.'),
('vacio.sinEquiposAyuda',
 'El parque es la base de todo lo demás: sin equipos no hay incidencias que registrar ni riesgo que calcular.',
 'The fleet is the basis for everything else: with no assets there are no incidents to log and no risk to compute.'),
('vacio.bitacoraAyuda',
 'La bitácora asienta todo lo que pasa en el sistema. Si acá no hay nada, probá ampliando las fechas.',
 'The audit log records everything that happens in the system. If there is nothing here, try widening the dates.'),
('vacio.bitacoraFiltroAyuda',
 'Probá quitando el filtro por tipo o por usuario.',
 'Try removing the type or user filter.'),
('vacio.errorFiltroAyuda',   'Probá con otro texto.',            'Try a different search.'),
('vacio.sinErroresAyuda',
 'Es el resultado esperado. Esta pantalla existe para el día en que no lo sea.',
 'That is the expected outcome. This screen exists for the day it is not.'),
('vacio.hechoFiltroAyuda',
 'Probá con otro texto o quitando el filtro por tipo.',
 'Try a different search, or remove the type filter.'),
('vacio.sinHechosAyuda',
 'Ni incidencias ni mantenimientos. Si esperabas ver algo, probá ampliando las fechas.',
 'No incidents and no maintenance. If you expected something, try widening the dates.'),
('vacio.incidenciaFiltroAyuda',
 'Probá con menos filtros. Si buscabas las abiertas y no hay, es una buena noticia.',
 'Try fewer filters. If you were looking for open ones and there are none, that is good news.'),
('ia.contextoCargaAyuda',
 'Cuántas incidencias abiertas tiene. Permite repartir el trabajo.',
 'How many open incidents each one has. It lets the work be spread out.'),
('ia.contextoHistorialAyuda',
 'Es el dato central: sin él la asignación sólo puede mirar la carga y la calidad cae mucho.',
 'This is the key input: without it the assignment can only look at workload, and quality drops sharply.'),
('ia.contextoEspecialidadAyuda',
 'Cuántos casos de ese mismo equipo ya resolvió cada técnico.',
 'How many cases on that same asset each technician has already solved.'),
('ia.contextoDisponibilidadAyuda',
 'Todavía no se registra en el sistema, así que activarlo no cambia nada por ahora.',
 'It is not recorded in the system yet, so turning it on changes nothing for now.'),
('vacio.sinMantenimientosAyuda',
 'Cada intervención alimenta el historial del equipo, y el historial es lo que el motor predictivo mira para calcular el riesgo.',
 'Every job feeds the asset history, and the history is what the predictive engine looks at to compute risk.'),
('vacio.mantenimientoFiltroAyuda',
 'Probá con menos filtros o con otro rango de fechas.',
 'Try fewer filters or a different date range.'),
('vacio.sinEquiposPropiosAyuda',
 'Si usás uno que debería figurar acá, avisale al responsable técnico para que te lo asigne.',
 'If you use one that should be listed here, ask the technical lead to assign it to you.'),
('vacio.sinReglasAyuda',
 'Sin reglas el análisis predictivo no tiene con qué evaluar el parque: todos los equipos quedan en riesgo cero.',
 'With no rules the predictive analysis has nothing to evaluate the fleet with: every asset stays at zero risk.'),
('regla.comoCalibrarTexto',
 'La señal de que una regla está mal configurada es que sus alertas siempre se descartan. Por eso «atender» y «descartar» son acciones distintas en la pantalla de análisis: la proporción entre ambas es lo que permite evaluar la calibración. Si una regla genera demasiadas alertas, conviene subir su umbral o bajar su peso; si un equipo falló sin haber generado alerta, bajar el umbral de la regla que debió detectarlo.',
 'The sign that a rule is badly configured is that its alerts are always dismissed. That is why «act on» and «dismiss» are separate actions on the analysis screen: the ratio between them is what makes the calibration measurable. If a rule raises too many alerts, raise its threshold or lower its weight; if an asset failed without raising one, lower the threshold of the rule that should have caught it.'),
('vacio.usuarioFiltroAyuda',
 'Probá con parte del nombre, del usuario o del correo.',
 'Try part of the name, the username or the email.'),
('activo.mostrandoDeTotal',    'Mostrando {n} de {total} equipos',  'Showing {n} of {total} assets'),
('incidencia.mostrandoDeTotal','Mostrando {n} de {total} incidencias',
                               'Showing {n} of {total} incidents'),
('ia.enTotal',                 '{total} en total',                 '{total} in total'),
('reporte.equiposEstado',      'Equipos: {estado}',                'Assets: {estado}'),
('reporte.incidenciasEstado',  'Incidencias: {estado}',            'Incidents: {estado}'),
('reporte.generando',          'Generando…',                       'Generating…'),
('reporte.descargarPdf',       'Descargar PDF',                    'Download PDF'),
('regla.pesoAyuda',
 'Peso: {peso} — los pesos no tienen que sumar 100; se normalizan entre las reglas aplicables',
 'Weight: {peso} — weights need not add up to 100; they are normalised across the applicable rules'),
('regla.contexto',             '{reglas} reglas · {activas} activas · peso total {peso}',
                               '{reglas} rules · {activas} enabled · total weight {peso}'),
('riesgo.alto',                'Alto',                             'High'),
('riesgo.medio',               'Medio',                            'Medium'),
('riesgo.bajo',                'Bajo',                             'Low'),
('riesgo.lectura',             'Riesgo {nivel}, puntaje {score} de 100',
                               '{nivel} risk, score {score} out of 100'),
('org.convieneCambiar',
 'Con {equipos} equipos te conviene el {plan}: {costo} por mes, {diferencia} menos que ahora.',
 'With {equipos} assets the {plan} suits you better: {costo} a month, {diferencia} less than now.'),
('org.noConvieneCambiar',
 'Con {equipos} equipos, el {plan} costaría {costo} por mes: {diferencia} más que tu plan actual.',
 'With {equipos} assets, the {plan} would cost {costo} a month: {diferencia} more than your current plan.'),
('org.desdeCuantosConviene',   'Te conviene cambiar a partir de los {equipos} equipos.',
                               'Switching pays off from {equipos} assets onwards.'),
('ia.tiempoDeEspera',          'Tiempo de espera: {segundos} s antes de darlo por no respondido',
                               'Timeout: {segundos} s before treating it as no answer.'),
('comun.equipo',               'equipo',                           'asset'),
('comun.equipos',              'equipos',                          'assets'),
('analisis.generadaHace',      'Generada {cuando}',                'Raised {cuando}'),

-- ------------------------------------- RF-16: el plan de mantenimiento y su agenda
--
-- «Agendado» y «programado» son la misma cosa dicha de dos formas; en la
-- interfaz se dice agenda, que es lo que la gente mira, y en la base
-- MantenimientoProgramado, que es lo que la tabla guarda.
('nav.agenda',                 'Agenda',                           'Schedule'),
('agenda.titulo',              'Agenda de mantenimiento',          'Maintenance schedule'),
('agenda.contexto',            '{vencidos} vencido(s) · {proximos} en los próximos 7 días',
                               '{vencidos} overdue · {proximos} in the next 7 days'),
('agenda.vencidos',            'Vencidos',                         'Overdue'),
('agenda.proximos7',           'Próximos 7 días',                  'Next 7 days'),
('agenda.agendados',           'Agendados',                        'Scheduled'),
('agenda.planesActivos',       'Planes activos',                   'Active plans'),
('agenda.programar',           'Programar mantenimiento',          'Schedule maintenance'),
('agenda.agendar',             'Agendar',                          'Schedule'),
('agenda.generar',             'Generar desde los planes',         'Generate from the plans'),
('agenda.generado',
 'Se agendaron {generados} trabajo(s) a partir de {planes} plan(es). {yaEstaban} ya estaban.',
 '{generados} job(s) scheduled from {planes} plan(s). {yaEstaban} were already there.'),
('agenda.fechaPrevista',       'Fecha prevista',                   'Due date'),
('agenda.nuevaFecha',          'Nueva fecha',                      'New date'),
('agenda.reprogramar',         'Reprogramar',                      'Reschedule'),
('agenda.anular',              'Anular',                           'Cancel'),
('agenda.registrarTrabajo',    'Registrar',                        'Log it'),
('agenda.motivo',              'Motivo',                           'Reason'),
('agenda.motivoOpcional',      'Motivo (opcional)',                'Reason (optional)'),
('agenda.motivoEjemplo',       'Pedido del responsable del área',  'Requested by the area lead'),
('agenda.motivoReprogramar',   'El equipo está en uso hasta fin de mes',
                               'The asset is in use until the end of the month'),
('agenda.motivoAnular',        'El equipo se dio de baja',         'The asset was decommissioned'),
('agenda.buscar',              'Buscar en la agenda',              'Search the schedule'),
('agenda.buscarPlaceholder',   'Equipo, tipo o ubicación…',        'Asset, type or location…'),
('agenda.sinNada',             'No hay trabajos agendados',        'No jobs scheduled'),
('agenda.sinNadaDetalle',
 'Cargá un plan en Configuración › Planes de mantenimiento y generá la agenda, o programá una fecha suelta.',
 'Add a plan under Settings › Maintenance plans and generate the schedule, or schedule a single date.'),

('programado.programados',     'Programados',                      'Scheduled'),
('programado.ejecutados',      'Ejecutados',                       'Done'),
('programado.anulados',        'Anulados',                         'Cancelled'),
('programado.ejecutado',       'Ejecutado',                        'Done'),
('programado.anulado',         'Anulado',                          'Cancelled'),
('programado.hoy',             'Hoy',                              'Today'),
-- El rotulo lleva los dias porque «Programado» describe igual al que vence
-- manana que al que vencio hace dos meses, y en una agenda se mira eso.
('programado.vencidoDias',     'Vencido · {dias} d',               'Overdue · {dias} d'),
('programado.enDias',          'En {dias} d',                      'In {dias} d'),

('plan.titulo',                'Planes de mantenimiento',          'Maintenance plans'),
('plan.contexto',              '{planes} plan(es) · {activos} activo(s) · {equipos} equipo(s) cubierto(s)',
                               '{planes} plan(s) · {activos} active · {equipos} asset(s) covered'),
('plan.nuevo',                 'Nuevo plan',                       'New plan'),
('plan.editar',                'Editar plan',                      'Edit plan'),
('plan.alcance',               'A qué alcanza',                    'What it covers'),
('plan.alcanceAyuda',
 'Un plan por tipo cubre todo el parque de esa clase, incluidos los equipos que se den de alta después. Uno por equipo es para el caso puntual.',
 'A plan by type covers every asset of that class, including those added later. A plan by asset is for the one-off case.'),
('plan.tipoDeEquipo',          'Tipo de equipo',                   'Asset type'),
('plan.unEquipo',              'Un equipo puntual',                'A single asset'),
('plan.queTrabajo',            'Qué trabajo',                      'Which job'),
('plan.cadaCuanto',            'Cada cuántos días',                'Every how many days'),
('plan.cadaDias',              'cada {dias} días',                 'every {dias} days'),
('plan.frecuencia',            'Frecuencia',                       'Frequency'),
('plan.equipos',               'Equipos',                          'Assets'),
('plan.descripcion',           'Descripción (opcional)',           'Description (optional)'),
('plan.descripcionEjemplo',    'Limpieza interna y revisión de ventilación',
                               'Internal cleaning and airflow check'),
('plan.sinPlanes',             'Todavía no hay planes',            'No plans yet'),
('plan.sinPlanesDetalle',
 'Un plan dice cada cuánto le toca el preventivo a un equipo o a todo un tipo de equipo. Sin plan, el sistema estima con un umbral general.',
 'A plan states how often an asset — or a whole asset type — is due for preventive work. With no plan, the system falls back to a generic threshold.'),
-- Nadie dice «cada 180 dias»: dice «semestral». Se muestran las dos cosas,
-- porque el numero es el que define la cuenta.
('plan.semanal',               'semanal',                          'weekly'),
('plan.quincenal',             'quincenal',                        'fortnightly'),
('plan.mensual',               'mensual',                          'monthly'),
('plan.trimestral',            'trimestral',                       'quarterly'),
('plan.semestral',             'semestral',                        'half-yearly'),
('plan.anual',                 'anual',                            'yearly'),

('comun.elegir',               'Elegir…',                          'Choose…'),
('comun.elegirEquipo',         'Elegir equipo…',                   'Choose an asset…'),
('equipo.ubicacion',           'Ubicación',                        'Location'),

-- ------------------------------------------ RF-17: facturación del servicio
--
-- «Comprobante» y no «factura»: no hay CAE, ni punto de venta, ni integración
-- con AFIP. Es el registro interno de lo que la organización paga por usar el
-- sistema, y el rótulo tiene que decir eso y no prometer otra cosa.
('factura.titulo',             'Facturación del servicio',         'Service billing'),
('factura.contexto',           '{pendiente} por cobrar · {vencidos} vencido(s)',
                               '{pendiente} outstanding · {vencidos} overdue'),
('factura.periodo',            'Período',                          'Period'),
('factura.numero',             'N.º',                              'No.'),
('factura.total',              'Total',                            'Total'),
('factura.subtotal',           'Subtotal',                         'Subtotal'),
('factura.iva',                'IVA',                              'VAT'),
('factura.concepto',           'Concepto',                         'Item'),
('factura.cantidad',           'Cantidad',                         'Quantity'),
('factura.precioUnitario',     'Precio unitario',                  'Unit price'),
('factura.importe',            'Importe',                          'Amount'),

('factura.porCobrar',          'Por cobrar',                       'Outstanding'),
('factura.vencido',            'Vencido',                          'Overdue'),
('factura.ultimos12',          'Facturado en 12 meses',            'Billed over 12 months'),
('factura.periodoSugerido',    'Último mes cerrado',               'Last closed month'),
('factura.yaEstaFacturado',    'Ya facturado',                     'Already billed'),
('factura.sinFacturar',        'Sin facturar',                     'Not billed yet'),

('factura.generarPeriodo',     'Facturar el último mes',           'Bill the last month'),
('factura.generado',           'Borrador del período {periodo} generado por {total}.',
                               'Draft for {periodo} created for {total}.'),
('factura.yaFacturado',        'El período {periodo} ya estaba facturado.',
                               'Period {periodo} was already billed.'),

('factura.emitir',             'Emitir',                           'Issue'),
('factura.registrarPago',      'Registrar el pago',                'Record the payment'),
('factura.anular',             'Anular',                           'Void'),
('factura.descartar',          'Descartar',                        'Discard'),
('factura.motivo',             'Motivo',                           'Reason'),
('factura.motivoEjemplo',      'Se facturó con el plan equivocado','It was billed under the wrong plan'),
('factura.motivoAnulacion',    'Motivo de la anulación',           'Reason for voiding'),

('factura.ajuste',             'Ajuste',                           'Adjustment'),
('factura.ajusteConcepto',     'Ajuste',                           'Adjustment'),
('factura.ajusteEjemplo',      'Descuento por pago adelantado',    'Early payment discount'),
-- El signo va en el rótulo: un ajuste negativo es como se carga un descuento,
-- sin necesitar un tipo de comprobante aparte.
('factura.importeConSigno',    'Importe (+/−)',                    'Amount (+/−)'),
('factura.agregarAjuste',      'Agregar ajuste',                   'Add adjustment'),

('factura.borrador',           'Borrador',                         'Draft'),
('factura.emitidoEstado',      'Emitido',                          'Issued'),
('factura.pagado',             'Cobrado',                          'Paid'),
('factura.anulado',            'Anulado',                          'Voided'),
('factura.vencidoDias',        'Vencido · {dias} d',               'Overdue · {dias} d'),
('factura.emitido',            'Emitido',                          'Issued'),
('factura.vence',              'Vence',                            'Due'),
('factura.cobrado',            'Cobrado',                          'Paid'),

('factura.sinComprobantes',    'Todavía no hay comprobantes',      'No invoices yet'),
('factura.sinComprobantesDetalle',
 'El comprobante sale del plan contratado y de los equipos administrados al cierre del mes. Facturá el último mes cerrado para empezar.',
 'The invoice comes from the contracted plan and the assets managed at month end. Bill the last closed month to start.'),

('comun.confirmar',            'Confirmar',                        'Confirm');

/* ------------------------------------------------------------------ carga */
MERGE dbo.Traduccion AS d
USING (
    SELECT @es AS id_idioma, clave, es AS texto FROM @t
    UNION ALL
    SELECT @en AS id_idioma, clave, en AS texto FROM @t
) AS s
ON d.id_idioma = s.id_idioma AND d.clave = s.clave
WHEN NOT MATCHED THEN
    INSERT (id_traduccion, id_idioma, clave, texto)
    VALUES (NEWID(), s.id_idioma, s.clave, s.texto)
/* Se actualiza el texto: asi una correccion de redaccion se aplica volviendo
   a correr el script, sin tener que borrar filas a mano. */
WHEN MATCHED AND d.texto <> s.texto THEN UPDATE SET texto = s.texto
/* Y la clave que se fue del script se va de la base. Este archivo es la lista
   completa: sin esta rama, retirar un texto lo deja cargado para siempre, y
   una fila que ninguna pantalla pide es una pregunta sin buena respuesta.
   Paso en el rediseno 03 con los tres temas que se dieron de baja. */
WHEN NOT MATCHED BY SOURCE THEN DELETE;
GO

/* Un indice por idioma y clave: la consulta que trae el diccionario completo
   filtra por idioma, y la que resuelve una clave suelta busca por las dos. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Traduccion_idioma_clave')
CREATE UNIQUE NONCLUSTERED INDEX UX_Traduccion_idioma_clave
    ON dbo.Traduccion(id_idioma, clave);
GO

DECLARE @claves INT = (SELECT COUNT(DISTINCT clave) FROM dbo.Traduccion);
DECLARE @filas  INT = (SELECT COUNT(*) FROM dbo.Traduccion);
PRINT 'Traducciones cargadas: ' + CAST(@claves AS VARCHAR) + ' claves, '
      + CAST(@filas AS VARCHAR) + ' filas.';

/* Control: si un idioma activo tiene menos claves que otro, hay traducciones
   faltantes y conviene verlo al aplicar el script, no en pantalla. */
DECLARE @faltantes INT = (
    SELECT COUNT(*)
    FROM (SELECT DISTINCT clave FROM dbo.Traduccion) c
    CROSS JOIN dbo.Idioma i
    WHERE i.activo = 1
      AND NOT EXISTS (SELECT 1 FROM dbo.Traduccion t
                      WHERE t.clave = c.clave AND t.id_idioma = i.id_idioma)
);
IF @faltantes > 0
    PRINT 'ATENCION: faltan ' + CAST(@faltantes AS VARCHAR) + ' traducciones.';
ELSE
    PRINT 'Todos los idiomas activos tienen todas las claves.';
GO
