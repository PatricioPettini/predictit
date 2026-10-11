using PredictIT.BLL.Contracts;
using PredictIT.BLL.DTO;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Factory;
using PredictIT.Domain.Negocio;
using PredictIT.Domain.Seguridad;
using PredictIT.Service.IA;
using PredictIT.Service.Respaldos;
using PredictIT.Service.Seguridad;

namespace PredictIT.BLL.Implementations;

/// <summary>
/// Mantenimientos y el historial del parque (RF-03, CU-008, CU-009).
///
/// Registrar un mantenimiento dispara una reevaluación del equipo: la
/// intervención cambia el historial sobre el que el motor calcula el riesgo, y
/// si el puntaje se recalculara sólo por barrido el técnico vería un número
/// viejo justo después de haber trabajado.
/// </summary>
public class MantenimientoBusiness(
    IFactoryDaoSeguridad seguridad,
    IContextoSesion contexto,
    IFactoryDao dao,
    IBitacoraService bitacora,
    Func<IPrediccionBusiness> prediccion)
    : BaseAdministracion(seguridad, contexto), IMantenimientoBusiness
{
    // ------------------------------------------------------ mantenimientos

    public IReadOnlyList<MantenimientoListaDto> Mantenimientos(Guid? idEquipo)
    {
        Exigir(Patentes.MantenimientoVer);

        var lista = idEquipo is { } id
            ? dao.Mantenimientos.DeEquipo(id)
            : dao.Mantenimientos.GetAll();

        var nombres = MapaDeUsuarios();
        var equipos = dao.Equipos.GetAll().ToDictionary(e => e.Id, e => e.Codigo);

        // Los números de incidencia se resuelven de una sola vez: pedirlos de a
        // uno por mantenimiento seria el problema N+1.
        var numeros = dao.Incidencias.GetAll().ToDictionary(i => i.Id, i => i.Numero);

        return lista.Select(m => new MantenimientoListaDto(
            m.Id,
            m.IdEquipo,
            equipos.TryGetValue(m.IdEquipo, out var codigo) ? codigo : string.Empty,
            m.Tipo?.Nombre ?? string.Empty,
            m.Tipo?.EsPreventivo ?? false,
            m.Fecha,
            NombreDe(nombres, m.IdTecnico),
            m.Descripcion,
            m.Costo,
            m.IdIncidencia is { } ii && numeros.TryGetValue(ii, out var n) ? n : null)).ToList();
    }

    public Guid RegistrarMantenimiento(MantenimientoEntradaDto entrada)
    {
        Exigir(Patentes.MantenimientoRegistrar);

        var descripcion = (entrada.Descripcion ?? string.Empty).Trim();
        if (descripcion.Length < 10)
        {
            throw BusinessException.De("descripcion",
                "Describí qué se hizo, con al menos 10 caracteres.");
        }

        var equipo = dao.Equipos.GetById(entrada.IdEquipo)
                     ?? throw BusinessException.De("idEquipo",
                         "El equipo no existe o no es de tu organización.");

        var tipos = dao.Catalogos.TiposDeMantenimiento();
        var tipo = tipos.FirstOrDefault(t => t.Id == entrada.IdTipo)
                   ?? throw BusinessException.De("idTipo", "El tipo de mantenimiento no existe.");

        // La fecha no puede ser futura: es el registro de algo que ya se hizo.
        var fecha = entrada.Fecha ?? DateTime.Now;
        if (fecha.Date > DateTime.Today)
        {
            throw BusinessException.De("fecha",
                "La fecha no puede ser futura: el mantenimiento se registra una vez hecho.");
        }

        if (entrada.Costo is < 0)
            throw BusinessException.De("costo", "El costo no puede ser negativo.");

        // Si se vincula a una incidencia, tiene que ser del mismo equipo: un
        // correctivo sobre otro equipo no es el correctivo de esa falla.
        if (entrada.IdIncidencia is { } idIncidencia)
        {
            var incidencia = dao.Incidencias.GetById(idIncidencia)
                             ?? throw BusinessException.De("idIncidencia", "La incidencia no existe.");

            if (incidencia.IdEquipo != equipo.Id)
            {
                throw BusinessException.De("idIncidencia",
                    "La incidencia corresponde a otro equipo.");
            }
        }

        var mantenimiento = new Mantenimiento
        {
            IdEquipo = equipo.Id,
            IdTipo = tipo.Id,
            IdTecnico = Contexto.IdUsuario,
            IdIncidencia = entrada.IdIncidencia,
            Fecha = fecha,
            Descripcion = descripcion,
            Resultado = Recortado(entrada.Resultado),
            Repuestos = Recortado(entrada.Repuestos),
            Observaciones = Recortado(entrada.Observaciones),
            Costo = entrada.Costo
        };

        var id = dao.Mantenimientos.Insert(mantenimiento);

        CerrarProgramado(entrada.IdProgramado, equipo, tipo, id);

        bitacora.Registrar(TipoEvento.Codigos.MantenimientoAlta,
            $"{tipo.Nombre} sobre {equipo.Codigo} del {fecha:dd/MM/yyyy}: {descripcion}");

        // Un mantenimiento baja el riesgo tanto como una incidencia lo sube: si
        // había una alerta por mantenimiento vencido, el registro la resuelve.
        prediccion().ReevaluarPorEvento(equipo.Id);

        return id;
    }

    /// <summary>
    /// Cierra el mantenimiento programado que este registro ejecuta.
    ///
    /// Con identificador explícito se cierra ése y se valida que sea del mismo
    /// equipo. Sin identificador, y sólo si el tipo es preventivo, se cierra el
    /// pendiente más viejo de ese equipo y ese tipo: el trabajo se hizo, y dejar
    /// la fecha abierta la mostraría vencida al día siguiente de haberla
    /// cumplido. Se elige el más viejo y no el más próximo porque es el que
    /// estaba atrasado.
    /// </summary>
    private void CerrarProgramado(Guid? idProgramado, Equipo equipo, TipoMantenimiento tipo, Guid idMantenimiento)
    {
        if (idProgramado is { } explicito)
        {
            var programado = dao.MantenimientosProgramados.PorId(explicito)
                             ?? throw BusinessException.De("idProgramado",
                                 "El mantenimiento programado no existe o no es de tu organización.");

            if (programado.IdEquipo != equipo.Id)
            {
                throw BusinessException.De("idProgramado",
                    "El mantenimiento programado corresponde a otro equipo.");
            }

            if (programado.Estado != EstadoProgramado.Programado)
            {
                throw BusinessException.De("idProgramado",
                    $"Ese trabajo ya está {programado.Estado.ToLowerInvariant()}.");
            }

            dao.MantenimientosProgramados.MarcarEjecutado(explicito, idMantenimiento);
            return;
        }

        if (!tipo.EsPreventivo) return;

        var pendiente = dao.MantenimientosProgramados.PendientesDeEquipo(equipo.Id)
            .Where(p => p.IdTipoMantenimiento == tipo.Id)
            .OrderBy(p => p.FechaProgramada)
            .FirstOrDefault();

        if (pendiente is not null)
            dao.MantenimientosProgramados.MarcarEjecutado(pendiente.Id, idMantenimiento);
    }

    public CatalogosMantenimientoDto CatalogosMantenimiento()
    {
        Exigir(Patentes.MantenimientoVer);

        return new CatalogosMantenimientoDto(
            dao.Catalogos.TiposDeMantenimiento()
                .Select(t => new TipoMantenimientoDto(t.Id, t.Nombre, t.EsPreventivo)).ToList(),
            dao.Tecnicos.CandidatosPara(idEquipo: null)
                .Select(t => new TecnicoDto(
                    t.IdUsuario, t.NombreCompleto,
                    t.Especialidades.Select(e => e.Especialidad?.Nombre ?? string.Empty).ToList(),
                    t.IncidenciasAbiertas)).ToList());
    }

    // ------------------------------------------------------------- bitácora

    public IReadOnlyList<HechoHistorialDto> Historial(DateTime? desde, DateTime? hasta)
    {
        Exigir(Patentes.HistorialVerOrganizacion);

        var hasta2 = (hasta ?? DateTime.Today).Date.AddDays(1);
        var desde2 = (desde ?? hasta2.AddDays(-90)).Date;

        if (desde2 > hasta2) throw BusinessException.De("desde", "La fecha «desde» es posterior a «hasta».");

        var equipos = dao.Equipos.GetAll().ToDictionary(e => e.Id, e => e.Codigo);
        var estados = dao.Catalogos.EstadosDeIncidencia().ToDictionary(e => e.Id, e => e.Nombre);
        var tipos = dao.Catalogos.TiposDeMantenimiento().ToDictionary(t => t.Id, t => t.Nombre);
        var nombres = MapaDeUsuarios();

        // Las dos fuentes se traen enteras y se cruzan en memoria, en una sola
        // pasada por cada una. Un join por equipo serían tantas consultas como
        // equipos tenga el parque.
        var deIncidencias = dao.Incidencias.GetAll()
            .Where(i => i.Fecha >= desde2 && i.Fecha < hasta2)
            .Select(i => new HechoHistorialDto(
                i.Fecha,
                "INCIDENCIA",
                equipos.TryGetValue(i.IdEquipo, out var c) ? c : "(equipo dado de baja)",
                $"#{i.Numero} · {i.Titulo}",
                i.Solucion ?? i.Diagnostico,
                estados.TryGetValue(i.IdEstado, out var e) ? e : null,
                NombreDe(nombres, i.IdTecnico)));

        var deMantenimientos = dao.Mantenimientos.GetAll()
            .Where(m => m.Fecha >= desde2 && m.Fecha < hasta2)
            .Select(m => new HechoHistorialDto(
                m.Fecha,
                "MANTENIMIENTO",
                equipos.TryGetValue(m.IdEquipo, out var c) ? c : "(equipo dado de baja)",
                tipos.TryGetValue(m.IdTipo, out var t) ? t : "Mantenimiento",
                m.Descripcion,
                m.Resultado,
                NombreDe(nombres, m.IdTecnico)));

        return deIncidencias.Concat(deMantenimientos)
            .OrderByDescending(h => h.Fecha)
            .ToList();
    }
}
