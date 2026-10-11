using System.Data;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Helpers;
using PredictIT.Domain.Negocio;
using PredictIT.Domain.Seguridad;

namespace PredictIT.DAO.Mappers;

public class TipoEquipoMapper : IObjectMapper<TipoEquipo>
{
    public TipoEquipo Map(IDataRecord f) => new()
    {
        Id = f.Guid("id_tipo_equipo"),
        Nombre = f.Texto("nombre")
    };
}

public class EstadoEquipoMapper : IObjectMapper<EstadoEquipo>
{
    public EstadoEquipo Map(IDataRecord f) => new()
    {
        Id = f.Guid("id_estado_equipo"),
        Nombre = f.Texto("nombre"),
        Operativo = f.Booleano("operativo"),
        Orden = f.Entero("orden")
    };
}

public class UbicacionMapper : IObjectMapper<Ubicacion>
{
    public Ubicacion Map(IDataRecord f) => new()
    {
        Id = f.Guid("id_ubicacion"),
        IdOrganizacion = f.Guid("id_organizacion"),
        Nombre = f.Texto("nombre"),
        Descripcion = f.TextoNulo("descripcion"),
        Activa = f.Booleano("activa")
    };
}

public class PlanComercialMapper : IObjectMapper<PlanComercial>
{
    public PlanComercial Map(IDataRecord f) => new()
    {
        Id = f.Guid("id_plan"),
        Codigo = f.Texto("codigo"),
        Nombre = f.Texto("nombre"),
        AbonoMensual = f.Decimal("abono_mensual"),
        EquiposIncluidos = f.Entero("equipos_incluidos"),
        PrecioEquipoAdicional = f.Decimal("precio_equipo_adicional"),
        Soporte = f.TextoNulo("soporte"),
        VigenteDesde = f.SoloFechaNula("vigente_desde") ?? default
    };
}

public class OrganizacionMapper : IObjectMapper<Organizacion>
{
    public Organizacion Map(IDataRecord f) => new()
    {
        Id = f.Guid("id_organizacion"),
        RazonSocial = f.Texto("razon_social"),
        NombreCorto = f.Texto("nombre_corto"),
        Cuit = f.TextoNulo("cuit"),
        Activa = f.Booleano("activa"),
        FechaAlta = f.Fecha("fecha_alta"),
        IdPlan = f.GuidNulo("id_plan")
    };
}

/// <summary>
/// Mapea el equipo con los nombres del catálogo ya resueltos por el JOIN.
///
/// Las columnas de nombre se leen sólo si vienen en el conjunto de resultados,
/// para que el mismo mapper sirva al listado (que trae los JOIN) y a un GetById
/// más chico.
/// </summary>
public class EquipoMapper : IObjectMapper<Equipo>
{
    public Equipo Map(IDataRecord f)
    {
        var equipo = new Equipo
        {
            Id = f.Guid("id_equipo"),
            IdOrganizacion = f.Guid("id_organizacion"),
            Codigo = f.Texto("codigo"),
            IdTipoEquipo = f.Guid("id_tipo_equipo"),
            Marca = f.TextoNulo("marca"),
            Modelo = f.TextoNulo("modelo"),
            NumeroSerie = f.TextoNulo("numero_serie"),
            DescripcionTecnica = f.TextoNulo("descripcion_tecnica"),
            FechaAlta = f.Fecha("fecha_alta"),
            FechaAdquisicion = f.SoloFechaNula("fecha_adquisicion"),
            FechaFinGarantia = f.SoloFechaNula("fecha_fin_garantia"),
            Proveedor = f.TextoNulo("proveedor"),
            Criticidad = f.Entero("criticidad"),
            IdEstadoEquipo = f.Guid("id_estado_equipo"),
            IdUbicacion = f.GuidNulo("id_ubicacion"),
            IdResponsable = f.GuidNulo("id_responsable")
        };

        if (f.Tiene("tipo_nombre"))
        {
            equipo.Tipo = new TipoEquipo { Id = equipo.IdTipoEquipo, Nombre = f.Texto("tipo_nombre") };
        }
        if (f.Tiene("estado_nombre"))
        {
            equipo.Estado = new EstadoEquipo
            {
                Id = equipo.IdEstadoEquipo,
                Nombre = f.Texto("estado_nombre"),
                Operativo = f.Booleano("estado_operativo")
            };
        }
        if (f.Tiene("ubicacion_nombre") && equipo.IdUbicacion is { } idUbic)
        {
            equipo.Ubicacion = new Ubicacion { Id = idUbic, Nombre = f.Texto("ubicacion_nombre") };
        }

        return equipo;
    }
}

public class UsuarioMapper : IObjectMapper<Usuario>
{
    public Usuario Map(IDataRecord f) => new()
    {
        Id = f.Guid("id_usuario"),
        Nombre = f.Texto("nombre"),
        Apellido = f.Texto("apellido"),
        Email = f.Texto("email"),
        Username = f.Texto("username"),
        PasswordHash = f.Texto("password"),
        Telefono = f.TextoNulo("telefono"),
        Activo = f.Booleano("activo"),
        Bloqueado = f.Booleano("bloqueado"),
        IntentosFallidos = f.Entero("intentos_fallidos"),
        FechaAlta = f.Fecha("fecha_alta"),
        UltimoAcceso = f.FechaNula("ultimo_acceso"),
        IdOrganizacion = f.GuidNulo("id_organizacion"),
        IdIdioma = f.GuidNulo("id_idioma")
    };
}

public class PatenteMapper : IObjectMapper<Patente>
{
    public Patente Map(IDataRecord f) => new()
    {
        Id = f.Guid("id_patente"),
        Nombre = f.Texto("nombre"),
        DataKey = f.Texto("data_key"),
        Descripcion = f.TextoNulo("descripcion"),
        Modulo = f.TextoNulo("modulo"),
        TipoAcceso = (TipoAcceso)f.Entero("tipo_acceso")
    };
}

public class BitacoraMapper : IObjectMapper<Bitacora>
{
    public Bitacora Map(IDataRecord f) => new()
    {
        Id = f.Guid("id_bitacora"),
        IdUsuario = f.GuidNulo("id_usuario"),
        UsuarioTexto = f.TextoNulo("usuario_texto"),
        IdTipoEvento = f.Guid("id_tipo_evento"),
        CodigoTipoEvento = f.Tiene("codigo") ? f.Texto("codigo") : null,
        Fecha = f.Fecha("fecha"),
        Descripcion = f.Texto("descripcion"),
        Entidad = f.TextoNulo("entidad"),
        IdEntidad = f.GuidNulo("id_entidad"),
        IdOrganizacion = f.GuidNulo("id_organizacion"),
        Ip = f.TextoNulo("ip"),
        Traza = f.TextoNulo("traza")
    };
}
