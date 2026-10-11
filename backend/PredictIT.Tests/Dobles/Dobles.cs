using PredictIT.BLL.Contracts;
using PredictIT.BLL.DTO;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Factory;
using PredictIT.Domain.Negocio;
using PredictIT.Domain.Seguridad;
using PredictIT.Service.Seguridad;

namespace PredictIT.Tests.Dobles;

/*
    Dobles escritos a mano y no generados con una librería de simulación.

    Dos motivos. El primero es que un miembro que la prueba no configuró tira
    NotSupportedException con el nombre adentro, así que si el código toca un
    DAO que no debería, la prueba lo dice; una simulación generada devuelve el
    valor por omisión y la prueba pasa igual, tapando el error. El segundo es
    que no agrega una dependencia más al proyecto para algo que son cien
    líneas.
*/

/// <summary>Quién opera y con qué permisos, decidido por la prueba.</summary>
public class ContextoFalso : IContextoSesion
{
    public Guid IdUsuario { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = "prueba";
    public Guid IdOrganizacion { get; set; } = Guid.NewGuid();
    public Guid? IdIdioma { get; set; }

    public HashSet<string> Patentes { get; } = new(StringComparer.OrdinalIgnoreCase);

    public ContextoFalso Con(params string[] patentes)
    {
        foreach (var p in patentes) Patentes.Add(p);
        return this;
    }

    public bool Tiene(string dataKey) => Patentes.Contains(dataKey);
}

/// <summary>
/// Incidencias en memoria.
///
/// `Atender` y `Asignar` escriben sobre la misma instancia que devuelve
/// `GetById`, igual que hace el DAO real contra la base: así la prueba puede
/// mirar el objeto después de la operación y ver el estado que quedó.
/// </summary>
public class IncidenciaDaoFalso : IIncidenciaDao
{
    public Dictionary<Guid, Incidencia> Guardadas { get; } = [];
    public List<(Guid Id, Guid Estado, string? Diagnostico, string? Solucion, DateTime? Resolucion)> Atendidas { get; } = [];
    public List<(Guid Id, Guid? Tecnico, TipoAsignacion Tipo, bool Revision, Guid? Estado)> Asignaciones { get; } = [];

    public Incidencia Agregar(Incidencia i)
    {
        Guardadas[i.Id] = i;
        return i;
    }

    public Incidencia? GetById(Guid id) => Guardadas.GetValueOrDefault(id);

    public void Atender(Guid idIncidencia, Guid idEstado, string? diagnostico,
                        string? solucion, DateTime? fechaResolucion)
    {
        Atendidas.Add((idIncidencia, idEstado, diagnostico, solucion, fechaResolucion));

        if (!Guardadas.TryGetValue(idIncidencia, out var i)) return;
        i.IdEstado = idEstado;
        i.Diagnostico = diagnostico ?? i.Diagnostico;
        i.Solucion = solucion ?? i.Solucion;
        i.FechaResolucion = fechaResolucion;
    }

    public void Asignar(Guid idIncidencia, Guid? idTecnico, TipoAsignacion tipo,
                        bool pendienteRevision, Guid? idEstado = null)
    {
        Asignaciones.Add((idIncidencia, idTecnico, tipo, pendienteRevision, idEstado));

        if (!Guardadas.TryGetValue(idIncidencia, out var i)) return;
        i.IdTecnico = idTecnico;
        if (idEstado is not null) i.IdEstado = idEstado.Value;
    }

    /// <summary>El último filtro con el que lo llamaron, para poder afirmar
    /// qué le impuso la capa de negocio antes de bajar.</summary>
    public FiltroIncidencias? UltimoFiltro { get; private set; }

    public PaginaDe<Incidencia> Buscar(FiltroIncidencias filtro)
    {
        UltimoFiltro = filtro;
        return new PaginaDe<Incidencia>
        {
            Items = Guardadas.Values.ToList(), Total = Guardadas.Count,
            Pagina = 1, PorPagina = 20
        };
    }
    public int ProximoNumero() => throw Sin(nameof(ProximoNumero));
    public IList<Incidencia> AbiertasDeEquipo(Guid idEquipo) => throw Sin(nameof(AbiertasDeEquipo));
    public IList<Incidencia> DeEquipoDesde(Guid idEquipo, DateTime desde) => throw Sin(nameof(DeEquipoDesde));
    public void GuardarClasificacion(ClasificacionIncidencia c) => throw Sin(nameof(GuardarClasificacion));
    public void GuardarRecomendacion(RecomendacionAsignacion r) => throw Sin(nameof(GuardarRecomendacion));
    public ClasificacionIncidencia? ClasificacionDe(Guid id) => throw Sin(nameof(ClasificacionDe));
    public RecomendacionAsignacion? RecomendacionDe(Guid id) => throw Sin(nameof(RecomendacionDe));
    public Guid Insert(Incidencia e) => throw Sin(nameof(Insert));
    public void Update(Incidencia e) => throw Sin(nameof(Update));
    public void Delete(Guid id) => throw Sin(nameof(Delete));
    public IList<Incidencia> GetAll() => throw Sin(nameof(GetAll));

