using PredictIT.BLL.Contracts;
using PredictIT.BLL.DTO;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Factory;
using PredictIT.DAO.Helpers;
using PredictIT.Domain.Negocio;
using PredictIT.Domain.Seguridad;
using PredictIT.Service.Reportes;
using PredictIT.Service.Seguridad;

namespace PredictIT.BLL.Implementations;

/// <summary>
/// Reportes exportables (RF-14, patente REPORTE_GENERAR).
///
/// Los tres reportes son tres definiciones de columnas sobre datos que el
/// sistema ya tiene, no tres consultas nuevas: el parque sale del mismo listado
/// que la pantalla de activos, y las incidencias del mismo buscador. Si el
/// reporte trajera sus propios datos, la pantalla y el papel podrían decir
/// cosas distintas sobre el mismo día.
/// </summary>
public class ReporteBusiness(
    IFactoryDao dao,
    IFactoryDaoSeguridad seguridad,
    IContextoSesion contexto,
    IBitacoraService bitacora,
    IReporteService reportes) : IReporteBusiness
{
    public IReadOnlyList<CatalogoItemDto> Disponibles()
    {
        Exigir(Patentes.ReporteGenerar);

        return
        [
            new CatalogoItemDto(Guid.Empty, "parque"),
            new CatalogoItemDto(Guid.Empty, "incidencias"),
            new CatalogoItemDto(Guid.Empty, "mantenimientos")
        ];
    }

    public ReporteGeneradoDto Generar(string reporte, FiltroReporteDto filtro)
    {
        Exigir(Patentes.ReporteGenerar);

        var clave = (reporte ?? string.Empty).Trim().ToLowerInvariant();
        var hasta = (filtro.Hasta ?? DateTime.Today).Date.AddDays(1);
        var desde = (filtro.Desde ?? hasta.AddDays(-90)).Date;

        if (desde > hasta)
            throw BusinessException.De("desde", "La fecha «desde» es posterior a «hasta».");

        var (nombre, bytes, filas) = clave switch
        {
            "parque" => Parque(filtro),
            "incidencias" => Incidencias(filtro, desde, hasta),
            "mantenimientos" => Mantenimientos(filtro, desde, hasta),
            _ => throw BusinessException.De("reporte",
                    "Los reportes disponibles son: parque, incidencias y mantenimientos.")
        };

        // Se asienta quién generó qué y con qué filtros. Un reporte impreso
        // circula fuera del sistema: saber que salió de acá, cuándo y con qué
        // recorte es lo único que después permite explicarlo.
        bitacora.Registrar(TipoEvento.Codigos.ReporteGenerado,
            $"{contexto.Username} generó el reporte «{clave}» con {filas} " +
            $"{(filas == 1 ? "registro" : "registros")}.",
            contexto.IdUsuario, contexto.IdOrganizacion);

        return new ReporteGeneradoDto(nombre, "application/pdf", bytes, filas);
    }

    // ─────────────────────────────────────────────────────── los tres reportes

    private (string, byte[], int) Parque(FiltroReporteDto filtro)
    {
        var estados = dao.Catalogos.EstadosDeEquipo().ToDictionary(e => e.Id, e => e.Nombre);
        var tipos = dao.Catalogos.TiposDeEquipo().ToDictionary(t => t.Id, t => t.Nombre);
        var ubicaciones = dao.Catalogos.Ubicaciones().ToDictionary(u => u.Id, u => u.Nombre);
        var nombres = MapaDeUsuarios();

        var equipos = dao.Equipos.GetAll()
            .Where(e => filtro.IdEstado is null || e.IdEstadoEquipo == filtro.IdEstado)
            .OrderBy(e => e.Codigo)
            .ToList();

        // El riesgo se cruza desde la última evaluación de cada equipo. Es el
        // dato que hace que este reporte sirva para decidir y no sólo para
        // inventariar.
        var riesgo = dao.Prediccion.UltimasEvaluaciones()
            .GroupBy(e => e.Evaluacion.IdEquipo)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Evaluacion.Fecha).First().Evaluacion);

        var bytes = reportes.Listado(
            Encabezado("Parque informático", Filtros(("Estado", NombreDe(estados, filtro.IdEstado) ?? "todos"))),
            equipos,
            [
                new ColumnaReporte<Equipo>("Código", e => e.Codigo, 1.1f),
                new ColumnaReporte<Equipo>("Tipo", e => NombreDe(tipos, e.IdTipoEquipo) ?? "—", 1.1f),
                new ColumnaReporte<Equipo>("Marca y modelo",
                    e => string.Join(' ', new[] { e.Marca, e.Modelo }.Where(x => !string.IsNullOrWhiteSpace(x))), 1.7f),
                new ColumnaReporte<Equipo>("Ubicación", e => NombreDe(ubicaciones, e.IdUbicacion) ?? "—", 1.2f),
                new ColumnaReporte<Equipo>("Responsable", e => NombreDe(nombres, e.IdResponsable) ?? "—", 1.3f),
                new ColumnaReporte<Equipo>("Estado", e => NombreDe(estados, e.IdEstadoEquipo) ?? "—", 1.1f),
                new ColumnaReporte<Equipo>("Riesgo",
                    e => riesgo.TryGetValue(e.Id, out var r) ? $"{r.Score} {NivelRiesgoTexto.A(r.Nivel)}" : "sin evaluar",
                    1f, ALaDerecha: true)
            ],
            [
                ("equipos", equipos.Count.ToString()),
                ("operativos", equipos.Count(e => e.Estado?.Operativo ?? false).ToString()),
                ("en riesgo alto", riesgo.Count(r => equipos.Any(e => e.Id == r.Key)
                                                     && r.Value.Nivel == NivelRiesgo.Alto).ToString())
            ]);

        return ($"parque-{DateTime.Today:yyyyMMdd}.pdf", bytes, equipos.Count);
    }

    private (string, byte[], int) Incidencias(FiltroReporteDto filtro, DateTime desde, DateTime hasta)
    {
        var estados = dao.Catalogos.EstadosDeIncidencia().ToDictionary(e => e.Id, e => e.Nombre);
        var prioridades = dao.Catalogos.Prioridades().ToDictionary(p => p.Id, p => p.Nombre);
        var equipos = dao.Equipos.GetAll().ToDictionary(e => e.Id, e => e.Codigo);
        var nombres = MapaDeUsuarios();

        var incidencias = dao.Incidencias.GetAll()
            .Where(i => i.Fecha >= desde && i.Fecha < hasta)
            .Where(i => filtro.IdEstado is null || i.IdEstado == filtro.IdEstado)
            .Where(i => filtro.IdTecnico is null || i.IdTecnico == filtro.IdTecnico)
            .OrderByDescending(i => i.Fecha)
            .ToList();

        var resueltas = incidencias.Where(i => i.FechaResolucion is not null).ToList();

        var bytes = reportes.Listado(
            Encabezado("Incidencias", Filtros(
                ("Período", $"{desde:dd/MM/yyyy} a {hasta.AddDays(-1):dd/MM/yyyy}"),
                ("Estado", NombreDe(estados, filtro.IdEstado) ?? "todos"),
                ("Técnico", NombreDe(nombres, filtro.IdTecnico) ?? "todos"))),
            incidencias,
            [
                new ColumnaReporte<Incidencia>("N.º", i => i.Numero.ToString(), 0.5f, ALaDerecha: true),
                new ColumnaReporte<Incidencia>("Fecha", i => i.Fecha.ToString("dd/MM/yy HH:mm"), 0.9f),
                new ColumnaReporte<Incidencia>("Equipo",
                    i => equipos.TryGetValue(i.IdEquipo, out var c) ? c : "—", 1.1f),
                new ColumnaReporte<Incidencia>("Título", i => i.Titulo, 2.4f),
                new ColumnaReporte<Incidencia>("Prioridad", i => NombreDe(prioridades, i.IdPrioridad) ?? "—", 0.8f),
                new ColumnaReporte<Incidencia>("Estado", i => NombreDe(estados, i.IdEstado) ?? "—", 1.2f),
                new ColumnaReporte<Incidencia>("Técnico", i => NombreDe(nombres, i.IdTecnico) ?? "sin asignar", 1.2f),
                new ColumnaReporte<Incidencia>("Resolución", Demora, 0.9f, ALaDerecha: true)
            ],
            [
                ("incidencias", incidencias.Count.ToString()),
                ("resueltas", resueltas.Count.ToString()),
                ("tiempo medio de resolución", TiempoMedio(resueltas))
            ]);

        return ($"incidencias-{DateTime.Today:yyyyMMdd}.pdf", bytes, incidencias.Count);
    }

    private (string, byte[], int) Mantenimientos(FiltroReporteDto filtro, DateTime desde, DateTime hasta)
    {
        var tipos = dao.Catalogos.TiposDeMantenimiento().ToDictionary(t => t.Id, t => t.Nombre);
        var equipos = dao.Equipos.GetAll().ToDictionary(e => e.Id, e => e.Codigo);
        var nombres = MapaDeUsuarios();

        var mantenimientos = dao.Mantenimientos.GetAll()
            .Where(m => m.Fecha >= desde && m.Fecha < hasta)
            .Where(m => filtro.IdTecnico is null || m.IdTecnico == filtro.IdTecnico)
            .OrderByDescending(m => m.Fecha)
            .ToList();

        var costo = mantenimientos.Sum(m => m.Costo ?? 0m);

        var bytes = reportes.Listado(
            Encabezado("Mantenimientos", Filtros(
                ("Período", $"{desde:dd/MM/yyyy} a {hasta.AddDays(-1):dd/MM/yyyy}"),
                ("Técnico", NombreDe(nombres, filtro.IdTecnico) ?? "todos"))),
            mantenimientos,
            [
                new ColumnaReporte<Mantenimiento>("Fecha", m => m.Fecha.ToString("dd/MM/yy"), 0.8f),
                new ColumnaReporte<Mantenimiento>("Equipo",
                    m => equipos.TryGetValue(m.IdEquipo, out var c) ? c : "—", 1.1f),
                new ColumnaReporte<Mantenimiento>("Tipo", m => NombreDe(tipos, m.IdTipo) ?? "—", 1.1f),
                new ColumnaReporte<Mantenimiento>("Descripción", m => m.Descripcion, 2.6f),
                new ColumnaReporte<Mantenimiento>("Técnico", m => NombreDe(nombres, m.IdTecnico) ?? "—", 1.2f),
                new ColumnaReporte<Mantenimiento>("Costo",
                    m => m.Costo is null ? "—" : m.Costo.Value.ToString("N0"), 0.8f, ALaDerecha: true)
            ],
            [
                ("mantenimientos", mantenimientos.Count.ToString()),
                ("equipos alcanzados", mantenimientos.Select(m => m.IdEquipo).Distinct().Count().ToString()),
                ("costo total", costo == 0 ? "sin registrar" : costo.ToString("C0"))
            ]);

        return ($"mantenimientos-{DateTime.Today:yyyyMMdd}.pdf", bytes, mantenimientos.Count);
    }

    // ────────────────────────────────────────────────────────────── privados

    private EncabezadoReporte Encabezado(string titulo,
                                         IReadOnlyList<(string, string)> filtros) =>
        new(titulo,
            dao.Organizaciones.GetById(contexto.IdOrganizacion)?.RazonSocial ?? "—",
            contexto.Username,
            Reloj.Ahora,
            filtros);

    /// <summary>Los filtros que se aplicaron de verdad; los vacíos no se listan.</summary>
    private static IReadOnlyList<(string, string)> Filtros(params (string Campo, string Valor)[] todos) =>
        todos.Where(f => !string.IsNullOrWhiteSpace(f.Valor)).ToList();

    private static string Demora(Incidencia i)
    {
        if (i.FechaResolucion is null) return "abierta";

        var horas = (i.FechaResolucion.Value - i.Fecha).TotalHours;
        return horas < 48 ? $"{horas:0.#} h" : $"{horas / 24:0.#} d";
    }

    private static string TiempoMedio(IReadOnlyList<Incidencia> resueltas)
    {
        if (resueltas.Count == 0) return "sin datos";

        var horas = resueltas.Average(i => (i.FechaResolucion!.Value - i.Fecha).TotalHours);
        return horas < 48 ? $"{horas:0.#} h" : $"{horas / 24:0.#} días";
    }

    private static string? NombreDe<T>(IDictionary<Guid, T> mapa, Guid? id) =>
        id is { } g && mapa.TryGetValue(g, out var v) ? v?.ToString() : null;

    private IDictionary<Guid, string> MapaDeUsuarios() =>
        seguridad.Usuarios.PorOrganizacion(contexto.IdOrganizacion)
            .ToDictionary(u => u.Id, u => u.NombreCompleto);

    private void Exigir(string patente)
    {
        if (!contexto.Tiene(patente)) throw new PermisoDenegadoException(patente);
    }
}
