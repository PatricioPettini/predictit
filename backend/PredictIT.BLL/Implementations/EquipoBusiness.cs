using PredictIT.BLL.Contracts;
using PredictIT.BLL.DTO;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Factory;
using PredictIT.Domain.Negocio;
using PredictIT.Domain.Seguridad;
using PredictIT.Service.Seguridad;

namespace PredictIT.BLL.Implementations;

/// <summary>
/// Reglas de negocio de los activos informáticos (RF-04, RF-05, RF-06).
///
/// La validación vive acá y no en el controlador: así vale para cualquier punto
/// de entrada, no sólo para la API. El controlador se limita a traducir HTTP.
/// </summary>
public class EquipoBusiness : IEquipoBusiness
{
    private readonly IFactoryDao _dao;
    private readonly IFactoryDaoSeguridad _seguridad;
    private readonly IContextoSesion _contexto;
    private readonly IBitacoraService _bitacora;

    /// <summary>
    /// La predicción entra como función y no resuelta: si se pasara resuelta,
    /// construir el business de equipos construiría también el de predicción
    /// aunque la operación no lo necesite.
    /// </summary>
    private readonly Func<IPrediccionBusiness> _prediccion;

    public EquipoBusiness(IFactoryDao dao, IFactoryDaoSeguridad seguridad, IContextoSesion contexto,
                          IBitacoraService bitacora, Func<IPrediccionBusiness> prediccion)
    {
        _dao = dao;
        _seguridad = seguridad;
        _contexto = contexto;
        _bitacora = bitacora;
        _prediccion = prediccion;
    }

    public PaginaDto<EquipoListaDto> Buscar(FiltroEquipos filtro)
    {
        Exigir(Patentes.EquipoVer);

        var pagina = _dao.Equipos.Buscar(filtro);
        var responsables = MapaDeResponsables();

        return new PaginaDto<EquipoListaDto>
        {
            Items = pagina.Items.Select(e => new EquipoListaDto(
                e.Id, e.Codigo, e.Tipo?.Nombre ?? string.Empty, e.MarcaModelo, e.NumeroSerie,
                e.Ubicacion?.Nombre, NombreDe(responsables, e.IdResponsable),
                e.Estado?.Nombre ?? string.Empty, e.Estado?.Operativo ?? false)).ToList(),
            Total = pagina.Total,
            Pagina = pagina.Pagina,
            PorPagina = pagina.PorPagina
        };
    }

    public SegmentosDto Segmentos(FiltroEquipos filtro)
    {
        Exigir(Patentes.EquipoVer);

        var r = _dao.Equipos.ContarSegmentos(filtro);
        return new SegmentosDto(r.Todos, r.RiesgoAlto, r.PreventivoVencido, r.GarantiaVencida);
    }

    public PaginaDto<EquipoListaDto> MisEquipos()
    {
        if (!_contexto.Tiene(Patentes.HistorialVerPropio) && !_contexto.Tiene(Patentes.EquipoVer))
            throw new PermisoDenegadoException(Patentes.HistorialVerPropio);

        // El responsable se fuerza al usuario de la sesión: es lo que hace que
        // esta operación sea segura sin la patente amplia de inventario.
        var pagina = _dao.Equipos.Buscar(new FiltroEquipos
        {
            IdResponsable = _contexto.IdUsuario,
            PorPagina = 200
        });

        var responsables = MapaDeResponsables();

        return new PaginaDto<EquipoListaDto>
        {
            Items = pagina.Items.Select(e => new EquipoListaDto(
                e.Id, e.Codigo, e.Tipo?.Nombre ?? string.Empty, e.MarcaModelo, e.NumeroSerie,
                e.Ubicacion?.Nombre, NombreDe(responsables, e.IdResponsable),
                e.Estado?.Nombre ?? string.Empty, e.Estado?.Operativo ?? false)).ToList(),
            Total = pagina.Total,
            Pagina = pagina.Pagina,
            PorPagina = pagina.PorPagina
        };
    }

    public EquipoDetalleDto ObtenerDetalle(Guid id)
    {
        Exigir(Patentes.EquipoVer);

        var e = _dao.Equipos.GetById(id)
                ?? throw new BusinessException("El equipo no existe o no pertenece a tu organización.")
                   { Codigo = "no-encontrado" };

        var responsables = MapaDeResponsables();

        return new EquipoDetalleDto(
            e.Id, e.Codigo, e.IdTipoEquipo, e.Tipo?.Nombre ?? string.Empty,
            e.Marca, e.Modelo, e.NumeroSerie, e.DescripcionTecnica,
            e.FechaAlta, e.FechaAdquisicion, e.FechaFinGarantia,
            e.GarantiaVencida, e.DiasParaVencimientoGarantia, e.AntiguedadEnMeses,
            e.Proveedor, e.Criticidad,
            e.IdEstadoEquipo, e.Estado?.Nombre ?? string.Empty,
            e.IdUbicacion, e.Ubicacion?.Nombre,
            e.IdResponsable, NombreDe(responsables, e.IdResponsable));
    }