    private static NotSupportedException Sin(string miembro) =>
        new($"IncidenciaDaoFalso.{miembro} no está preparado: la prueba no debería llegar acá.");
}

/// <summary>
/// Los catálogos que necesita la máquina de estados: los siete estados con los
/// nombres y las marcas de final exactos que tiene la base. Están copiados a
/// mano a propósito —<c>IncidenciaBusiness</c> compara por nombre—, así que si
/// alguno cambia en la base y no acá, estas pruebas siguen verdes contra un
/// catálogo que ya no existe. Lo cubre
/// <c>EstadosDelCatalogoTests</c>, que compara esta lista contra la real.
/// </summary>
public class CatalogoDaoFalso : ICatalogoDao
{
    public static readonly Guid IdPendiente = Guid.Parse("11111111-0000-0000-0000-000000000001");
    public static readonly Guid IdAsignada = Guid.Parse("11111111-0000-0000-0000-000000000002");
    public static readonly Guid IdEnCurso = Guid.Parse("11111111-0000-0000-0000-000000000003");
    public static readonly Guid IdEsperaRepuesto = Guid.Parse("11111111-0000-0000-0000-000000000004");
    public static readonly Guid IdResuelta = Guid.Parse("11111111-0000-0000-0000-000000000005");
    public static readonly Guid IdCerrada = Guid.Parse("11111111-0000-0000-0000-000000000006");
    public static readonly Guid IdAnulada = Guid.Parse("11111111-0000-0000-0000-000000000007");

    public IList<EstadoIncidencia> EstadosDeIncidencia() =>
    [
        new() { Id = IdPendiente, Nombre = "Pendiente de asignación", EsFinal = false },
        new() { Id = IdAsignada, Nombre = "Asignada", EsFinal = false },
        new() { Id = IdEnCurso, Nombre = "En curso", EsFinal = false },
        new() { Id = IdEsperaRepuesto, Nombre = "En espera de repuesto", EsFinal = false },
        new() { Id = IdResuelta, Nombre = "Resuelta", EsFinal = true },
        new() { Id = IdCerrada, Nombre = "Cerrada", EsFinal = true },
        new() { Id = IdAnulada, Nombre = "Anulada", EsFinal = true }
    ];

    public static readonly Guid IdOperativo = Guid.Parse("22222222-0000-0000-0000-000000000001");
    public static readonly Guid IdEnReparacion = Guid.Parse("22222222-0000-0000-0000-000000000002");
    public static readonly Guid IdDadoDeBaja = Guid.Parse("22222222-0000-0000-0000-000000000003");
    public static readonly Guid IdTipoNotebook = Guid.Parse("33333333-0000-0000-0000-000000000001");
    public static readonly Guid IdUbicacionComercial = Guid.Parse("44444444-0000-0000-0000-000000000001");

    public IList<TipoEquipo> TiposDeEquipo() =>
    [
        new() { Id = IdTipoNotebook, Nombre = "Notebook" }
    ];

    public IList<EstadoEquipo> EstadosDeEquipo() =>
    [
        new() { Id = IdOperativo, Nombre = "Operativo", Operativo = true },
        new() { Id = IdEnReparacion, Nombre = "En reparación", Operativo = false },
        new() { Id = IdDadoDeBaja, Nombre = "Dado de baja", Operativo = false }
    ];

