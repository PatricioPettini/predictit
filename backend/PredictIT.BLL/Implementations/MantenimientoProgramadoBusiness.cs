using PredictIT.BLL.Contracts;
using PredictIT.BLL.DTO;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Factory;
using PredictIT.Domain.Negocio;
using PredictIT.Domain.Seguridad;
using PredictIT.Service.Seguridad;

namespace PredictIT.BLL.Implementations;

/// <summary>
/// El plan de mantenimiento preventivo y su agenda (RF-16, CU-019, CU-020).
///
/// Cierra un bucle que el sistema abría y no podía terminar: el motor
/// predictivo recomendaba «Programar el mantenimiento preventivo» y no había
/// con qué programarlo. Hasta acá <c>Mantenimiento</c> sólo registraba lo que
/// ya había pasado.
///
/// La decisión de fondo es que el plan define la expectativa —cada cuántos días
/// le toca a un equipo o a un tipo— y de ahí salen fechas concretas. Con eso, la
/// regla «Mantenimiento vencido» deja de medir contra un umbral suelto escrito
/// en la regla y pasa a medir contra lo que la organización se comprometió a
/// hacer, que es lo que «vencido» quiere decir.
/// </summary>
public class MantenimientoProgramadoBusiness(
    IFactoryDaoSeguridad seguridad,
    IContextoSesion contexto,
    IFactoryDao dao,
    IBitacoraService bitacora)
    : BaseAdministracion(seguridad, contexto), IMantenimientoProgramadoBusiness
{
    /// <summary>Hasta cuántos días hacia adelante se considera «próximo».</summary>
    private const int VentanaProximos = 7;

    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Today);

    // ------------------------------------------------------------- planes

    public IReadOnlyList<PlanMantenimientoDto> Planes()
    {
        Exigir(Patentes.MantenimientoVer);

        return dao.MantenimientosProgramados.Planes()
                  .Select(ADto)
                  .ToList();
    }

    public Guid GuardarPlan(Guid? id, PlanMantenimientoEntradaDto entrada)
    {
        Exigir(Patentes.MantenimientoPlanificar);

        // Uno u otro, nunca los dos ni ninguno. La base también lo garantiza;
        // acá se valida para poder explicar por qué en lugar de devolver un
        // error del motor.
        var porEquipo = entrada.IdEquipo is not null;
        var porTipo = entrada.IdTipoEquipo is not null;

        if (porEquipo == porTipo)
        {
            throw BusinessException.De("alcance",
                "El plan se aplica a un equipo o a un tipo de equipo, no a los dos ni a ninguno.");
        }

        if (entrada.CadaDias is < 7 or > 3650)
        {
            throw BusinessException.De("cadaDias",
                "La frecuencia va de 7 días a 10 años. Menos de una semana no es un "
                + "preventivo, y más de diez años no es un plan.");
        }

        var tipos = dao.Catalogos.TiposDeMantenimiento();
        var tipo = tipos.FirstOrDefault(t => t.Id == entrada.IdTipoMantenimiento)
                   ?? throw BusinessException.De("idTipoMantenimiento",
                       "El tipo de mantenimiento no existe.");

        // Planificar un correctivo no tiene sentido: el correctivo es la
        // respuesta a una falla que todavía no pasó.
        if (!tipo.EsPreventivo)
        {
            throw BusinessException.De("idTipoMantenimiento",
                $"«{tipo.Nombre}» no es preventivo. Un plan agenda lo que se hace antes "
                + "de la falla; lo correctivo nace de una incidencia.");
        }

        if (entrada.IdEquipo is { } idEquipo && dao.Equipos.GetById(idEquipo) is null)
        {
            throw BusinessException.De("idEquipo", "El equipo no existe o no es de tu organización.");
        }

        if (entrada.IdTipoEquipo is { } idTipoEquipo
            && dao.Catalogos.TiposDeEquipo().All(t => t.Id != idTipoEquipo))
        {
            throw BusinessException.De("idTipoEquipo", "El tipo de equipo no existe.");
        }

        var plan = id is { } existente
            ? dao.MantenimientosProgramados.PlanPorId(existente)
              ?? throw new BusinessException("El plan no existe o no es de tu organización.")
            : new PlanMantenimiento();

        plan.IdEquipo = entrada.IdEquipo;
        plan.IdTipoEquipo = entrada.IdTipoEquipo;
        plan.IdTipoMantenimiento = tipo.Id;
        plan.CadaDias = entrada.CadaDias;
        plan.Activo = entrada.Activo;
        plan.Descripcion = entrada.Descripcion?.Trim();

        var idGuardado = dao.MantenimientosProgramados.GuardarPlan(plan);

        bitacora.Registrar(TipoEvento.Codigos.Modificacion,
            $"Plan de mantenimiento {(id is null ? "creado" : "modificado")}: "
            + $"{tipo.Nombre} cada {plan.CadaDias} días.",
            Contexto.IdUsuario, Contexto.IdOrganizacion, "PlanMantenimiento", idGuardado);

        return idGuardado;
    }

    // --------------------------------------------------------- la agenda

    public IReadOnlyList<MantenimientoProgramadoDto> Programados(string? estado)
    {
        Exigir(Patentes.MantenimientoVer);

        if (estado is not null && !EstadoProgramado.Todos.Contains(estado))
            throw BusinessException.De("estado", "Ese estado no existe.");

        return dao.MantenimientosProgramados.Buscar(estado, null).Select(ADto).ToList();
    }

    public PanelProgramadoDto Panel()
    {
        Exigir(Patentes.MantenimientoVer);

        var pendientes = dao.MantenimientosProgramados.PendientesDeLaOrganizacion();
        var hoy = Hoy;
        var limite = hoy.AddDays(VentanaProximos);

        return new PanelProgramadoDto(
            pendientes.Count(p => p.FechaProgramada < hoy),
            pendientes.Count(p => p.FechaProgramada >= hoy && p.FechaProgramada <= limite),
            pendientes.Count,
            dao.MantenimientosProgramados.Planes(soloActivos: true).Count,
            pendientes.OrderBy(p => p.FechaProgramada).Take(10).Select(ADto).ToList());
    }

    public Guid Programar(ProgramarEntradaDto entrada)
    {
        Exigir(Patentes.MantenimientoPlanificar);

        var equipo = dao.Equipos.GetById(entrada.IdEquipo)
                     ?? throw BusinessException.De("idEquipo",
                         "El equipo no existe o no es de tu organización.");

        var tipo = dao.Catalogos.TiposDeMantenimiento()
                      .FirstOrDefault(t => t.Id == entrada.IdTipoMantenimiento)
                   ?? throw BusinessException.De("idTipoMantenimiento",
                       "El tipo de mantenimiento no existe.");

        // Se puede programar para hoy, no para ayer: agendar en el pasado deja
        // el trabajo vencido en el mismo momento de crearlo.
        if (entrada.FechaProgramada < Hoy)
        {
            throw BusinessException.De("fechaProgramada",
                "No se puede programar para una fecha que ya pasó.");
        }

        if (dao.MantenimientosProgramados.YaProgramado(equipo.Id, tipo.Id, entrada.FechaProgramada))
        {
            throw BusinessException.De("fechaProgramada",
                $"Ya hay un {tipo.Nombre} programado para {equipo.Codigo} ese día.");
        }

        var id = dao.MantenimientosProgramados.Programar(new MantenimientoProgramado
        {
            IdEquipo = equipo.Id,
            IdTipoMantenimiento = tipo.Id,
            FechaProgramada = entrada.FechaProgramada,
            Estado = EstadoProgramado.Programado,
            Motivo = entrada.Motivo?.Trim(),
        });

        bitacora.Registrar(TipoEvento.Codigos.MantenimientoProgramado,
            $"{tipo.Nombre} de {equipo.Codigo} programado para "
            + $"{entrada.FechaProgramada:dd/MM/yyyy}.",
            Contexto.IdUsuario, Contexto.IdOrganizacion, "MantenimientoProgramado", id);

        return id;
    }

    public void Reprogramar(Guid id, ReprogramarEntradaDto entrada)
    {
        Exigir(Patentes.MantenimientoPlanificar);

        var programado = Pendiente(id);

        if (entrada.FechaProgramada < Hoy)
        {
            throw BusinessException.De("fechaProgramada",
                "No se puede reprogramar para una fecha que ya pasó.");
        }

        // El motivo es obligatorio y no es burocracia: un preventivo que se
        // corre sin explicación es indistinguible de uno que nadie miró, y la
        // diferencia importa cuando el equipo después falla.
        var motivo = (entrada.Motivo ?? string.Empty).Trim();
        if (motivo.Length < 5)
        {
            throw BusinessException.De("motivo",
                "Decí por qué se reprograma: queda en el historial del equipo.");
        }

        dao.MantenimientosProgramados.Reprogramar(id, entrada.FechaProgramada, motivo);

        bitacora.Registrar(TipoEvento.Codigos.MantenimientoReprogramado,
            $"{programado.NombreTipoMantenimiento} de {programado.CodigoEquipo}: "
            + $"de {programado.FechaProgramada:dd/MM/yyyy} a "
            + $"{entrada.FechaProgramada:dd/MM/yyyy}. Motivo: {motivo}",
            Contexto.IdUsuario, Contexto.IdOrganizacion, "MantenimientoProgramado", id);
    }

    public void Anular(Guid id, string motivo)
    {
        Exigir(Patentes.MantenimientoPlanificar);

        var programado = Pendiente(id);

        var texto = (motivo ?? string.Empty).Trim();
        if (texto.Length < 5)
            throw BusinessException.De("motivo", "Decí por qué se anula.");

        dao.MantenimientosProgramados.Anular(id, texto);

        bitacora.Registrar(TipoEvento.Codigos.MantenimientoAnulado,
            $"{programado.NombreTipoMantenimiento} de {programado.CodigoEquipo} del "
            + $"{programado.FechaProgramada:dd/MM/yyyy} anulado. Motivo: {texto}",
            Contexto.IdUsuario, Contexto.IdOrganizacion, "MantenimientoProgramado", id);
    }

    /// <summary>
    /// Genera las fechas que faltan a partir de los planes activos.
    ///
    /// Cada equipo alcanzado por un plan lleva como mucho una fecha pendiente:
    /// generar la serie completa hasta el fin de los tiempos llenaría la agenda
    /// de trabajo que todavía no se decidió. La próxima sale del último
    /// preventivo de ese tipo, o del alta del equipo si nunca tuvo uno.
    /// </summary>
    public GeneracionDto GenerarDesdePlanes()
    {
        Exigir(Patentes.MantenimientoPlanificar);

        var planes = dao.MantenimientosProgramados.Planes(soloActivos: true);
        var generados = 0;
        var yaEstaban = 0;

        foreach (var plan in planes)
        {
            foreach (var idEquipo in dao.MantenimientosProgramados.EquiposAlcanzados(plan.Id))
            {
                var pendientes = dao.MantenimientosProgramados.PendientesDeEquipo(idEquipo);
                if (pendientes.Any(p => p.IdTipoMantenimiento == plan.IdTipoMantenimiento))
                {
                    yaEstaban++;
                    continue;
                }

                var ultimo = dao.Mantenimientos.DeEquipo(idEquipo)
                    .Where(m => m.IdTipo == plan.IdTipoMantenimiento)
                    .OrderByDescending(m => m.Fecha)
                    .FirstOrDefault();

                var equipo = dao.Equipos.GetById(idEquipo);
                var referencia = ultimo is not null
                    ? DateOnly.FromDateTime(ultimo.Fecha)
                    : DateOnly.FromDateTime(equipo?.FechaAlta ?? DateTime.Today);

                // Si la fecha que sale del plan ya pasó, se agenda para hoy: el
                // trabajo está atrasado y ponerlo en el pasado sólo serviría
                // para que naciera vencido.
                var proxima = referencia.AddDays(plan.CadaDias);
                if (proxima < Hoy) proxima = Hoy;

                dao.MantenimientosProgramados.Programar(new MantenimientoProgramado
                {
                    IdPlan = plan.Id,
                    IdEquipo = idEquipo,
                    IdTipoMantenimiento = plan.IdTipoMantenimiento,
                    FechaProgramada = proxima,
                    Estado = EstadoProgramado.Programado,
                });

                generados++;
            }
        }

        bitacora.Registrar(TipoEvento.Codigos.MantenimientoProgramado,
            $"Generación desde {planes.Count} plan(es) activos: {generados} fecha(s) nuevas, "
            + $"{yaEstaban} equipo(s) ya tenían una pendiente.");

        return new GeneracionDto(planes.Count, generados, yaEstaban);
    }

    // ------------------------------------------------------------ interno

    private MantenimientoProgramado Pendiente(Guid id)
    {
        var programado = dao.MantenimientosProgramados.PorId(id)
                         ?? throw new BusinessException(
                             "El mantenimiento programado no existe o no es de tu organización.");

        if (programado.Estado != EstadoProgramado.Programado)
        {
            throw new BusinessException(
                $"Ya está {programado.Estado.ToLowerInvariant()}: sólo se puede cambiar lo que "
                + "sigue pendiente.");
        }

        return programado;
    }

    private MantenimientoProgramadoDto ADto(MantenimientoProgramado p)
    {
        var hoy = Hoy;
        return new MantenimientoProgramadoDto(
            p.Id, p.IdEquipo, p.CodigoEquipo ?? string.Empty, p.UbicacionEquipo,
            p.IdTipoMantenimiento, p.NombreTipoMantenimiento ?? string.Empty,
            p.FechaProgramada, p.Estado, p.EstaVencido(hoy), p.DiasDeAtraso(hoy),
            p.IdPlan, p.IdMantenimiento, p.Motivo);
    }

    private PlanMantenimientoDto ADto(PlanMantenimiento p) => new(
        p.Id, p.IdEquipo, p.IdTipoEquipo, p.Alcance,
        p.IdTipoMantenimiento, p.NombreTipoMantenimiento ?? string.Empty,
        p.CadaDias, p.Activo, p.Descripcion,
        dao.MantenimientosProgramados.EquiposAlcanzados(p.Id).Count);
}