    public Guid Registrar(EquipoEntradaDto entrada)
    {
        Exigir(Patentes.EquipoGestionar);
        Validar(entrada, null);

        var equipo = new Equipo();
        Aplicar(entrada, equipo);

        var id = _dao.Equipos.Insert(equipo);

        _bitacora.Registrar(TipoEvento.Codigos.Alta,
            $"Alta del equipo {equipo.Codigo}.",
            _contexto.IdUsuario, _contexto.IdOrganizacion, "Equipo", id);

        return id;
    }

    public void Actualizar(Guid id, EquipoEntradaDto entrada)
    {
        Exigir(Patentes.EquipoGestionar);

        var equipo = _dao.Equipos.GetById(id)
                     ?? throw new BusinessException("El equipo no existe o no pertenece a tu organización.");

        Validar(entrada, id);
        var codigoAnterior = equipo.Codigo;
        Aplicar(entrada, equipo);
        equipo.Id = id;

        _dao.Equipos.Update(equipo);

        _bitacora.Registrar(TipoEvento.Codigos.Modificacion,
            codigoAnterior == equipo.Codigo
                ? $"Modificación del equipo {equipo.Codigo}."
                : $"Modificación del equipo {codigoAnterior}, ahora {equipo.Codigo}.",
            _contexto.IdUsuario, _contexto.IdOrganizacion, "Equipo", id);
    }

    public void CambiarEstado(Guid id, CambioEstadoEquipoDto entrada)
    {
        Exigir(Patentes.EquipoCambiarEstado);

        var equipo = _dao.Equipos.GetById(id)
                     ?? throw new BusinessException("El equipo no existe o no es de tu organización.");

        var estados = _dao.Catalogos.EstadosDeEquipo();
        var destino = estados.FirstOrDefault(e => e.Id == entrada.IdEstado)
                      ?? throw BusinessException.De("idEstado", "Ese estado no existe.");

        if (equipo.IdEstadoEquipo == destino.Id)
            throw BusinessException.De("idEstado", $"El equipo ya está «{destino.Nombre}».");

        var anterior = estados.FirstOrDefault(e => e.Id == equipo.IdEstadoEquipo)?.Nombre ?? "sin estado";

        // Dar de baja es otra operación, con su propia patente y sus propias
        // consecuencias. Acá se cambia el estado operativo de un equipo que
        // sigue siendo del parque.
        if (!destino.Operativo && destino.Nombre == "Dado de baja")
        {
            throw BusinessException.De("idEstado",
                "Para dar de baja un equipo se usa la baja, que además valida que no tenga incidencias abiertas.");
        }

        _dao.Equipos.CambiarEstado(id, destino.Id);

        var motivo = string.IsNullOrWhiteSpace(entrada.Motivo)
            ? string.Empty
            : $" Motivo: {entrada.Motivo.Trim()}";

        _bitacora.Registrar(TipoEvento.Codigos.EquipoCambioEstado,
            $"Equipo {equipo.Codigo}: {anterior} -> {destino.Nombre}.{motivo}",
            _contexto.IdUsuario, _contexto.IdOrganizacion, "Equipo", id);

        // Un equipo que deja de estar operativo sale de la evaluación, y uno
        // que vuelve entra: en los dos casos el riesgo del parque cambió.
        _prediccion().ReevaluarPorEvento(id);
    }

    public void DarDeBaja(Guid id)
    {
        Exigir(Patentes.EquipoGestionar);

        var equipo = _dao.Equipos.GetById(id)
                     ?? throw new BusinessException("El equipo no existe o no pertenece a tu organización.");

        _dao.Equipos.Delete(id);

        _bitacora.Registrar(TipoEvento.Codigos.Baja,
            $"Baja del equipo {equipo.Codigo}.",
            _contexto.IdUsuario, _contexto.IdOrganizacion, "Equipo", id);
    }

    public CatalogosEquipoDto Catalogos()
    {
        Exigir(Patentes.EquipoVer);

        return new CatalogosEquipoDto(
            _dao.Catalogos.TiposDeEquipo().Select(t => new TipoEquipoDto(t.Id, t.Nombre)).ToList(),
            _dao.Catalogos.EstadosDeEquipo().Select(e => new EstadoEquipoDto(e.Id, e.Nombre, e.Operativo)).ToList(),
            _dao.Catalogos.Ubicaciones().Select(u => new UbicacionDto(u.Id, u.Nombre, u.Descripcion)).ToList(),
            _seguridad.Usuarios.PorOrganizacion(_contexto.IdOrganizacion)
                .Where(u => u.Activo)
                .Select(u => new ResponsableDto(u.Id, u.NombreCompleto)).ToList());
    }

