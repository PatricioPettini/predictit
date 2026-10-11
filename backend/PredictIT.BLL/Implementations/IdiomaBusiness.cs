using PredictIT.BLL.Contracts;
using PredictIT.BLL.DTO;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Factory;
using PredictIT.Domain.Seguridad;
using PredictIT.Service.Seguridad;

namespace PredictIT.BLL.Implementations;

/// <summary>
/// Multiidioma (Req. Arq. 001 · CU.Arq.004 Traducir).
///
/// Es el único business que atiende pedidos **sin sesión**: la pantalla de
/// inicio de sesión necesita su diccionario antes de que exista un usuario. Por
/// eso ninguna de sus lecturas exige patente, y la única escritura —cambiar la
/// preferencia— opera siempre sobre el usuario de la sesión y nunca sobre uno
/// indicado por parámetro.
/// </summary>
public class IdiomaBusiness(
    IFactoryDaoSeguridad dao,
    IContextoSesion contexto,
    IBitacoraService bitacora) : IIdiomaBusiness
{
    public IReadOnlyList<IdiomaDto> Idiomas() =>
        dao.Idiomas.Idiomas()
            .Select(i => new IdiomaDto(i.Codigo, i.Nombre, i.EsPorDefecto))
            .ToList();

    public DiccionarioDto Diccionario(string? codigo)
    {
        // Se resuelve el idioma en tres pasos: el pedido, la preferencia del
        // usuario si hay sesión, y el idioma por defecto. Así la misma llamada
        // sirve para el login —donde no hay sesión— y para el resto.
        var idioma = (codigo is not null ? dao.Idiomas.PorCodigo(codigo) : null)
                     ?? IdiomaDelUsuario()
                     ?? dao.Idiomas.PorDefecto();

        if (idioma is null)
        {
            // Sin ningún idioma activo la interfaz no puede dibujar un texto.
            // Es un problema de configuración y conviene decirlo.
            throw new BusinessException(
                "No hay ningún idioma activo configurado en el sistema.");
        }

        if (!idioma.Activo)
        {
            throw BusinessException.De("codigo",
                $"El idioma «{idioma.Codigo}» está desactivado.");
        }

        var textos = dao.Idiomas.Diccionario(idioma.Id);

        // El idioma por defecto se usa como respaldo de las claves faltantes.
        // Mostrar la clave cruda —«nav.activos»— sería peor para quien lo usa,
        // y la cobertura se mide aparte con `Cobertura()`.
        var porDefecto = dao.Idiomas.PorDefecto();
        if (porDefecto is not null && porDefecto.Id != idioma.Id)
        {
            foreach (var (clave, texto) in dao.Idiomas.Diccionario(porDefecto.Id))
            {
                textos.TryAdd(clave, texto);
            }
        }

        return new DiccionarioDto(idioma.Codigo, idioma.Nombre, textos);
    }

    public void CambiarIdioma(string codigo)
    {
        // No exige patente: cambiar el idioma de la propia interfaz no es una
        // operación sobre los datos de nadie. Lo que sí se controla es que
        // opere sobre el usuario de la sesión y no sobre uno indicado.
        if (contexto.IdUsuario == Guid.Empty)
            throw new BusinessException("Hace falta una sesión para guardar la preferencia.");

        var idioma = dao.Idiomas.PorCodigo(codigo)
                     ?? throw BusinessException.De("codigo", "Ese idioma no existe.");

        if (!idioma.Activo)
            throw BusinessException.De("codigo", "Ese idioma está desactivado.");

        dao.Idiomas.CambiarIdiomaDeUsuario(contexto.IdUsuario, idioma.Id);

        bitacora.Registrar(TipoEvento.Codigos.Modificacion,
            $"{contexto.Username} cambió el idioma de la interfaz a {idioma.Nombre}.");
    }

    public CoberturaIdiomaDto Cobertura()
    {
        Exigir(Patentes.IdiomaGestionar);

        var idiomas = dao.Idiomas.Idiomas();
        var faltantes = dao.Idiomas.ClavesFaltantes();

        var total = idiomas
            .Select(i => dao.Idiomas.Diccionario(i.Id).Count)
            .DefaultIfEmpty(0)
            .Max();

        return new CoberturaIdiomaDto(
            total,
            idiomas.Select(i => new CoberturaPorIdiomaDto(
                i.Codigo,
                i.Nombre,
                dao.Idiomas.Diccionario(i.Id).Count,
                faltantes.Where(f => f.CodigoIdioma == i.Codigo).Select(f => f.Clave).ToList()))
            .ToList());
    }

    /// <summary>
    /// El idioma que el usuario eligió, si hay sesión y si eligió alguno.
    ///
    /// Se resuelve por el identificador guardado en su ficha, no por el
    /// encabezado del navegador: la preferencia explícita gana sobre la
    /// automática.
    /// </summary>
    private Domain.Seguridad.Idioma? IdiomaDelUsuario()
    {
        if (contexto.IdUsuario == Guid.Empty) return null;

        var usuario = dao.Idiomas.Idiomas(soloActivos: false);
        var idUsuario = contexto.IdIdioma;

        return idUsuario is null ? null : usuario.FirstOrDefault(i => i.Id == idUsuario);
    }

    private void Exigir(string patente)
    {
        if (!contexto.Tiene(patente)) throw new PermisoDenegadoException(patente);
    }
}
