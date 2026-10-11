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
/// Bitácora y errores del sistema (Req. Arq. 002, CU.Arq.007).
///
/// Es la única de las cinco áreas que no escribe nada: sólo lee. La bitácora no
/// se puede modificar ni desde la base, así que tampoco tendría sentido
/// ofrecerlo acá.
/// </summary>
public class AuditoriaBusiness(
    IFactoryDaoSeguridad seguridad,
    IContextoSesion contexto)
    : BaseAdministracion(seguridad, contexto), IAuditoriaBusiness
{
    public IReadOnlyList<EntradaBitacoraDto> Bitacora(FiltroBitacoraDto filtro)
    {
        Exigir(Patentes.BitacoraVer);

        // Sin fechas, los últimos treinta días. Traer la bitácora completa de
        // arranque puede ser decenas de miles de filas.
        var hasta = (filtro.Hasta ?? DateTime.Today).Date.AddDays(1);
        var desde = (filtro.Desde ?? hasta.AddDays(-30)).Date;

        if (desde > hasta) throw BusinessException.De("desde", "La fecha «desde» es posterior a «hasta».");

        var entradas = Seguridad.Bitacora.Consultar(
            desde, hasta, Contexto.IdOrganizacion, filtro.CodigoTipoEvento, filtro.IdUsuario);

        var tipos = Seguridad.Bitacora.TiposDeEvento().ToDictionary(t => t.Codigo);
        var nombres = MapaDeUsuarios();

        return entradas.Select(e =>
        {
            var codigo = e.CodigoTipoEvento ?? string.Empty;
            var tipo = tipos.TryGetValue(codigo, out var t) ? t : null;

            return new EntradaBitacoraDto(
                e.Id,
                e.Fecha,
                tipo?.Nombre ?? codigo,
                (tipo?.Criticidad ?? CriticidadEvento.Info).ToString().ToUpperInvariant(),
                // En un login fallido no hay usuario que referenciar: se guardó
                // lo que se tecleó, y es justamente el dato que interesa.
                NombreDe(nombres, e.IdUsuario) ?? e.UsuarioTexto,
                e.Descripcion,
                e.Entidad,
                e.Ip);
        }).ToList();
    }

    public IReadOnlyList<CatalogoItemDto> TiposDeEvento()
    {
        Exigir(Patentes.BitacoraVer);

        return Seguridad.Bitacora.TiposDeEvento()
            .Select(t => new CatalogoItemDto(t.Id, $"{t.Nombre}"))
            .ToList();
    }


    public IReadOnlyList<ErrorDto> Errores(FiltroErroresDto filtro)
    {
        Exigir(Patentes.ErrorVer);

        // Siete días por defecto y no treinta como la bitácora: cuando se abre
        // esta pantalla es porque algo pasó, y lo que pasó es reciente.
        var hasta = (filtro.Hasta ?? DateTime.Today).Date.AddDays(1);
        var desde = (filtro.Desde ?? hasta.AddDays(-7)).Date;

        if (desde > hasta) throw BusinessException.De("desde", "La fecha «desde» es posterior a «hasta».");

        var entradas = Seguridad.Bitacora.ConsultarErrores(
            desde, hasta, Contexto.IdOrganizacion, filtro.IncluirAdvertencias);

        var tipos = Seguridad.Bitacora.TiposDeEvento().ToDictionary(t => t.Codigo);
        var nombres = MapaDeUsuarios();

        return entradas.Select(e =>
        {
            var codigo = e.CodigoTipoEvento ?? string.Empty;
            var tipo = tipos.TryGetValue(codigo, out var t) ? t : null;

            return new ErrorDto(
                e.Id,
                e.Fecha,
                tipo?.Nombre ?? codigo,
                (tipo?.Criticidad ?? CriticidadEvento.Error).ToString().ToUpperInvariant(),
                NombreDe(nombres, e.IdUsuario) ?? e.UsuarioTexto,
                e.Descripcion,
                e.Entidad,
                e.Ip,
                e.Traza);
        }).ToList();
    }
}