    public IList<Ubicacion> Ubicaciones() =>
    [
        new() { Id = IdUbicacionComercial, Nombre = "Comercial" }
    ];
    public IList<PrioridadIncidencia> Prioridades() => throw Sin(nameof(Prioridades));
    public IList<CategoriaIncidencia> Categorias() => throw Sin(nameof(Categorias));
    public IList<Especialidad> Especialidades() => throw Sin(nameof(Especialidades));
    public IList<TipoMantenimiento> TiposDeMantenimiento() => throw Sin(nameof(TiposDeMantenimiento));
    public IList<EstadoAlerta> EstadosDeAlerta() => throw Sin(nameof(EstadosDeAlerta));
    public EstadoIncidencia? EstadoIncidenciaInicial() =>
        EstadosDeIncidencia().First(e => e.Id == IdPendiente);
    public EstadoIncidencia? EstadoIncidenciaAsignada() =>
        EstadosDeIncidencia().First(e => e.Id == IdAsignada);
    public EstadoAlerta? EstadoAlertaInicial() => throw Sin(nameof(EstadoAlertaInicial));

    private static NotSupportedException Sin(string miembro) =>
        new($"CatalogoDaoFalso.{miembro} no está preparado: la prueba no debería llegar acá.");
}

/// <summary>
/// La fábrica de negocio. Sólo entrega los dos DAO que las pruebas configuran;
/// pedir cualquier otro es un error de la prueba y se avisa como tal.
/// </summary>
public class FabricaFalsa(IIncidenciaDao incidencias, ICatalogoDao catalogos,
                          IEquipoDao? equipos = null,
                          IFacturacionDao? facturacion = null,
                          IOrganizacionDao? organizaciones = null) : IFactoryDao
{
    public IIncidenciaDao Incidencias { get; } = incidencias;
    public ICatalogoDao Catalogos { get; } = catalogos;

    // Opcional: las pruebas que no tocan equipos siguen construyendo la fábrica
    // con dos argumentos, y pedir el DAO sin haberlo configurado sigue siendo
    // un error de la prueba.
    public IEquipoDao Equipos => equipos ?? throw Sin(nameof(Equipos));
    public IOrganizacionDao Organizaciones => organizaciones ?? throw Sin(nameof(Organizaciones));
    public IFacturacionDao Facturacion => facturacion ?? throw Sin(nameof(Facturacion));
    public IMantenimientoDao Mantenimientos => throw Sin(nameof(Mantenimientos));
    public IMantenimientoProgramadoDao MantenimientosProgramados =>
        throw Sin(nameof(MantenimientosProgramados));
    public IPrediccionDao Prediccion => throw Sin(nameof(Prediccion));
    public ITecnicoDao Tecnicos => throw Sin(nameof(Tecnicos));
    public IConfiguracionAsignacionDao ConfiguracionAsignacion =>
        throw Sin(nameof(ConfiguracionAsignacion));
    public IUnitOfWork IniciarUnidadDeTrabajo() => throw Sin(nameof(IniciarUnidadDeTrabajo));

    private static NotSupportedException Sin(string miembro) =>
        new($"FabricaFalsa.{miembro} no está preparado: la prueba no debería llegar acá.");
}

/// <summary>Anota lo que se le pide registrar, para poder afirmarlo.</summary>
public class BitacoraFalsa : IBitacoraService
{
    public List<(string Codigo, string Descripcion)> Eventos { get; } = [];

    public void Registrar(string codigoTipoEvento, string descripcion, Guid? idUsuario = null,
                          Guid? idOrganizacion = null, string? entidad = null, Guid? idEntidad = null,
                          string? ip = null, string? traza = null) =>
        Eventos.Add((codigoTipoEvento, descripcion));

    public void RegistrarAccesoExitoso(Usuario usuario, Guid idOrganizacion, string? ip) { }
    public void RegistrarIntentoFallido(string username, string? ip, string motivo, Guid? idUsuario = null) { }
    public void RegistrarAccesoDenegado(string username, string dataKey, Guid? idUsuario, Guid? idOrganizacion) { }
    public void RegistrarExcepcion(Exception ex, string? contexto, Guid? idUsuario, Guid? idOrganizacion) { }

    public IList<Bitacora> Consultar(DateTime desde, DateTime hasta, Guid? idOrganizacion,
                                                      string? codigo, Guid? idUsuario = null) => [];
    public IList<Bitacora> ConsultarErrores(DateTime desde, DateTime hasta,
                                                             Guid? idOrganizacion, bool incluirAdvertencias) => [];
}

/// <summary>
/// El motor predictivo, para poder afirmar que la atención lo despierta.
///
/// Resolver o anular una incidencia cambia el historial del equipo, y el
/// historial es de lo que se alimentan las reglas: si eso deja de llamarse, las
/// alertas se calculan sobre datos viejos y nadie se entera.
/// </summary>
public class PrediccionFalsa : IPrediccionBusiness
{
    public List<Guid> Reevaluados { get; } = [];

