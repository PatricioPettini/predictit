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
/// Respaldos y restauración de las dos bases (Req. Arq. 003).
///
/// Restaurar pide que quien opera escriba el nombre de la base: es la
/// confirmación de que sabe qué va a reemplazar. Y el resultado se asienta
/// haya salido bien o mal, porque un respaldo que falló en silencio es peor
/// que no tener respaldos.
/// </summary>
public class RespaldoBusiness(
    IFactoryDaoSeguridad seguridad,
    IContextoSesion contexto,
    IBitacoraService bitacora,
    IRespaldoService respaldos)
    : BaseAdministracion(seguridad, contexto), IRespaldoBusiness
{
    // ------------------------------------------------------------ respaldos

    public PanelRespaldosDto PanelRespaldos()
    {
        Exigir(Patentes.BackupGestionar);

        var historial = Seguridad.Respaldos.Historial();
        var nombres = MapaDeUsuarios();

        // El último correcto por base. Que existan cien respaldos viejos no dice
        // nada si el último falló, así que es el dato que va arriba.
        var ultimoOk = respaldos.BasesQueRespalda.ToDictionary(
            b => b,
            b => historial
                .Where(r => r.Base == b && r.Resultado == "OK")
                .Select(r => (DateTime?)r.Fecha)
                .FirstOrDefault());

        return new PanelRespaldosDto(
            respaldos.BasesQueRespalda,
            ultimoOk,
            historial.Select(r => new RespaldoDto(
                r.Id, r.Base, r.NombreArchivo, r.Ruta, r.Fecha, r.TamanoBytes, r.Tipo,
                NombreDe(nombres, r.IdUsuario), r.Resultado == "OK", r.Detalle)).ToList());
    }

    public RespaldoDto Restaurar(Guid idRespaldo, string confirmoLaBase)
    {
        Exigir(Patentes.BackupGestionar);

        var respaldo = Seguridad.Respaldos.PorId(idRespaldo)
                       ?? throw new BusinessException("Ese respaldo no está registrado.");

        // La confirmación se compara contra el nombre real de la base del
        // respaldo. No es burocracia: restaurar reemplaza la base entera, y es
        // la única operación del sistema que puede perder datos que nadie
        // pidió borrar.
        if (!string.Equals((confirmoLaBase ?? string.Empty).Trim(), respaldo.Base,
                           StringComparison.OrdinalIgnoreCase))
        {
            throw BusinessException.De("confirmoLaBase",
                $"Para restaurar hay que escribir el nombre de la base: «{respaldo.Base}».");
        }

        var resultado = respaldos.Restaurar(respaldo.Base, respaldo.Ruta);

        if (!resultado.Ok)
        {
            bitacora.Registrar(TipoEvento.Codigos.ErrorSistema,
                $"Falló la restauración de {respaldo.Base} desde {respaldo.NombreArchivo}: {resultado.Detalle}",
                Contexto.IdUsuario, Contexto.IdOrganizacion);

            throw new BusinessException(
                $"No se pudo restaurar: {resultado.Detalle}. La base quedó como estaba.");
        }

        // Se asienta después de restaurar y no antes: la bitácora vive en la
        // base de servicio, así que si se restaura la de negocio el registro
        // sobrevive, y si se restaura la de servicio esta línea se pierde con
        // ella. Que se pierda es correcto: pasó a ser parte del pasado que se
        // restauró, y el logger de la aplicación lo tiene igual.
        bitacora.Registrar(TipoEvento.Codigos.Restore,
            $"{respaldo.Base} restaurada desde {respaldo.NombreArchivo} " +
            $"(respaldo del {respaldo.Fecha:dd/MM/yyyy HH:mm}) por {Contexto.Username}.",
            Contexto.IdUsuario, Contexto.IdOrganizacion, "Backup", idRespaldo);

        var nombres = MapaDeUsuarios();
        return new RespaldoDto(
            respaldo.Id, respaldo.Base, respaldo.NombreArchivo, respaldo.Ruta, respaldo.Fecha,
            respaldo.TamanoBytes, "RESTAURACION", NombreDe(nombres, Contexto.IdUsuario),
            true, resultado.Detalle);
    }

    public RespaldoDto Respaldar(string nombreBase)
    {
        Exigir(Patentes.BackupGestionar);

        if (!respaldos.BasesQueRespalda.Contains(nombreBase))
            throw BusinessException.De("base", "Esa no es una de las bases del sistema.");

        var resultado = respaldos.Respaldar(nombreBase);

        // Se registra el intento haya salido bien o mal. Un respaldo que falla en
        // silencio es el peor de los casos: nadie se enteraría hasta necesitarlo.
        var registro = new Respaldo
        {
            Base = resultado.Base,
            NombreArchivo = resultado.NombreArchivo,
            Ruta = resultado.Ruta,
            Fecha = DateTime.Now,
            TamanoBytes = resultado.TamanoBytes,
            Tipo = "MANUAL",
            IdUsuario = Contexto.IdUsuario,
            Resultado = resultado.Ok ? "OK" : "ERROR",
            Detalle = resultado.Detalle
        };

        Seguridad.Respaldos.Registrar(registro);

        bitacora.Registrar(resultado.Ok ? "BACKUP" : "ERROR_SISTEMA",
            resultado.Ok
                ? $"Respaldo de {nombreBase} en {resultado.NombreArchivo}" +
                  (resultado.TamanoBytes is { } t ? $" ({t / 1024 / 1024} MB)" : "") + "."
                : $"Falló el respaldo de {nombreBase}: {resultado.Detalle}");

        var nombres = MapaDeUsuarios();
        return new RespaldoDto(
            registro.Id, registro.Base, registro.NombreArchivo, registro.Ruta, registro.Fecha,
            registro.TamanoBytes, registro.Tipo, NombreDe(nombres, registro.IdUsuario),
            resultado.Ok, resultado.Detalle);
    }
}
