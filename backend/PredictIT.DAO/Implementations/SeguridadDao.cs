using Microsoft.Data.SqlClient;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Helpers;
using PredictIT.DAO.Mappers;
using PredictIT.Domain.Seguridad;

namespace PredictIT.DAO.Implementations;

public class UsuarioDao : IUsuarioDao
{
    private const string Seleccion = @"
        SELECT id_usuario, nombre, apellido, email, username, [password], telefono, activo,
               bloqueado, intentos_fallidos, fecha_alta, ultimo_acceso, id_organizacion, id_idioma
        FROM dbo.Usuario";

    private readonly SqlHelperSeguridad _sql;
    private readonly UsuarioMapper _mapper = new();

    public UsuarioDao(SqlHelperSeguridad sql) => _sql = sql;

    public Usuario? PorUsername(string username) => _sql.ConsultarUno(
        Seleccion + " WHERE username = @u;", _mapper.Map, new SqlParameter("@u", username));

    public Usuario? GetById(Guid id) => _sql.ConsultarUno(
        Seleccion + " WHERE id_usuario = @id;", _mapper.Map, new SqlParameter("@id", id));

    /// <summary>
    /// Usuarios de una organización.
    ///
    /// La pertenencia se expresa de dos formas y hay que mirar las dos: los
    /// usuarios internos la tienen en <c>Usuario.id_organizacion</c>, y el
    /// Partner —que no pertenece a ninguna— tiene una fila en
    /// <c>Usuario_Organizacion</c> por cada cliente que atiende. Mirando sólo la
    /// columna, el Partner quedaba afuera y su nombre salía vacío en las
    /// pantallas de las organizaciones que administra.
    /// </summary>
    public IList<Usuario> PorOrganizacion(Guid idOrganizacion) => _sql.Consultar(
        Seleccion + @"
        WHERE id_organizacion = @org
           OR EXISTS (SELECT 1 FROM dbo.Usuario_Organizacion uo
                      WHERE uo.id_usuario = dbo.Usuario.id_usuario
                        AND uo.id_organizacion = @org)
        ORDER BY apellido, nombre;",
        _mapper.Map, new SqlParameter("@org", idOrganizacion));

    public void RegistrarAccesoExitoso(Guid idUsuario) => _sql.EjecutarNoConsulta(@"
        UPDATE dbo.Usuario SET ultimo_acceso = @ahora, intentos_fallidos = 0
        WHERE id_usuario = @id;",
        new SqlParameter("@id", idUsuario), Reloj.Parametro());

    public void CambiarEstado(Guid idUsuario, bool activo) => _sql.EjecutarNoConsulta(
        "UPDATE dbo.Usuario SET activo = @activo WHERE id_usuario = @id;",
        new SqlParameter("@id", idUsuario), new SqlParameter("@activo", activo));

    public void ActualizarHash(Guid idUsuario, string hash) => _sql.EjecutarNoConsulta(
        "UPDATE dbo.Usuario SET password = @hash WHERE id_usuario = @id;",
        new SqlParameter("@id", idUsuario), new SqlParameter("@hash", hash));

    public void Desbloquear(Guid idUsuario) => _sql.EjecutarNoConsulta(
        "UPDATE dbo.Usuario SET bloqueado = 0, intentos_fallidos = 0 WHERE id_usuario = @id;",
        new SqlParameter("@id", idUsuario));