    public int ReevaluarPorEvento(Guid idEquipo)
    {
        Reevaluados.Add(idEquipo);
        return 0;
    }

    public RiesgoEquipoDto EvaluarEquipo(Guid idEquipo) => throw Sin(nameof(EvaluarEquipo));
    public ResultadoEvaluacionDto EvaluarParque() => throw Sin(nameof(EvaluarParque));
    public IReadOnlyList<AlertaDto> AlertasActivas() => throw Sin(nameof(AlertasActivas));
    public IReadOnlyList<CalibracionReglaDto> Calibracion() => throw Sin(nameof(Calibracion));
    public PanelPredictivoDto Panel() => throw Sin(nameof(Panel));
    public void AtenderAlerta(Guid idAlerta, bool descartar) => throw Sin(nameof(AtenderAlerta));
    public IReadOnlyList<ReglaDto> Reglas() => throw Sin(nameof(Reglas));
    public Guid GuardarRegla(ReglaDto entrada) => throw Sin(nameof(GuardarRegla));
    public EstadoIaDto EstadoDeLaIa() => throw Sin(nameof(EstadoDeLaIa));
    public DashboardDto Dashboard() => throw Sin(nameof(Dashboard));

    private static NotSupportedException Sin(string miembro) =>
        new($"PrediccionFalsa.{miembro} no está preparado: la prueba no debería llegar acá.");
}

/// <summary>La base de seguridad: sólo usuarios, y vacía salvo que la prueba cargue.</summary>
public class FabricaSeguridadFalsa(IUsuarioDao usuarios, IBitacoraDao? bitacora = null)
    : IFactoryDaoSeguridad
{
    public IUsuarioDao Usuarios { get; } = usuarios;

    public IPermisoDao Permisos => throw Sin(nameof(Permisos));
    public IBitacoraDao Bitacora => bitacora ?? throw Sin(nameof(Bitacora));
    public IRespaldoDao Respaldos => throw Sin(nameof(Respaldos));
    public IIdiomaDao Idiomas => throw Sin(nameof(Idiomas));

    private static NotSupportedException Sin(string miembro) =>
        new($"FabricaSeguridadFalsa.{miembro} no está preparado: la prueba no debería llegar acá.");
}

public class UsuarioDaoFalso : IUsuarioDao
{
    public List<Usuario> Usuarios { get; } = [];

    public IList<Usuario> PorOrganizacion(Guid idOrganizacion) => Usuarios;
    public Usuario? GetById(Guid id) => Usuarios.FirstOrDefault(u => u.Id == id);

    public Usuario? PorUsername(string username) => throw Sin(nameof(PorUsername));
    public void RegistrarAccesoExitoso(Guid idUsuario) => throw Sin(nameof(RegistrarAccesoExitoso));
    public void RegistrarAccesoFallido(Guid idUsuario) => throw Sin(nameof(RegistrarAccesoFallido));
    public void CambiarEstado(Guid idUsuario, bool activo) => throw Sin(nameof(CambiarEstado));
    public void Desbloquear(Guid idUsuario) => throw Sin(nameof(Desbloquear));
    public void AsignarRoles(Guid idUsuario, IEnumerable<Guid> idsRol) => throw Sin(nameof(AsignarRoles));
    public void ActualizarHash(Guid idUsuario, string hash) => throw Sin(nameof(ActualizarHash));

    private static NotSupportedException Sin(string miembro) =>
        new($"UsuarioDaoFalso.{miembro} no está preparado: la prueba no debería llegar acá.");
}

/// <summary>
/// Una bitácora que falla las primeras <c>FallasSeguidas</c> escrituras.
///
/// Sirve para el camino que no se puede provocar contra la base real sin
/// montar contención a propósito: el interbloqueo o la espera agotada que
/// SQL Server devuelve cuando muchas operaciones escriben a la vez.
/// </summary>
public class BitacoraDaoFalso : IBitacoraDao
{
    public int FallasSeguidas { get; set; }
    public int Intentos { get; private set; }
    public List<Bitacora> Escritas { get; } = [];

    /// <summary>La excepción con la que falla. Por defecto, una transitoria.</summary>
    public Func<Exception> Falla { get; set; } = () => new TimeoutException("La espera se agotó.");

    public void Registrar(Bitacora entrada)
    {
        Intentos++;
        if (Intentos <= FallasSeguidas) throw Falla();
        Escritas.Add(entrada);
    }

    public Guid? IdTipoEventoPorCodigo(string codigo) => Guid.NewGuid();