    // ------------------------------------------------------------------ reglas

    private void Validar(EquipoEntradaDto e, Guid? idExistente)
    {
        if (string.IsNullOrWhiteSpace(e.Codigo))
            throw BusinessException.De(nameof(e.Codigo), "El código de inventario es obligatorio.");

        e.Codigo = e.Codigo.Trim();

        if (_dao.Equipos.ExisteCodigo(e.Codigo, idExistente))
            throw BusinessException.De(nameof(e.Codigo),
                $"Ya existe un equipo con el código {e.Codigo} en esta organización.");

        if (!string.IsNullOrWhiteSpace(e.NumeroSerie))
        {
            e.NumeroSerie = e.NumeroSerie.Trim();
            if (_dao.Equipos.ExisteNumeroSerie(e.NumeroSerie, idExistente))
                throw BusinessException.De(nameof(e.NumeroSerie),
                    $"Ya existe un equipo con el número de serie {e.NumeroSerie}.");
        }

        if (e.IdTipoEquipo == Guid.Empty)
            throw BusinessException.De(nameof(e.IdTipoEquipo), "Hay que indicar el tipo de equipo.");

        if (e.IdEstadoEquipo == Guid.Empty)
            throw BusinessException.De(nameof(e.IdEstadoEquipo), "Hay que indicar el estado del equipo.");

        if (e.Criticidad is < 1 or > 4)
            throw BusinessException.De(nameof(e.Criticidad), "La criticidad va de 1 a 4.");

        // La garantía posterior a la compra es un error de carga tan común que
        // conviene atajarlo acá: si pasa, el motor predictivo calcula mal la
        // regla de garantía por vencer.
        if (e.FechaAdquisicion is { } compra && e.FechaFinGarantia is { } fin && fin < compra)
            throw BusinessException.De(nameof(e.FechaFinGarantia),
                "La garantía no puede vencer antes de la fecha de adquisición.");

        if (e.FechaAdquisicion is { } adq && adq > DateOnly.FromDateTime(DateTime.Today))
            throw BusinessException.De(nameof(e.FechaAdquisicion),
                "La fecha de adquisición no puede estar en el futuro.");

        // Las claves foráneas existen a nivel de base, pero un mensaje de error
        // de SQL Server no le sirve a nadie: se validan acá para poder explicar.
        if (e.IdUbicacion is { } idUbic && _dao.Catalogos.Ubicaciones().All(u => u.Id != idUbic))
            throw BusinessException.De(nameof(e.IdUbicacion),
                "La ubicación indicada no existe en esta organización.");
    }

    private static void Aplicar(EquipoEntradaDto e, Equipo destino)
    {
        destino.Codigo = e.Codigo.Trim();
        destino.IdTipoEquipo = e.IdTipoEquipo;
        destino.Marca = Limpiar(e.Marca);
        destino.Modelo = Limpiar(e.Modelo);
        destino.NumeroSerie = Limpiar(e.NumeroSerie);
        destino.DescripcionTecnica = Limpiar(e.DescripcionTecnica);
        destino.FechaAdquisicion = e.FechaAdquisicion;
        destino.FechaFinGarantia = e.FechaFinGarantia;
        destino.Proveedor = Limpiar(e.Proveedor);
        destino.Criticidad = e.Criticidad;
        destino.IdEstadoEquipo = e.IdEstadoEquipo;
        destino.IdUbicacion = e.IdUbicacion;
        destino.IdResponsable = e.IdResponsable;
    }

    private static string? Limpiar(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private void Exigir(string dataKey)
    {
        if (!_contexto.Tiene(dataKey)) throw new PermisoDenegadoException(dataKey);
    }

    /// <summary>
    /// Nombres de los responsables, resueltos en una sola consulta.
    ///
    /// Los usuarios viven en la otra base, así que no hay JOIN posible con el
    /// equipo (ADR 0003). Traerlos por lote evita el problema N+1 que aparecería
    /// consultando uno por fila.
    /// </summary>
    private Dictionary<Guid, string> MapaDeResponsables() =>
        _seguridad.Usuarios.PorOrganizacion(_contexto.IdOrganizacion)
            .ToDictionary(u => u.Id, u => u.NombreCompleto);

    private static string? NombreDe(IReadOnlyDictionary<Guid, string> mapa, Guid? id) =>
        id is { } i && mapa.TryGetValue(i, out var nombre) ? nombre : null;
}