    /// <summary>
    /// Reemplaza los roles del usuario. El borrado y las altas van en una sola
    /// transacción: si fallara en el medio, el usuario quedaría sin ningún rol,
    /// que es peor que no haber cambiado nada.
    /// </summary>
    public void AsignarRoles(Guid idUsuario, IEnumerable<Guid> idsRol)
    {
        var ids = idsRol.Distinct().ToList();

        using var cn = _sql.AbrirConexion();
        using var tx = cn.BeginTransaction();
        try
        {
            _sql.EjecutarNoConsultaEnTransaccion(cn, tx,
                "DELETE FROM dbo.Usuario_Rol WHERE id_usuario = @u;",
                new SqlParameter("@u", idUsuario));

            foreach (var idRol in ids)
            {
                _sql.EjecutarNoConsultaEnTransaccion(cn, tx,
                    "INSERT INTO dbo.Usuario_Rol (id_usuario, id_rol) VALUES (@u, @r);",
                    new SqlParameter("@u", idUsuario), new SqlParameter("@r", idRol));
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Suma un intento fallido y bloquea al quinto.
    ///
    /// El bloqueo se decide en la misma sentencia que incrementa el contador, y
    /// no leyendo primero y escribiendo después, para que dos intentos
    /// simultáneos no se pisen el contador.
    /// </summary>
    public void RegistrarAccesoFallido(Guid idUsuario) => _sql.EjecutarNoConsulta(@"
        UPDATE dbo.Usuario
        SET intentos_fallidos = intentos_fallidos + 1,
            bloqueado = CASE WHEN intentos_fallidos + 1 >= 5 THEN 1 ELSE bloqueado END
        WHERE id_usuario = @id;", new SqlParameter("@id", idUsuario));
}

/// <summary>
/// Arma el árbol de permisos del usuario.
///
/// Trae de la base las tres relaciones —familias del rol, familias anidadas y
/// patentes— y con eso construye los objetos del dominio. La recursión no se
/// resuelve en SQL: la resuelve el Composite, que es donde tiene que estar.
/// </summary>
public class PermisoDao : IPermisoDao
{
    private readonly SqlHelperSeguridad _sql;

    public PermisoDao(SqlHelperSeguridad sql) => _sql = sql;

    public IList<Rol> RolesDe(Guid idUsuario)
    {
        var roles = _sql.Consultar(@"
            SELECT r.id_rol, r.nombre, r.descripcion, r.es_sistema
            FROM dbo.Rol r
            JOIN dbo.Usuario_Rol ur ON ur.id_rol = r.id_rol
            WHERE ur.id_usuario = @u;",
            f => new Rol
            {
                Id = f.Guid("id_rol"),
                Nombre = f.Texto("nombre"),
                Descripcion = f.TextoNulo("descripcion"),
                EsSistema = f.Booleano("es_sistema")
            },
            new SqlParameter("@u", idUsuario));

        if (roles.Count == 0) return roles;

        // Se traen los tres conjuntos completos y se arma el árbol en memoria.
        // Son tablas de decenas de filas: traerlas enteras sale más barato que
        // una consulta por rol, y evita el problema N+1.
        var familias = CargarFamilias();
        var patentes = CargarPatentes();
        VincularPatentesAFamilias(familias, patentes);
        AnidarFamilias(familias);

        var rolFamilia = Relacion("SELECT id_rol, id_familia FROM dbo.Rol_Familia;", "id_rol", "id_familia");
        var rolPatente = Relacion("SELECT id_rol, id_patente FROM dbo.Rol_Patente;", "id_rol", "id_patente");

        foreach (var rol in roles)
        {
            foreach (var idFamilia in rolFamilia.Where(x => x.Item1 == rol.Id).Select(x => x.Item2))
            {
                if (familias.TryGetValue(idFamilia, out var familia)) rol.Familias.Add(familia);
            }
            foreach (var idPatente in rolPatente.Where(x => x.Item1 == rol.Id).Select(x => x.Item2))
            {
                if (patentes.TryGetValue(idPatente, out var patente)) rol.PatentesDirectas.Add(patente);
            }
        }

        return roles;
    }

    public IList<Guid> OrganizacionesDe(Guid idUsuario) => _sql.Consultar(
        "SELECT id_organizacion FROM dbo.Usuario_Organizacion WHERE id_usuario = @u;",
        f => f.Guid("id_organizacion"), new SqlParameter("@u", idUsuario));

    public IList<Rol> TodosLosRoles()
    {
        var roles = _sql.Consultar(@"
            SELECT id_rol, nombre, descripcion, es_sistema
            FROM dbo.Rol ORDER BY nombre;",
            f => new Rol
            {
                Id = f.Guid("id_rol"),
                Nombre = f.Texto("nombre"),
                Descripcion = f.TextoNulo("descripcion"),
                EsSistema = f.Booleano("es_sistema")
            });

        if (roles.Count == 0) return roles;

        // Mismo armado en memoria que RolesDe, por la misma razón: son tablas de
        // decenas de filas y una consulta por rol seria el problema N+1.
        var familias = CargarFamilias();
        var patentes = CargarPatentes();
        VincularPatentesAFamilias(familias, patentes);
        AnidarFamilias(familias);

        var rolFamilia = Relacion("SELECT id_rol, id_familia FROM dbo.Rol_Familia;", "id_rol", "id_familia");
        var rolPatente = Relacion("SELECT id_rol, id_patente FROM dbo.Rol_Patente;", "id_rol", "id_patente");

        foreach (var rol in roles)
        {
            foreach (var idFamilia in rolFamilia.Where(x => x.Item1 == rol.Id).Select(x => x.Item2))
            {
                if (familias.TryGetValue(idFamilia, out var familia)) rol.Familias.Add(familia);
            }
            foreach (var idPatente in rolPatente.Where(x => x.Item1 == rol.Id).Select(x => x.Item2))
            {
                if (patentes.TryGetValue(idPatente, out var patente)) rol.PatentesDirectas.Add(patente);
            }
        }

        return roles;
    }

    private Dictionary<Guid, Familia> CargarFamilias() => _sql.Consultar(
        "SELECT id_familia, nombre, descripcion FROM dbo.Familia;",
        f => new Familia
        {
            Id = f.Guid("id_familia"),
            Nombre = f.Texto("nombre"),
            Descripcion = f.TextoNulo("descripcion")
        }).ToDictionary(x => x.Id);

    private Dictionary<Guid, Patente> CargarPatentes() => _sql.Consultar(
        "SELECT id_patente, nombre, data_key, descripcion, modulo, tipo_acceso FROM dbo.Patente;",
        new PatenteMapper().Map).ToDictionary(x => x.Id);

    private void VincularPatentesAFamilias(Dictionary<Guid, Familia> familias, Dictionary<Guid, Patente> patentes)
    {
        foreach (var (idFamilia, idPatente) in Relacion(
            "SELECT id_familia, id_patente FROM dbo.Familia_Patente;", "id_familia", "id_patente"))
        {
            if (familias.TryGetValue(idFamilia, out var familia) &&
                patentes.TryGetValue(idPatente, out var patente))
            {
                familia.Agregar(patente);
            }
        }
    }

    private void AnidarFamilias(Dictionary<Guid, Familia> familias)
    {
        foreach (var (idPadre, idHijo) in Relacion(
            "SELECT id_familia, id_familia_hijo FROM dbo.Familia_Familia;", "id_familia", "id_familia_hijo"))
        {
            if (familias.TryGetValue(idPadre, out var padre) &&
                familias.TryGetValue(idHijo, out var hijo))
            {
                padre.Agregar(hijo);
            }
        }
    }

    private IList<(Guid, Guid)> Relacion(string sql, string columnaA, string columnaB) =>
        _sql.Consultar(sql, f => (f.Guid(columnaA), f.Guid(columnaB)));
}

public class BitacoraDao : IBitacoraDao
{
    private readonly SqlHelperSeguridad _sql;
    private readonly BitacoraMapper _mapper = new();

    public BitacoraDao(SqlHelperSeguridad sql) => _sql = sql;

    public void Registrar(Bitacora e)
    {
        if (e.Id == Guid.Empty) e.Id = Guid.NewGuid();
        _sql.EjecutarNoConsulta(@"
            INSERT INTO dbo.Bitacora
                (id_bitacora, id_usuario, usuario_texto, id_tipo_evento, fecha, descripcion,
                 entidad, id_entidad, id_organizacion, ip, traza)
            VALUES (@id, @usuario, @texto, @tipo, @ahora, @desc, @entidad, @identidad, @org, @ip, @traza);",
            Reloj.Parametro(),
            new SqlParameter("@id", e.Id),
            new SqlParameter("@usuario", e.IdUsuario ?? (object)DBNull.Value),
            new SqlParameter("@texto", e.UsuarioTexto ?? (object)DBNull.Value),
            new SqlParameter("@tipo", e.IdTipoEvento),
            new SqlParameter("@desc", e.Descripcion),
            new SqlParameter("@entidad", e.Entidad ?? (object)DBNull.Value),
            new SqlParameter("@identidad", e.IdEntidad ?? (object)DBNull.Value),
            new SqlParameter("@org", e.IdOrganizacion ?? (object)DBNull.Value),
            new SqlParameter("@ip", e.Ip ?? (object)DBNull.Value),
            new SqlParameter("@traza", e.Traza ?? (object)DBNull.Value));
    }

    public IList<TipoEvento> TiposDeEvento() => _sql.Consultar(@"
        SELECT id_tipo_evento, codigo, nombre, descripcion, criticidad
        FROM dbo.TipoEvento ORDER BY codigo;",
        f => new TipoEvento
        {
            Id = f.Guid("id_tipo_evento"),
            Codigo = f.Texto("codigo"),
            Nombre = f.Texto("nombre"),
            Descripcion = f.TextoNulo("descripcion"),
            Criticidad = f.Texto("criticidad").ToUpperInvariant() switch
            {
                "CRITICO" => CriticidadEvento.Critico,
                "ERROR" => CriticidadEvento.Error,
                "ADVERTENCIA" => CriticidadEvento.Advertencia,
                _ => CriticidadEvento.Info
            }
        });

    public IDictionary<string, int> ContarPorTipo(DateTime desde, DateTime hasta,
                                                  Guid? idOrganizacion,
                                                  IEnumerable<string> codigos)
    {
        var lista = codigos.ToList();
        if (lista.Count == 0) return new Dictionary<string, int>();

        // Los codigos van como parametros y no interpolados: son constantes del
        // dominio y no entrada de nadie, pero la regla de que toda consulta usa
        // SqlParameter no tiene excepciones «porque este caso es seguro».
        var marcas = string.Join(", ", lista.Select((_, i) => "@c" + i));
        var parametros = new List<SqlParameter>
        {
            new("@desde", desde),
            new("@hasta", hasta),
            new("@org", idOrganizacion ?? (object)DBNull.Value)
        };
        parametros.AddRange(lista.Select((c, i) => new SqlParameter("@c" + i, c)));

        var filas = _sql.Consultar($@"
            SELECT te.codigo, COUNT(*) AS cantidad
            FROM dbo.Bitacora b
            JOIN dbo.TipoEvento te ON te.id_tipo_evento = b.id_tipo_evento
            WHERE b.fecha >= @desde AND b.fecha < @hasta
              AND (@org IS NULL OR b.id_organizacion = @org)
              AND te.codigo IN ({marcas})
            GROUP BY te.codigo;",
            f => new { Codigo = f.Texto("codigo"), Cantidad = f.Entero("cantidad") },
            parametros.ToArray());

        return filas.ToDictionary(f => f.Codigo, f => f.Cantidad);
    }

    public IList<Bitacora> Consultar(DateTime desde, DateTime hasta, Guid? idOrganizacion,
                                     string? codigoTipoEvento, Guid? idUsuario = null) => _sql.Consultar(@"
        SELECT b.id_bitacora, b.id_usuario, b.usuario_texto, b.id_tipo_evento, b.fecha,
               b.descripcion, b.entidad, b.id_entidad, b.id_organizacion, b.ip, b.traza,
               te.codigo
        FROM dbo.Bitacora b
        JOIN dbo.TipoEvento te ON te.id_tipo_evento = b.id_tipo_evento
        WHERE b.fecha >= @desde AND b.fecha < @hasta
          AND (@org IS NULL OR b.id_organizacion = @org)
          AND (@codigo IS NULL OR te.codigo = @codigo)
          AND (@usuario IS NULL OR b.id_usuario = @usuario)
        ORDER BY b.fecha DESC;",
        _mapper.Map,
        new SqlParameter("@desde", desde),
        new SqlParameter("@hasta", hasta),
        new SqlParameter("@org", idOrganizacion ?? (object)DBNull.Value),
        new SqlParameter("@codigo", (object?)codigoTipoEvento ?? DBNull.Value),
        new SqlParameter("@usuario", idUsuario ?? (object)DBNull.Value));

    public IList<Bitacora> ConsultarErrores(DateTime desde, DateTime hasta,
                                            Guid? idOrganizacion, bool incluirAdvertencias) =>
        _sql.Consultar(@"
        SELECT b.id_bitacora, b.id_usuario, b.usuario_texto, b.id_tipo_evento, b.fecha,
               b.descripcion, b.entidad, b.id_entidad, b.id_organizacion, b.ip, b.traza,
               te.codigo
        FROM dbo.Bitacora b
        JOIN dbo.TipoEvento te ON te.id_tipo_evento = b.id_tipo_evento
        WHERE b.fecha >= @desde AND b.fecha < @hasta
          AND (@org IS NULL OR b.id_organizacion = @org)
          AND (te.criticidad IN ('ERROR','CRITICO')
               OR (@advertencias = 1 AND te.criticidad = 'ADVERTENCIA'))
        ORDER BY b.fecha DESC;",
        _mapper.Map,
        new SqlParameter("@desde", desde),
        new SqlParameter("@hasta", hasta),
        new SqlParameter("@org", idOrganizacion ?? (object)DBNull.Value),
        new SqlParameter("@advertencias", incluirAdvertencias ? 1 : 0));

    public Guid? IdTipoEventoPorCodigo(string codigo)
    {
        var v = _sql.EjecutarEscalar(
            "SELECT id_tipo_evento FROM dbo.TipoEvento WHERE codigo = @c;",
            new SqlParameter("@c", codigo));
        return v is Guid g ? g : null;
    }
}

/// <summary>Historial de respaldos. Vive en la base de servicio (Req. Arq. 003).</summary>
public class RespaldoDao(SqlHelperSeguridad sql) : IRespaldoDao
{
    public void Registrar(Respaldo r)
    {
        if (r.Id == Guid.Empty) r.Id = Guid.NewGuid();

        sql.EjecutarNoConsulta(@"
            INSERT INTO dbo.Respaldo
                (id_respaldo, base, nombre_archivo, ruta, fecha, tamano_bytes, tipo,
                 id_usuario, resultado, detalle)
            VALUES (@id, @base, @archivo, @ruta, @fecha, @tamano, @tipo, @usuario, @resultado, @detalle);",
            new SqlParameter("@fecha", r.Fecha == default ? Reloj.Ahora : r.Fecha),
            new SqlParameter("@id", r.Id),
            new SqlParameter("@base", r.Base),
            new SqlParameter("@archivo", r.NombreArchivo),
            new SqlParameter("@ruta", r.Ruta),
            new SqlParameter("@tamano", r.TamanoBytes ?? (object)DBNull.Value),
            new SqlParameter("@tipo", r.Tipo),
            new SqlParameter("@usuario", r.IdUsuario ?? (object)DBNull.Value),
            new SqlParameter("@resultado", r.Resultado),
            new SqlParameter("@detalle", r.Detalle ?? (object)DBNull.Value));
    }

    public Respaldo? PorId(Guid id) => sql.ConsultarUno(@"
        SELECT id_respaldo, base, nombre_archivo, ruta, fecha,
               tamano_bytes, tipo, id_usuario, resultado, detalle
        FROM dbo.Respaldo WHERE id_respaldo = @id;",
        Mapear,
        new SqlParameter("@id", id));

    public IList<Respaldo> Historial(int cantidad = 50) => sql.Consultar(@"
        SELECT TOP (@tope) id_respaldo, base, nombre_archivo, ruta, fecha,
               tamano_bytes, tipo, id_usuario, resultado, detalle
        FROM dbo.Respaldo
        ORDER BY fecha DESC;",
        Mapear,
        new SqlParameter("@tope", Math.Clamp(cantidad, 1, 500)));

    /// <summary>
    /// El mapeo de la tabla, en un solo lugar: dos consultas con dos mapeos de
    /// la misma tabla se desincronizan la primera vez que se agrega una columna.
    /// </summary>
    private static Respaldo Mapear(System.Data.IDataRecord f) => new()
    {
        Id = f.Guid("id_respaldo"),
        Base = f.Texto("base"),
        NombreArchivo = f.Texto("nombre_archivo"),
        Ruta = f.Texto("ruta"),
        Fecha = f.Fecha("fecha"),
        TamanoBytes = f.LargoNulo("tamano_bytes"),
        Tipo = f.Texto("tipo"),
        IdUsuario = f.GuidNulo("id_usuario"),
        Resultado = f.Texto("resultado"),
        Detalle = f.TextoNulo("detalle")
    };
}