    public IList<Bitacora> Consultar(DateTime desde, DateTime hasta, Guid? idOrganizacion,
                                     string? codigoTipoEvento, Guid? idUsuario = null) =>
        throw Sin(nameof(Consultar));

    public IList<Bitacora> ConsultarErrores(DateTime desde, DateTime hasta, Guid? idOrganizacion,
                                            bool incluirAdvertencias) => throw Sin(nameof(ConsultarErrores));

    public IList<TipoEvento> TiposDeEvento() => throw Sin(nameof(TiposDeEvento));

    public IDictionary<string, int> ContarPorTipo(DateTime desde, DateTime hasta, Guid? idOrganizacion,
                                                 IEnumerable<string> codigos) => throw Sin(nameof(ContarPorTipo));

    private static NotSupportedException Sin(string miembro) =>
        new($"BitacoraDaoFalso.{miembro} no está preparado: la prueba no debería llegar acá.");
}

/// <summary>Un logger que guarda lo que le dijeron, para poder afirmarlo.</summary>
public class LoggerFalso : ILoggerService
{
    public List<string> Informaciones { get; } = [];
    public List<string> Advertencias { get; } = [];
    public List<(string Mensaje, Exception? Ex)> Errores { get; } = [];

    public void Informacion(string mensaje) => Informaciones.Add(mensaje);
    public void Advertencia(string mensaje) => Advertencias.Add(mensaje);
    public void Error(string mensaje, Exception? ex = null) => Errores.Add((mensaje, ex));
}

/// <summary>El plan comercial de la organización, puesto por la prueba.</summary>
public class OrganizacionDaoFalso(PlanComercial? plan = null) : IOrganizacionDao
{
    public PlanComercial? PlanDe(Guid idOrganizacion) => plan;

    public Organizacion? GetById(Guid id) => throw Sin(nameof(GetById));
    public IList<PlanComercial> TodosLosPlanes() => plan is null ? [] : [plan];
    public IList<Organizacion> Activas() => throw Sin(nameof(Activas));

    private static NotSupportedException Sin(string miembro) =>
        new($"OrganizacionDaoFalso.{miembro} no está preparado: la prueba no debería llegar acá.");
}

/// <summary>
/// Comprobantes en memoria.
///
/// Guarda de verdad: las reglas que se prueban —que emitir numere, que un
/// emitido no se modifique, que el período no se duplique— sólo tienen sentido
/// sobre algo que conserva lo que se le guardó.
/// </summary>
public class FacturacionDaoFalso : IFacturacionDao
{
    public List<Comprobante> Comprobantes { get; } = [];
    public Dictionary<Guid, List<LineaComprobante>> LineasPorComprobante { get; } = [];

    /// <summary>Cuántos equipos administrados devuelve, sea cual sea la fecha.</summary>
    public int Equipos { get; set; }

    public IList<Comprobante> Buscar(string? estado, string? periodo) =>
        [.. Comprobantes.Where(c => (estado is null || c.Estado == estado)
                                 && (periodo is null || c.Periodo == periodo))];

    public Comprobante? PorId(Guid id)
    {
        var c = Comprobantes.FirstOrDefault(x => x.Id == id);
        if (c is not null) c.Lineas = [.. Lineas(id)];
        return c;
    }

    public Comprobante? PorPeriodo(string periodo) =>
        Comprobantes.FirstOrDefault(c => c.Periodo == periodo);

    public IList<LineaComprobante> Lineas(Guid idComprobante) =>
        LineasPorComprobante.TryGetValue(idComprobante, out var l) ? [.. l] : [];

    public int EquiposAdministradosAl(DateOnly fecha) => Equipos;

    public Guid Guardar(Comprobante comprobante)
    {
        if (comprobante.Id == Guid.Empty) comprobante.Id = Guid.NewGuid();
        if (!Comprobantes.Contains(comprobante)) Comprobantes.Add(comprobante);
        return comprobante.Id;
    }

    public void ReemplazarLineas(Guid idComprobante, IEnumerable<LineaComprobante> lineas) =>
        LineasPorComprobante[idComprobante] = [.. lineas];

    public void Eliminar(Guid id)
    {
        Comprobantes.RemoveAll(c => c.Id == id && c.Estado == EstadoComprobante.Borrador);
        LineasPorComprobante.Remove(id);
    }

    public int ProximoNumero() =>
        Comprobantes.Where(c => c.Numero.HasValue).Select(c => c.Numero!.Value).DefaultIfEmpty(0).Max() + 1;
}
