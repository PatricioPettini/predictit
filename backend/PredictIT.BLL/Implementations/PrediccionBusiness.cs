using System.Text.Json;
using PredictIT.BLL.Contracts;
using PredictIT.BLL.DTO;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Factory;
using PredictIT.DAO.Helpers;
using PredictIT.Domain.Negocio;
using PredictIT.Domain.Seguridad;
using PredictIT.Service.IA;
using PredictIT.Service.Prediccion;
using PredictIT.Service.Seguridad;

namespace PredictIT.BLL.Implementations;

/// <summary>
/// Análisis predictivo (RF-11, RF-12, RF-13, CU-010, CU-011).
///
/// El motor está en la capa de servicio y es lógica pura sin acceso a datos. Acá
/// se le arma el contexto, se persiste el resultado y se decide cuándo una regla
/// cumplida amerita una alerta nueva.
/// </summary>
public class PrediccionBusiness(
    IFactoryDao dao,
    IFactoryDaoSeguridad seguridad,
    IContextoSesion contexto,
    IBitacoraService bitacora,
    IProveedorIA proveedorIa,
    MotorPredictivo motor) : IPrediccionBusiness
{
    /// <summary>
    /// Ventana de historial que se le pasa al motor.
    ///
    /// Es el mayor de los alcances que usan las reglas del seed (90 días) con
    /// margen: traer todo el historial de cada equipo para evaluar reglas de
    /// 30 y 90 días sería leer de más en cada evaluación del parque.
    /// </summary>
    private const int DiasDeHistorial = 400;

    public RiesgoEquipoDto EvaluarEquipo(Guid idEquipo)
    {
        Exigir(Patentes.AlertaVer);

        var equipo = dao.Equipos.GetById(idEquipo)
                     ?? throw new BusinessException("El equipo no existe o no es de tu organización.");

        var riesgo = Evaluar(equipo, dao.Prediccion.ReglasDeLaOrganizacion(), DateTime.Now);
        Persistir(equipo, riesgo, generarAlertas: true);

        return ADto(equipo, riesgo, DateTime.Now);
    }

    public ResultadoEvaluacionDto EvaluarParque()
    {
        Exigir(Patentes.AlertaVer);

        var reglas = dao.Prediccion.ReglasDeLaOrganizacion();
        var ahora = DateTime.Now;

        if (reglas.Count == 0)
        {
            // Sin reglas no hay análisis posible. Decirlo es mejor que devolver
            // ceros, que se leen como «no hay nada en riesgo».
            throw new BusinessException(
                "No hay reglas de alerta configuradas: el análisis predictivo no tiene con qué evaluar.");
        }

        var equipos = dao.Equipos.GetAll()
            // Un equipo dado de baja no se evalúa: no hay nada que prevenir.
            .Where(e => e.Estado?.Operativo ?? true)
            .ToList();

        var alertasNuevas = 0;
        var altos = 0;
        var medios = 0;

        foreach (var equipo in equipos)
        {
            var riesgo = Evaluar(equipo, reglas, ahora);
            alertasNuevas += Persistir(equipo, riesgo, generarAlertas: true);

            if (riesgo.Nivel == NivelRiesgo.Alto) altos++;
            else if (riesgo.Nivel == NivelRiesgo.Medio) medios++;
        }

        bitacora.Registrar(TipoEvento.Codigos.AnalisisPredictivo,
            $"Evaluación del parque: {equipos.Count} equipos, {alertasNuevas} alertas nuevas, " +
            $"{altos} en riesgo alto y {medios} en riesgo medio.");

        return new ResultadoEvaluacionDto(equipos.Count, alertasNuevas, altos, medios);
    }

    public int ReevaluarPorEvento(Guid idEquipo)
    {
        try
        {
            var equipo = dao.Equipos.GetById(idEquipo);

            // Un equipo dado de baja no se evalúa: no hay nada que prevenir.
            if (equipo is null || !(equipo.Estado?.Operativo ?? true)) return 0;

            var reglas = dao.Prediccion.ReglasDeLaOrganizacion();
            if (reglas.Count == 0) return 0;

            var riesgo = Evaluar(equipo, reglas, Reloj.Ahora);
            var nuevas = Persistir(equipo, riesgo, generarAlertas: true);

            // Sólo se asienta si algo cambió. Una línea de bitácora por cada
            // incidencia registrada diciendo «no pasó nada» haría ilegible la
            // pantalla de auditoría.
            if (nuevas > 0)
            {
                bitacora.Registrar(TipoEvento.Codigos.AnalisisPredictivo,
                    $"Reevaluación automática de {equipo.Codigo} tras un hecho registrado: " +
                    $"score {riesgo.Score}, {nuevas} alerta(s) nueva(s).",
                    contexto.IdUsuario, contexto.IdOrganizacion, "Equipo", equipo.Id);
            }

            return nuevas;
        }
        catch (Exception ex)
        {
            // Deliberadamente no se propaga. La incidencia ya está guardada y
            // el usuario no tiene por qué ver un error de un proceso que no
            // pidió. Queda el rastro en bitácora.
            bitacora.Registrar(TipoEvento.Codigos.ErrorSistema,
                $"Falló la reevaluación predictiva del equipo {idEquipo}: {ex.Message}",
                contexto.IdUsuario, contexto.IdOrganizacion, "Equipo", idEquipo,
                traza: ex.ToString());

            return 0;
        }
    }

    public IReadOnlyList<AlertaDto> AlertasActivas()
    {
        Exigir(Patentes.AlertaVer);

        return dao.Prediccion.AlertasActivas().Select(a => new AlertaDto(
            a.Id, a.IdEquipo, a.Equipo?.Codigo ?? string.Empty,
            a.Regla?.Nombre ?? string.Empty,
            a.Estado?.Nombre ?? string.Empty,
            a.Motivo, a.Recomendacion, a.NivelRiesgo,
            a.FechaGeneracion, a.FechaAtencion)).ToList();
    }

    public PanelPredictivoDto Panel()
    {
        Exigir(Patentes.AlertaVer);

        var evaluaciones = dao.Prediccion.UltimasEvaluaciones();
        var alertas = AlertasActivas();
        var reglas = dao.Prediccion.ReglasDeLaOrganizacion();

        // Qué reglas dispararon en cada equipo sale de las alertas activas y no
        // de reinterpretar el JSON del detalle: la alerta es el registro de que
        // la regla se cumplió, y así las dos partes de la pantalla no pueden
        // contradecirse.
        var porEquipo = alertas
            .GroupBy(a => a.IdEquipo)
            .ToDictionary(g => g.Key, g => g.Select(a => a.Regla).Distinct().ToList());

        var ranking = evaluaciones.Select(e => new FilaRankingDto(
            e.Evaluacion.IdEquipo,
            e.CodigoEquipo,
            e.Evaluacion.Score,
            NivelRiesgoTexto.A(e.Evaluacion.Nivel),
            e.Evaluacion.Fecha,
            porEquipo.TryGetValue(e.Evaluacion.IdEquipo, out var r) ? r : [])).ToList();

        return new PanelPredictivoDto(
            ranking.Count,
            evaluaciones.Count == 0 ? null : evaluaciones.Max(e => e.Evaluacion.Fecha),
            reglas.Count,
            alertas.Count(a => a.FechaAtencion is null),
            ranking.Count(f => f.Nivel == "ALTO"),
            ranking.Count(f => f.Nivel == "MEDIO"),
            ranking.Count(f => f.Nivel == "BAJO"),
            ranking,
            alertas);
    }

    public void AtenderAlerta(Guid idAlerta, bool descartar)
    {
        Exigir(Patentes.AlertaVer);

        var alerta = dao.Prediccion.AlertasActivas().FirstOrDefault(a => a.Id == idAlerta)
                     ?? throw new BusinessException("La alerta no existe, no es tuya o ya está cerrada.");

        dao.Prediccion.AtenderAlerta(idAlerta, contexto.IdUsuario, descartar);

        bitacora.Registrar(TipoEvento.Codigos.AlertaAtendida,
            $"Alerta sobre {alerta.Equipo?.Codigo} " +
            (descartar ? "descartada" : "marcada como atendida") + $": {alerta.Motivo}");
    }

    public IReadOnlyList<ReglaDto> Reglas()
    {
        Exigir(Patentes.AlertaVer);

        return dao.Prediccion.ReglasDeLaOrganizacion(soloActivas: false)
            .Select(r => new ReglaDto(
                r.Id, r.Nombre, r.Descripcion, TipoReglaTexto.A(r.Tipo),
                r.Condicion, r.Peso, r.Activa))
            .ToList();
    }

    public Guid GuardarRegla(ReglaDto entrada)
    {
        Exigir(Patentes.ReglaGestionar);

        var nombre = (entrada.Nombre ?? string.Empty).Trim();
        if (nombre.Length < 3)
            throw BusinessException.De("nombre", "La regla necesita un nombre de al menos 3 caracteres.");

        if (entrada.Peso is < 0 or > 100)
            throw BusinessException.De("peso", "El peso va de 0 a 100.");

        var tipo = TipoReglaTexto.De(entrada.Tipo)
                   ?? throw BusinessException.De("tipo", "El tipo de regla no es uno de los soportados.");

        // La condición se valida acá y no se deja que falle al evaluar: un JSON
        // roto guardado invalida la regla en silencio, y el usuario se enteraría
        // cuando el sistema deje de avisarle de un equipo.
        var condicion = string.IsNullOrWhiteSpace(entrada.Condicion) ? "{}" : entrada.Condicion.Trim();
        try
        {
            using var doc = JsonDocument.Parse(condicion);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw BusinessException.De("condicion", "La condición tiene que ser un objeto JSON.");
        }
        catch (JsonException)
        {
            throw BusinessException.De("condicion", "La condición no es un JSON válido.");
        }

        var regla = new ReglaAlerta
        {
            Id = entrada.Id,
            Nombre = nombre,
            Descripcion = entrada.Descripcion,
            Tipo = tipo,
            Condicion = condicion,
            Peso = entrada.Peso,
            Activa = entrada.Activa
        };

        dao.Prediccion.GuardarRegla(regla);

        bitacora.Registrar(TipoEvento.Codigos.ReglaModificada,
            $"Regla «{regla.Nombre}» ({TipoReglaTexto.A(regla.Tipo)}), peso {regla.Peso}, " +
            (regla.Activa ? "activa" : "inactiva") + $": {regla.Condicion}");

        return regla.Id;
    }

    public EstadoIaDto EstadoDeLaIa()
    {
        Exigir(Patentes.AlertaVer);

        var configuracion = dao.ConfiguracionAsignacion.DeLaOrganizacion();
        var o = EstadoProveedor.Observar(proveedorIa);

        return new EstadoIaDto(
            o.Proveedor,
            o.Estado.ToString().ToUpperInvariant(),
            o.FallosConsecutivos,
            o.SinRespuestaDesde,
            configuracion?.UltimaConsultaOk,
            configuracion?.UltimoError,
            o.Mensaje);
    }

    public DashboardDto Dashboard()
    {
        Exigir(Patentes.DashboardVer);

        var equipos = dao.Equipos.GetAll();
        var operativos = equipos.Count(e => e.Estado?.Operativo ?? false);

        var abiertas = dao.Incidencias.Buscar(new FiltroIncidencias
        {
            SoloAbiertas = true,
            PorPagina = 200
        });

        var alertas = AlertasActivas();
        var evaluaciones = dao.Prediccion.UltimasEvaluaciones();

        var sinAsignar = abiertas.Items.Count(i => i.TipoAsignacion == TipoAsignacion.Pendiente);
        var porRevisar = abiertas.Items.Count(i => i.PendienteRevision);
        var deEstaSemana = alertas.Count(a => a.FechaGeneracion >= DateTime.Now.AddDays(-7));
        var enAlto = evaluaciones.Count(e => e.Evaluacion.Nivel == NivelRiesgo.Alto);

        // Cada indicador viaja como clave, no como frase armada: el rótulo y el
        // detalle los resuelve el diccionario del idioma activo. Los detalles
        // que dependen de una cantidad eligen acá entre singular y plural
        // —quién decide eso es una regla del idioma y no del negocio, pero la
        // condición «cuántos hay» sí es del negocio— y el número viaja aparte.
        var indicadores = new List<IndicadorDto>
        {
            new("dash.equiposActivos", operativos.ToString(),
                "dash.deRegistrados",
                new Dictionary<string, string> { ["total"] = equipos.Count.ToString() }),

            new("dash.incidenciasAbiertas", abiertas.Total.ToString(),
                sinAsignar == 0
                    ? "dash.todasAsignadas"
                    : sinAsignar == 1 ? "dash.unaSinAsignar" : "dash.variasSinAsignar",
                new Dictionary<string, string> { ["cantidad"] = sinAsignar.ToString() }),

            new("dash.alertas", alertas.Count.ToString(),
                deEstaSemana == 1 ? "dash.unaEstaSemana" : "dash.variasEstaSemana",
                new Dictionary<string, string> { ["cantidad"] = deEstaSemana.ToString() }),

            new("dash.enRiesgoAlto", enAlto.ToString(),
                enAlto == 0 ? "dash.sinIntervenciones" : "dash.requierenIntervencion"),
        };

        if (porRevisar > 0)
        {
            // Sólo aparece cuando hay algo que revisar: es la señal de que el
            // proveedor de IA estuvo caído y alguien tiene que confirmar a quién
            // se le asignaron las incidencias (ADR 0009).
            indicadores.Add(new IndicadorDto(
                "dash.porRespaldo", porRevisar.ToString(), "dash.pendientesDeRevision"));
        }

        // Cuántas incidencias abiertas tiene cada equipo, para la tabla de riesgo.
        var incidenciasPorEquipo = abiertas.Items
            .GroupBy(i => i.IdEquipo)
            .ToDictionary(g => g.Key, g => g.Count());

        var reglasPorEquipo = alertas
            .GroupBy(a => a.IdEquipo)
            .ToDictionary(g => g.Key, g => g.ToList());

        var responsables = MapaDeResponsables();
        var porId = equipos.ToDictionary(e => e.Id);

        var enRiesgo = evaluaciones
            .Where(e => porId.ContainsKey(e.Evaluacion.IdEquipo))
            .Take(7)
            .Select(e =>
            {
                var equipo = porId[e.Evaluacion.IdEquipo];
                var suyas = reglasPorEquipo.TryGetValue(equipo.Id, out var lista) ? lista : [];

                // El motivo principal es el de la alerta de mayor riesgo. Si no
                // hay alertas, el score viene de reglas que no llegaron a su
                // umbral, y decirlo es mejor que dejar la celda vacía.
                var motivo = suyas.Count == 0
                    ? "Ninguna regla alcanzó su umbral"
                    : suyas.OrderByDescending(a => a.NivelRiesgo).First().Regla;

                return new EquipoEnRiesgoDto(
                    equipo.Id,
                    equipo.Codigo,
                    NombreDe(responsables, equipo.IdResponsable),
                    equipo.Ubicacion?.Nombre,
                    incidenciasPorEquipo.TryGetValue(equipo.Id, out var n) ? n : 0,
                    e.Evaluacion.Score,
                    NivelRiesgoTexto.A(e.Evaluacion.Nivel),
                    motivo);
            })
            .ToList();

        return new DashboardDto(
            indicadores,
            enRiesgo,
            alertas.OrderByDescending(a => a.FechaGeneracion).Take(5).ToList());
    }

    /// <summary>
    /// Nombres de los responsables de equipo. Viven en la base de servicio, así
    /// que se resuelven en una sola consulta y se cruzan en memoria (ADR 0003).
    /// </summary>
    private IDictionary<Guid, string> MapaDeResponsables() =>
        seguridad.Usuarios
            .PorOrganizacion(contexto.IdOrganizacion)
            .ToDictionary(u => u.Id, u => u.NombreCompleto);

    private static string? NombreDe(IDictionary<Guid, string> mapa, Guid? id) =>
        id is { } valor && mapa.TryGetValue(valor, out var nombre) ? nombre : null;

    // ------------------------------------------------------------- privados

    private RiesgoEquipo Evaluar(Equipo equipo, IList<ReglaAlerta> reglas, DateTime ahora)
    {
        var desde = ahora.AddDays(-DiasDeHistorial);

        var ctx = new ContextoEquipo(
            equipo,
            dao.Incidencias.DeEquipoDesde(equipo.Id, desde).ToList(),
            dao.Mantenimientos.DeEquipoDesde(equipo.Id, desde).ToList(),
            ahora)
        {
            Programados = dao.MantenimientosProgramados.PendientesDeEquipo(equipo.Id).ToList(),
        };

        return motor.Evaluar(ctx, reglas);
    }

    /// <summary>
    /// Guarda la evaluación y, si corresponde, genera las alertas. Devuelve
    /// cuántas alertas nuevas creó.
    /// </summary>
    private int Persistir(Equipo equipo, RiesgoEquipo riesgo, bool generarAlertas)
    {
        var detalle = JsonSerializer.Serialize(riesgo.Resultados.Select(r => new
        {
            regla = r.Regla.Nombre,
            tipo = TipoReglaTexto.A(r.Regla.Tipo),
            peso = r.Regla.Peso,
            aplicable = r.Aplicable,
            cumple = r.Cumple,
            intensidad = Math.Round(r.Intensidad, 3),
            motivo = r.Motivo
        }));

        dao.Prediccion.GuardarEvaluacion(new EvaluacionRiesgo
        {
            IdEquipo = equipo.Id,
            Fecha = DateTime.Now,
            Score = riesgo.Score,
            Nivel = riesgo.Nivel,
            Detalle = detalle
        });

        if (!generarAlertas) return 0;

        var estadoInicial = dao.Catalogos.EstadoAlertaInicial();
        if (estadoInicial is null) return 0;

        var nuevas = 0;

        foreach (var disparada in riesgo.Disparadas)
        {
            // Una alerta por regla y equipo mientras siga abierta. Sin este
            // control, cada evaluación duplicaría las mismas alertas y en una
            // semana la pantalla sería inservible.
            if (dao.Prediccion.AlertaActiva(equipo.Id, disparada.Regla.Id) is not null) continue;

            dao.Prediccion.GuardarAlerta(new AlertaPredictiva
            {
                IdEquipo = equipo.Id,
                IdRegla = disparada.Regla.Id,
                IdEstado = estadoInicial.Id,
                FechaGeneracion = DateTime.Now,
                Motivo = $"{equipo.Codigo}: {disparada.Motivo}",
                Recomendacion = disparada.Recomendacion,
                NivelRiesgo = riesgo.Score
            });

            bitacora.Registrar(TipoEvento.Codigos.AlertaGenerada,
                $"{disparada.Regla.Nombre} en {equipo.Codigo} (score {riesgo.Score}): {disparada.Motivo}",
                contexto.IdUsuario, contexto.IdOrganizacion, "Equipo", equipo.Id);

            nuevas++;
        }

        return nuevas;
    }

    private static RiesgoEquipoDto ADto(Equipo equipo, RiesgoEquipo riesgo, DateTime fecha) =>
        new(equipo.Id, equipo.Codigo, riesgo.Score, NivelRiesgoTexto.A(riesgo.Nivel), fecha,
            riesgo.Resultados.Select(r => new AporteReglaDto(
                r.Regla.Nombre, r.Regla.Peso, r.Aplicable, r.Cumple,
                Math.Round(r.Intensidad, 3), r.Motivo)).ToList());

    /// <summary>
    /// Mensaje para el administrador. Un sistema degradado que no lo dice es
    /// peor que uno caído (ADR 0009).
    /// </summary>
    private static string Mensaje(EstadoDisyuntor estado, DateTime? desde) => estado switch
    {
        EstadoDisyuntor.Abierto when desde is { } d =>
            $"Sin respuesta desde hace {Minutos(d)} min: se está aplicando la regla de respaldo.",
        EstadoDisyuntor.Abierto =>
            "Sin respuesta del proveedor: se está aplicando la regla de respaldo.",
        EstadoDisyuntor.Semiabierto =>
            "Se va a probar una consulta para ver si el proveedor volvió.",
        _ => "El proveedor está respondiendo con normalidad.",
    };

    private static int Minutos(DateTime desde) =>
        Math.Max(0, (int)(DateTime.UtcNow - desde).TotalMinutes);

    private void Exigir(string patente)
    {
        if (!contexto.Tiene(patente)) throw new PermisoDenegadoException(patente);
    }
    /// <summary>
    /// Rendimiento de cada regla, para calibrar los umbrales con datos (H-48).
    ///
    /// Se listan todas las reglas, incluidas las que nunca dispararon: una
    /// regla que en meses no generó una sola alerta también es un dato, y si
    /// sólo se listaran las que tienen alertas esa no aparecería nunca.
    /// </summary>
    public IReadOnlyList<CalibracionReglaDto> Calibracion()
    {
        Exigir(Patentes.AlertaVer);

        var recuento = dao.Prediccion.RecuentoPorRegla();

        return dao.Prediccion.ReglasDeLaOrganizacion(soloActivas: false)
            .Select(r =>
            {
                var suyas = recuento.Where(x => x.IdRegla == r.Id).ToList();

                int Con(string estado) => suyas
                    .Where(x => string.Equals(x.Estado, estado, StringComparison.OrdinalIgnoreCase))
                    .Sum(x => x.Cantidad);

                var nuevas = Con("Nueva");
                var atendidas = Con("Atendida");
                var descartadas = Con("Descartada");
                var resueltas = atendidas + descartadas;

                return new CalibracionReglaDto(
                    r.Id, r.Nombre, r.Activa, r.Peso,
                    nuevas + resueltas, nuevas, atendidas, descartadas,
                    resueltas == 0 ? null : Math.Round((double)atendidas / resueltas, 3),
                    suyas.Count == 0 ? null : suyas.Max(x => x.Ultima));
            })
            .OrderByDescending(c => c.Generadas)
            .ThenBy(c => c.Regla)
            .ToList();
    }

}
