using System.Text.Json;
using PredictIT.BLL.Contracts;
using PredictIT.BLL.DTO;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Factory;
using PredictIT.DAO.Helpers;
using PredictIT.Domain.Negocio;
using PredictIT.Domain.Seguridad;
using PredictIT.Service.IA;
using PredictIT.Service.Seguridad;

namespace PredictIT.BLL.Implementations;

/// <summary>
/// Reglas de negocio de las incidencias (RF-05 a RF-09, CU-006, CU-007, CU-013).
///
/// El flujo de registrar una incidencia es el corazón del sistema:
///
///   1. Se valida y se guarda la incidencia.
///   2. Se clasifica el texto libre (triage). Lo que el solicitante cargó a mano
///      manda: la sugerencia sólo completa lo que dejó vacío.
///   3. Se arma el contexto de asignación con lo que la organización autorizó
///      enviar, se consulta al proveedor y se persiste la recomendación con el
///      contexto exacto que se envió.
///   4. Si el proveedor no responde, se aplica la estrategia de respaldo y la
///      incidencia queda marcada para que la revise una persona.
///
/// Los pasos 2 a 4 nunca hacen fallar el paso 1. Que la asignación automática no
/// funcione no puede impedir que se registre el reporte de una falla.
/// </summary>
public class IncidenciaBusiness(
    IFactoryDao dao,
    IFactoryDaoSeguridad seguridad,
    IContextoSesion contexto,
    IBitacoraService bitacora,
    IProveedorIA proveedorIa,
    Func<IPrediccionBusiness> prediccion) : IIncidenciaBusiness
{
    private static readonly JsonSerializerOptions JsonCompacto = new() { WriteIndented = false };

    public PaginaDto<IncidenciaListaDto> Buscar(FiltroIncidencias filtro)
    {
        // Quien sólo puede ver las propias, ve las propias. El filtro se fuerza
        // acá y no se confía en que la pantalla lo mande.
        //
        // «Propias» son las que tiene asignadas o las que reportó, no sólo las
        // asignadas: el solicitante reporta y no atiende, y filtrar por técnico
        // le dejaba el listado vacío aunque hubiera reportado media docena.
        if (!contexto.Tiene(Patentes.IncidenciaVerTodas))
        {
            Exigir(Patentes.IncidenciaVerPropias);
            filtro.IdInvolucrado = contexto.IdUsuario;
            filtro.IdTecnico = null;
        }

        var pagina = dao.Incidencias.Buscar(filtro);
        var nombres = MapaDeUsuarios();
        var ahora = DateTime.Now;

        return new PaginaDto<IncidenciaListaDto>
        {
            Items = pagina.Items.Select(i => new IncidenciaListaDto(
                i.Id, i.Numero, i.Titulo,
                i.Equipo?.Codigo ?? string.Empty,
                i.Estado?.Nombre ?? string.Empty,
                !(i.Estado?.EsFinal ?? false),
                i.Prioridad?.Nombre ?? string.Empty,
                i.Prioridad?.Nivel ?? 0,
                i.Categoria?.Nombre,
                NombreDe(nombres, i.IdTecnico),
                TipoAsignacionTexto.A(i.TipoAsignacion),
                i.PendienteRevision,
                i.Fecha, i.FechaResolucion,
                i.FueraDeObjetivo(ahora))).ToList(),
            Total = pagina.Total,
            Pagina = pagina.Pagina,
            PorPagina = pagina.PorPagina
        };
    }

    public IncidenciaDetalleDto ObtenerDetalle(Guid id)
    {
        var i = dao.Incidencias.GetById(id)
                ?? throw new BusinessException("La incidencia no existe o no pertenece a tu organización.")
                   { Codigo = "no-encontrado" };

        // Ver el detalle de una incidencia ajena requiere la patente amplia. Sin
        // este control, la lista filtrada no serviría de nada: alcanzaría con
        // pedir el detalle por identificador.
        if (!contexto.Tiene(Patentes.IncidenciaVerTodas))
        {
            Exigir(Patentes.IncidenciaVerPropias);

            var propia = i.IdTecnico == contexto.IdUsuario ||
                         i.IdUsuarioReportante == contexto.IdUsuario;

            if (!propia) throw new PermisoDenegadoException(Patentes.IncidenciaVerTodas);
        }

        var nombres = MapaDeUsuarios();
        var recomendacion = dao.Incidencias.RecomendacionDe(id);
        var clasificacion = dao.Incidencias.ClasificacionDe(id);

        return new IncidenciaDetalleDto(
            i.Id, i.Numero, i.Titulo, i.Descripcion, i.Fecha, i.FechaResolucion,
            i.IdEquipo, i.Equipo?.Codigo ?? string.Empty,
            i.IdEstado, i.Estado?.Nombre ?? string.Empty, !(i.Estado?.EsFinal ?? false),
            i.IdPrioridad, i.Prioridad?.Nombre ?? string.Empty, i.Prioridad?.Nivel ?? 0,
            i.IdCategoria, i.Categoria?.Nombre,
            i.IdTecnico, NombreDe(nombres, i.IdTecnico),
            NombreDe(nombres, i.IdUsuarioReportante) ?? "—",
            TipoAsignacionTexto.A(i.TipoAsignacion), i.PendienteRevision,
            i.Diagnostico, i.Solucion,
            i.FueraDeObjetivo(DateTime.Now),
            Math.Round(i.HorasTranscurridas(DateTime.Now), 1),
            recomendacion is null ? null : ADto(recomendacion, nombres),
            clasificacion is null ? null : ADto(clasificacion));
    }

    public IncidenciaRegistradaDto Registrar(IncidenciaEntradaDto entrada)
    {
        Exigir(Patentes.IncidenciaRegistrar);

        var titulo = (entrada.Titulo ?? string.Empty).Trim();
        var descripcion = (entrada.Descripcion ?? string.Empty).Trim();

        if (titulo.Length < 5)
            throw BusinessException.De("titulo", "El título tiene que decir qué pasa, en al menos 5 caracteres.");
        if (titulo.Length > 150)
            throw BusinessException.De("titulo", "El título no puede pasar de 150 caracteres.");
        if (descripcion.Length < 10)
            throw BusinessException.De("descripcion", "Describí el problema con un poco más de detalle.");
        if (descripcion.Length > 2000)
            throw BusinessException.De("descripcion", "La descripción no puede pasar de 2000 caracteres.");

        var equipo = dao.Equipos.GetById(entrada.IdEquipo)
                     ?? throw BusinessException.De("idEquipo", "El equipo no existe o no es de tu organización.");

        var estadoInicial = dao.Catalogos.EstadoIncidenciaInicial()
                            ?? throw new BusinessException(
                                "No hay un estado inicial de incidencia configurado.");

        var prioridades = dao.Catalogos.Prioridades();
        var categorias = dao.Catalogos.Categorias();

        // --- 1) Triage. Antes de guardar, porque la clasificación puede aportar
        //        la prioridad y la categoría que el solicitante dejó vacías.
        var clasificacion = Clasificar(titulo, descripcion, equipo.Id, categorias, prioridades);

        var idCategoria = entrada.IdCategoria
                          ?? clasificacion?.IdCategoriaSugerida;

        var idPrioridad = entrada.IdPrioridad
                          ?? clasificacion?.IdPrioridadSugerida
                          // Sin dato ni sugerencia, la prioridad más baja. No la
                          // más alta: inflar prioridades hace que la cola pierda
                          // sentido y que lo urgente se mezcle con todo.
                          ?? prioridades.OrderBy(p => p.Nivel).First().Id;

        ValidarPertenece(idCategoria, categorias.Select(c => c.Id), "idCategoria", "La categoría no existe.");
        ValidarPertenece(idPrioridad, prioridades.Select(p => p.Id), "idPrioridad", "La prioridad no existe.");

        var incidencia = new Incidencia
        {
            Titulo = titulo,
            Descripcion = descripcion,
            Fecha = DateTime.Now,
            IdEquipo = equipo.Id,
            IdEstado = estadoInicial.Id,
            IdPrioridad = idPrioridad,
            IdCategoria = idCategoria,
            IdUsuarioReportante = contexto.IdUsuario,
            TipoAsignacion = TipoAsignacion.Pendiente
        };

        // --- 2) Alta. El número y la incidencia van en una transacción: dos
        //        altas simultáneas no pueden quedarse con el mismo correlativo.
        using (var unidad = dao.IniciarUnidadDeTrabajo())
        {
            incidencia.Numero = dao.Incidencias.ProximoNumero();
            dao.Incidencias.Insert(incidencia);
            unidad.Comprometer();
        }

        if (clasificacion is not null)
        {
            clasificacion.IdIncidencia = incidencia.Id;
            dao.Incidencias.GuardarClasificacion(clasificacion);

            // El código distingue quién clasificó. La tabla de clasificación ya
            // guarda el detalle; la bitácora es lo que permite auditarlo sin
            // entrar al detalle de cada incidencia.
            bitacora.Registrar(
                clasificacion.Origen == "IA"
                    ? TipoEvento.Codigos.ClasificacionIa
                    : TipoEvento.Codigos.ClasificacionHeuristica,
                $"Incidencia #{incidencia.Numero} clasificada automáticamente " +
                $"({clasificacion.Origen}): {categorias.FirstOrDefault(c => c.Id == idCategoria)?.Nombre ?? "sin categoría"}.",
                contexto.IdUsuario, contexto.IdOrganizacion, "Incidencia", incidencia.Id);
        }

        bitacora.Registrar(TipoEvento.Codigos.IncidenciaAlta,
            $"Incidencia #{incidencia.Numero} sobre el equipo {equipo.Codigo}: {titulo}");

        // --- 3) Asignación. Va después del alta y fuera de su transacción: si
        //        el proveedor tarda, no se sostiene una transacción abierta
        //        tomando bloqueos, y si falla la incidencia ya está guardada.
        var asignacion = Asignar(incidencia, equipo, idCategoria, categorias, prioridades);

        // --- 4) Reevaluación predictiva del equipo. Una incidencia nueva puede
        //        completar una recurrencia o cruzar un umbral, y la alerta tiene
        //        que aparecer ahora y no la próxima vez que alguien abra la
        //        pantalla (RF-11). No propaga errores.
        prediccion().ReevaluarPorEvento(equipo.Id);

        var nombres = MapaDeUsuarios();

        return new IncidenciaRegistradaDto(
            incidencia.Id,
            incidencia.Numero,
            asignacion is null ? null : ADto(asignacion, nombres),
            clasificacion is null ? null : ADto(clasificacion),
            AdvertenciaDe(asignacion));
    }

    /* ------------------------------------------------------------------
       Ciclo de vida de la incidencia

       La maquina de estados vive aca: en un solo lugar, o deja de ser una
       maquina de estados. La pantalla pregunta que transiciones hay y ofrece
       solo esas, pero no decide.

       Pendiente de asignacion -> Asignada (lo hace la asignacion) | Anulada
       Asignada                -> En curso | Anulada
       En curso                -> En espera de repuesto | Resuelta | Anulada
       En espera de repuesto   -> En curso | Anulada
       Resuelta                -> Cerrada | En curso (reapertura)
       Cerrada, Anulada        -> nada. Son finales.
       ------------------------------------------------------------------ */

    private const string Pendiente = "Pendiente de asignación";
    private const string Asignada = "Asignada";
    private const string EnCurso = "En curso";
    private const string EsperaRepuesto = "En espera de repuesto";
    private const string Resuelta = "Resuelta";
    private const string Cerrada = "Cerrada";
    private const string Anulada = "Anulada";

    private static readonly Dictionary<string, string[]> Permitidas = new()
    {
        [Pendiente] = [Anulada],
        [Asignada] = [EnCurso, Anulada],
        [EnCurso] = [EsperaRepuesto, Resuelta, Anulada],
        [EsperaRepuesto] = [EnCurso, Anulada],
        [Resuelta] = [Cerrada, EnCurso],
        [Cerrada] = [],
        [Anulada] = []
    };

    public IReadOnlyList<TransicionDto> TransicionesDe(Guid idIncidencia)
    {
        ExigirAlguna(Patentes.IncidenciaVerTodas, Patentes.IncidenciaVerPropias);

        var incidencia = dao.Incidencias.GetById(idIncidencia)
                         ?? throw new BusinessException("La incidencia no existe o no es de tu organización.");

        var estados = dao.Catalogos.EstadosDeIncidencia().ToDictionary(e => e.Id, e => e.Nombre);
        var actual = estados.TryGetValue(incidencia.IdEstado, out var n) ? n : string.Empty;

        if (!Permitidas.TryGetValue(actual, out var destinos)) return [];

        return destinos.Select(d => new TransicionDto(
            d,
            // Resolver exige explicación; anular y cerrar, no.
            ExigeSolucion: d == Resuelta,
            Impedimento: Impedimento(d, incidencia))).ToList();
    }

    /// <summary>
    /// Por qué una transición que la máquina permite igual no se puede hacer
    /// ahora. Se devuelve el motivo en lugar de esconder la opción: un botón
    /// que desaparece se lee como un bug, y uno deshabilitado con el motivo se
    /// lee como una regla.
    ///
    /// Sólo reporta lo que el formulario **no puede saber**: quién es el
    /// técnico asignado y si hay técnico. Que falte el diagnóstico o la
    /// solución no se reporta acá aunque resolver los exija, y el motivo es
    /// concreto: este método mira lo guardado, y el formulario manda los textos
    /// en la misma llamada que la transición. Reportarlo dejaba el botón
    /// «Resuelta» deshabilitado para siempre —para guardar el diagnóstico hacía
    /// falta una transición, y la transición pedía el diagnóstico guardado—.
    /// La validación de esos dos campos vive en <see cref="Atender"/>, que es
    /// donde se puede hacer bien.
    /// </summary>
    private string? Impedimento(string destino, Incidencia i)
    {
        if (destino == EnCurso && i.IdTecnico is null)
            return "Hace falta un técnico asignado para empezar a atenderla.";

        if (!PuedeAtender(i))
            return "Sólo la puede atender el técnico asignado.";

        return null;
    }

    /// <summary>
    /// Quién puede avanzar la atención: el técnico asignado, o quien pueda
    /// reasignar cualquier incidencia. Es la misma regla que ya rige la
    /// reasignación, y tenerla distinta acá sería una puerta lateral.
    /// </summary>
    private bool PuedeAtender(Incidencia i) =>
        contexto.Tiene(Patentes.IncidenciaReasignarTodas) || i.IdTecnico == contexto.IdUsuario;

    public void Atender(Guid idIncidencia, AtencionDto entrada)
    {
        Exigir(Patentes.IncidenciaAtender);

        var incidencia = dao.Incidencias.GetById(idIncidencia)
                         ?? throw new BusinessException("La incidencia no existe o no es de tu organización.");

        var estados = dao.Catalogos.EstadosDeIncidencia();
        var actual = estados.FirstOrDefault(e => e.Id == incidencia.IdEstado)?.Nombre ?? string.Empty;

        var destino = (entrada.Estado ?? string.Empty).Trim();
        var estadoDestino = estados.FirstOrDefault(e => e.Nombre == destino)
                            ?? throw BusinessException.De("estado", "Ese estado no existe.");

        if (!Permitidas.TryGetValue(actual, out var destinos) || !destinos.Contains(destino))
        {
            throw BusinessException.De("estado",
                $"Una incidencia «{actual}» no puede pasar a «{destino}».");
        }

        // Anular es un acto administrativo, no parte de la atención: cancela el
        // trabajo de otro. Por eso pide la patente de reasignar cualquiera.
        if (destino == Anulada && !contexto.Tiene(Patentes.IncidenciaReasignarTodas))
            throw new PermisoDenegadoException(Patentes.IncidenciaReasignarTodas);

        if (destino != Anulada && !PuedeAtender(incidencia))
        {
            throw new BusinessException(
                "Sólo la puede atender el técnico asignado. Pedí que te la reasignen.");
        }

        var diagnostico = Recortado(entrada.Diagnostico, 2000);
        var solucion = Recortado(entrada.Solucion, 2000);

        if (destino == EnCurso && incidencia.IdTecnico is null)
        {
            throw new BusinessException(
                "Hace falta un técnico asignado para empezar a atenderla.");
        }

        if (destino == Resuelta)
        {
            // Resolver sin decir qué se encontró y qué se hizo deja una
            // incidencia cerrada que no le sirve a nadie: ni al próximo técnico
            // que vea el mismo equipo, ni al motor predictivo.
            if (string.IsNullOrWhiteSpace(diagnostico ?? incidencia.Diagnostico))
                throw BusinessException.De("diagnostico", "Contá qué encontraste antes de resolverla.");

            if (string.IsNullOrWhiteSpace(solucion ?? incidencia.Solucion))
                throw BusinessException.De("solucion", "Contá qué hiciste para resolverla.");
        }

        // La fecha de resolución la escribe el paso a «resuelta» y la limpia la
        // reapertura. Es el dato del que sale el tiempo de reparación, así que
        // una incidencia reabierta no puede conservar la fecha vieja.
        DateTime? resolucion = destino switch
        {
            Resuelta => Reloj.Ahora,
            Cerrada => incidencia.FechaResolucion ?? Reloj.Ahora,
            EnCurso => null,
            Anulada => null,
            _ => incidencia.FechaResolucion
        };

        dao.Incidencias.Atender(idIncidencia, estadoDestino.Id, diagnostico, solucion, resolucion);

        var (codigo, texto) = destino switch
        {
            Resuelta => (TipoEvento.Codigos.IncidenciaResuelta,
                         $"Incidencia #{incidencia.Numero} resuelta en {Demora(incidencia, resolucion)}."),
            Cerrada => (TipoEvento.Codigos.IncidenciaCerrada,
                        $"Incidencia #{incidencia.Numero} cerrada."),
            Anulada => (TipoEvento.Codigos.IncidenciaAnulada,
                        $"Incidencia #{incidencia.Numero} anulada por {contexto.Username}."),
            _ => (TipoEvento.Codigos.IncidenciaAtendida,
                  $"Incidencia #{incidencia.Numero}: {actual} -> {destino}.")
        };

        bitacora.Registrar(codigo, texto, contexto.IdUsuario, contexto.IdOrganizacion,
                           "Incidencia", idIncidencia);

        // Resolver una incidencia cambia el historial del equipo, y el
        // historial es de lo que se alimentan las reglas. Igual que el alta: no
        // propaga errores.
        if (destino is Resuelta or Anulada)
            prediccion().ReevaluarPorEvento(incidencia.IdEquipo);
    }

    /// <summary>Cuánto tardó, en las unidades en que se habla del SLA.</summary>
    private static string Demora(Incidencia i, DateTime? resolucion)
    {
        if (resolucion is null) return "tiempo desconocido";

        var horas = (resolucion.Value - i.Fecha).TotalHours;
        if (horas < 1) return $"{Math.Max(1, (int)Math.Round(horas * 60))} min";
        if (horas < 48) return $"{horas:0.#} h";
        return $"{horas / 24:0.#} días";
    }

    private static string? Recortado(string? texto, int largo)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        texto = texto.Trim();
        return texto.Length <= largo ? texto : texto[..largo];
    }

    public void Reasignar(Guid idIncidencia, Guid idTecnico)
    {
        var incidencia = dao.Incidencias.GetById(idIncidencia)
                         ?? throw new BusinessException("La incidencia no existe o no es de tu organización.");

        // La regla del documento: el Responsable Técnico reasigna sólo las
        // suyas; el Administrador, cualquiera (CU-013).
        if (!contexto.Tiene(Patentes.IncidenciaReasignarTodas))
        {
            Exigir(Patentes.IncidenciaReasignarPropias);

            if (incidencia.IdTecnico != contexto.IdUsuario)
            {
                throw new BusinessException(
                    "Sólo podés reasignar las incidencias que tenés asignadas.");
            }
        }

        if (incidencia.Estado?.EsFinal == true)
            throw new BusinessException("La incidencia ya está cerrada: no se puede reasignar.");

        var candidatos = dao.Tecnicos.CandidatosPara(incidencia.IdEquipo);
        if (candidatos.All(c => c.IdUsuario != idTecnico))
        {
            throw BusinessException.De("idTecnico",
                "El técnico no existe o no pertenece a tu organización.");
        }

        // La reasignación manual limpia la marca de revisión: ya la revisó una
        // persona, que es justamente lo que la marca pedía.
        dao.Incidencias.Asignar(
            idIncidencia, idTecnico, TipoAsignacion.Manual, pendienteRevision: false,
            // Sólo se mueve si seguía en el estado inicial: si el técnico ya la
            // había puesto «en curso», reasignarla no puede hacerla retroceder.
            idEstado: incidencia.IdEstado == dao.Catalogos.EstadoIncidenciaInicial()?.Id
                ? dao.Catalogos.EstadoIncidenciaAsignada()?.Id
                : null);

        var nombre = candidatos.First(c => c.IdUsuario == idTecnico).NombreCompleto;
        bitacora.Registrar(TipoEvento.Codigos.ReasignacionManual,
            $"Incidencia #{incidencia.Numero} reasignada manualmente a {nombre}.");
    }

    public CatalogosIncidenciaDto Catalogos()
    {
        ExigirAlguna(Patentes.IncidenciaVerTodas, Patentes.IncidenciaVerPropias);

        var tecnicos = contexto.Tiene(Patentes.IncidenciaVerTodas)
            ? dao.Tecnicos.CandidatosPara(idEquipo: null)
            : [];

        return new CatalogosIncidenciaDto(
            dao.Catalogos.EstadosDeIncidencia()
                .Select(e => new EstadoIncidenciaDto(e.Id, e.Nombre, e.EsFinal)).ToList(),
            dao.Catalogos.Prioridades().Select(p => new CatalogoItemDto(p.Id, p.Nombre)).ToList(),
            dao.Catalogos.Categorias().Select(c => new CatalogoItemDto(c.Id, c.Nombre)).ToList(),
            tecnicos.Select(t => new TecnicoDto(
                t.IdUsuario, t.NombreCompleto,
                t.Especialidades.Select(e => e.Especialidad?.Nombre ?? string.Empty).ToList(),
                t.IncidenciasAbiertas)).ToList());
    }

    /// <summary>
    /// Guía de reparación sugerida (RF-15, CU-014).
    ///
    /// Se pide, no se genera sola: la mayoría de las incidencias se resuelven
    /// sin mirarla y consultar al proveedor en cada alta sería pagar por todas
    /// para que sirva en algunas. Tampoco se persiste. Es una sugerencia que
    /// depende del estado del equipo hoy, y guardarla invitaría a leer mañana
    /// una guía armada con otro historial; lo que queda registrado es que se
    /// consultó, con qué proveedor y sobre qué incidencia.
    /// </summary>
    public GuiaReparacionDto SugerirReparacion(Guid idIncidencia)
    {
        var i = dao.Incidencias.GetById(idIncidencia)
                ?? throw new BusinessException("La incidencia no existe o no pertenece a tu organización.");

        // Mismo control que el detalle: si no se puede ver la incidencia,
        // tampoco se puede pedir la guía de esa incidencia.
        if (!contexto.Tiene(Patentes.IncidenciaVerTodas))
        {
            Exigir(Patentes.IncidenciaVerPropias);
            var propia = i.IdTecnico == contexto.IdUsuario ||
                         i.IdUsuarioReportante == contexto.IdUsuario;
            if (!propia) throw new PermisoDenegadoException(Patentes.IncidenciaVerTodas);
        }

        var equipo = i.Equipo;
        var antecedentes = dao.Incidencias
            .DeEquipoDesde(i.IdEquipo, DateTime.Now.AddYears(-2))
            .Where(x => x.Id != idIncidencia && !string.IsNullOrWhiteSpace(x.Solucion))
            .OrderByDescending(x => x.FechaResolucion ?? x.Fecha)
            .Take(5)
            .Select(x => new AntecedenteEquipo(x.Numero, x.Titulo, x.Diagnostico, x.Solucion))
            .ToList();

        var ctx = new ContextoReparacion
        {
            TituloIncidencia = i.Titulo,
            DescripcionIncidencia = i.Descripcion,
            Categoria = i.Categoria?.Nombre,
            Prioridad = i.Prioridad?.Nombre,
            CodigoEquipo = equipo?.Codigo ?? "—",
            TipoEquipo = equipo?.Tipo?.Nombre,
            // La descripcion tecnica es texto libre y suele traer el sistema
            // operativo y la configuracion: es lo mas util que hay para el
            // proveedor sin agregarle campos al equipo.
            SistemaOperativo = equipo?.DescripcionTecnica,
            AntiguedadMeses = equipo?.FechaAdquisicion is { } compra
                ? (int)Math.Round((DateTime.Now - compra.ToDateTime(TimeOnly.MinValue)).TotalDays / 30.4)
                : null,
            Antecedentes = antecedentes,
        };

        var origen = OrigenDe(proveedorIa);
        var resultado = proveedorIa.SugerirReparacionAsync(ctx).GetAwaiter().GetResult();

        bitacora.Registrar(
            origen == "IA" ? TipoEvento.Codigos.GuiaReparacionIa
                           : TipoEvento.Codigos.GuiaReparacionHeuristica,
            $"Guía de reparación consultada para la incidencia #{i.Numero} " +
            $"({(resultado.Ok ? "respondió" : resultado.Falla?.ToString() ?? "sin respuesta")}).",
            contexto.IdUsuario, contexto.IdOrganizacion, "Incidencia", idIncidencia);

        if (!resultado.Ok || resultado.Valor is not { } guia)
        {
            // Sin guía se dice por qué. Una lista vacía dejaría al técnico
            // pensando que el sistema no tiene nada para sugerir, que es
            // distinto de que el proveedor no haya contestado.
            return new GuiaReparacionDto(false, null, [], null, null, origen,
                                         resultado.Detalle ?? "El proveedor no respondió.");
        }

        return new GuiaReparacionDto(
            true,
            guia.Resumen,
            guia.Pasos.Select(p => new PasoReparacionDto(p.Orden, p.Titulo, p.Detalle, p.Riesgo)).ToList(),
            guia.CuandoEscalar,
            guia.Confianza,
            origen,
            null);
    }

    // ------------------------------------------------------------- privados

    /// <summary>
    /// Clasifica el texto libre. Devuelve null si el proveedor no respondió: el
    /// triage es una ayuda, y su ausencia no impide registrar la incidencia.
    /// </summary>
    private ClasificacionIncidencia? Clasificar(
        string titulo,
        string descripcion,
        Guid idEquipo,
        IList<CategoriaIncidencia> categorias,
        IList<PrioridadIncidencia> prioridades)
    {
        var abiertas = dao.Incidencias.AbiertasDeEquipo(idEquipo);

        var ctx = new ContextoTriage
        {
            Titulo = titulo,
            Descripcion = descripcion,
            CategoriasPosibles = categorias.Select(c => c.Nombre).ToList(),
            PrioridadesPosibles = prioridades.Select(p => p.Nombre).ToList(),
            AbiertasDelEquipo = abiertas
                .Select(i => new IncidenciaAbierta(i.Id, i.Numero, i.Titulo)).ToList()
        };

        var resultado = proveedorIa.ClasificarAsync(ctx).GetAwaiter().GetResult();
        if (!resultado.Ok || resultado.Valor is not { } c) return null;

        var duplicada = c.DuplicadaDe is { } numero
            ? abiertas.FirstOrDefault(i => i.Numero == numero)?.Id
            : null;

        return new ClasificacionIncidencia
        {
            IdCategoriaSugerida = PorNombre(categorias, c.Categoria)?.Id,
            IdPrioridadSugerida = prioridades
                .FirstOrDefault(p => string.Equals(p.Nombre, c.Prioridad, StringComparison.OrdinalIgnoreCase))?.Id,
            IdIncidenciaDuplicada = duplicada,
            Confianza = c.Confianza,
            Origen = OrigenDe(proveedorIa),
            Justificacion = c.Justificacion
        };
    }

    /// <summary>
    /// Consulta al proveedor y asigna. Ante cualquier fallo aplica la estrategia
    /// de respaldo de la organización y deja la incidencia marcada para revisión.
    /// </summary>
    private RecomendacionAsignacion? Asignar(
        Incidencia incidencia,
        Equipo equipo,
        Guid? idCategoria,
        IList<CategoriaIncidencia> categorias,
        IList<PrioridadIncidencia> prioridades)
    {
        var configuracion = dao.ConfiguracionAsignacion.DeLaOrganizacion()
                            ?? new ConfiguracionAsignacion();

        var candidatos = dao.Tecnicos.CandidatosPara(equipo.Id);

        if (candidatos.Count == 0)
        {
            // Sin técnicos no hay nada que decidir, ni de la IA ni del respaldo.
            bitacora.Registrar(TipoEvento.Codigos.IncidenciaSinAsignar,
                $"Incidencia #{incidencia.Numero}: la organización no tiene técnicos activos.");
            return null;
        }

        var estadoAsignada = dao.Catalogos.EstadoIncidenciaAsignada()?.Id;

        var categoria = idCategoria is { } id ? categorias.FirstOrDefault(c => c.Id == id) : null;
        var especialidad = categoria?.IdEspecialidad is { } idEsp
            ? dao.Catalogos.Especialidades().FirstOrDefault(e => e.Id == idEsp)?.Nombre
            : null;

        var ctx = new ContextoAsignacion
        {
            TituloIncidencia = incidencia.Titulo,
            DescripcionIncidencia = incidencia.Descripcion,
            Categoria = categoria?.Nombre,
            EspecialidadRequerida = especialidad,
            Prioridad = prioridades.FirstOrDefault(p => p.Id == incidencia.IdPrioridad)?.Nombre,
            CodigoEquipo = equipo.Codigo,
            TipoEquipo = equipo.Tipo?.Nombre,
            UbicacionEquipo = equipo.Ubicacion?.Nombre,
            // Lo que la organización eligió no enviar, no viaja. Son datos de su
            // gente y la decisión es suya, no del sistema.
            Candidatos = candidatos.Select(t => new TecnicoCandidato
            {
                Id = t.IdUsuario,
                NombreCompleto = t.NombreCompleto,
                Especialidades = configuracion.EnviarEspecialidad
                    ? t.Especialidades
                        .Select(e => new EspecialidadNivel(e.Especialidad?.Nombre ?? string.Empty, e.Nivel))
                        .ToList()
                    : [],
                IncidenciasAbiertas = configuracion.EnviarCarga ? t.IncidenciasAbiertas : null,
                ResueltasEnEsteEquipo = configuracion.EnviarHistorial ? t.ResueltasEnEsteEquipo : null
            }).ToList()
        };

        var contextoEnviado = JsonSerializer.Serialize(ctx, JsonCompacto);
        var resultado = proveedorIa.RecomendarTecnicoAsync(ctx).GetAwaiter().GetResult();

        RecomendacionAsignacion recomendacion;

        if (resultado.Ok && resultado.Valor?.IdTecnico is { } idTecnico)
        {
            dao.ConfiguracionAsignacion.RegistrarResultado(ok: true, error: null);

            recomendacion = new RecomendacionAsignacion
            {
                IdIncidencia = incidencia.Id,
                IdTecnicoSugerido = idTecnico,
                Justificacion = resultado.Valor.Justificacion,
                ContextoEnviado = contextoEnviado,
                // El origen real, no «IA» fijo: cuando resuelve el proveedor
                // simulado la decisión no la tomó un modelo, y decir que sí
                // falsearía la trazabilidad de la asignación.
                Origen = OrigenDe(proveedorIa)
            };

            var tipo = OrigenDe(proveedorIa) == "IA"
                ? TipoAsignacion.Ia
                : TipoAsignacion.Heuristica;

            // Una incidencia con técnico ya no está «pendiente de asignación».
            // Sin este paso la pantalla mostraba a todas en el estado inicial
            // aunque tuvieran responsable, y el estado dejaba de significar algo.
            dao.Incidencias.Asignar(
                incidencia.Id, idTecnico, tipo, pendienteRevision: false, idEstado: estadoAsignada);

            // Hasta acá la bitácora sólo registraba el camino de respaldo, es
            // decir el que falla. Registrar también el que funciona es lo que
            // hace auditable la asignación automática (RF-08).
            bitacora.Registrar(
                tipo == TipoAsignacion.Ia
                    ? TipoEvento.Codigos.AsignacionIa
                    : TipoEvento.Codigos.AsignacionHeuristica,
                $"Incidencia #{incidencia.Numero} asignada automáticamente " +
                $"({OrigenDe(proveedorIa)}): {resultado.Valor.Justificacion}",
                contexto.IdUsuario, contexto.IdOrganizacion, "Incidencia", incidencia.Id);
        }
        else
        {
            var motivo = $"{resultado.Falla}: {resultado.Detalle}";
            dao.ConfiguracionAsignacion.RegistrarResultado(ok: false, error: motivo);

            var respaldo = AplicarRespaldo(configuracion, candidatos);

            recomendacion = new RecomendacionAsignacion
            {
                IdIncidencia = incidencia.Id,
                IdTecnicoSugerido = respaldo?.IdUsuario,
                Justificacion = respaldo is null
                    ? "Sin asignar por configuración de respaldo."
                    : $"Asignada por regla de respaldo a {respaldo.NombreCompleto} " +
                      $"({respaldo.IncidenciasAbiertas} incidencias abiertas).",
                ContextoEnviado = contextoEnviado,
                Origen = "RESPALDO",
                MotivoRespaldo = motivo
            };

            // Queda marcada para revisión: el respaldo asigna por carga, sin
            // mirar la especialidad, así que hace falta que un humano confirme.
            dao.Incidencias.Asignar(
                incidencia.Id, respaldo?.IdUsuario, TipoAsignacion.Respaldo, pendienteRevision: true,
                // Si el respaldo no asignó a nadie, la incidencia sigue
                // pendiente de asignación: no se la puede mover a «asignada».
                idEstado: respaldo is null ? null : estadoAsignada);

            bitacora.Registrar(TipoEvento.Codigos.AsignacionRespaldo,
                $"Incidencia #{incidencia.Numero}: el proveedor de IA no respondió ({motivo}). " +
                (respaldo is null
                    ? "Quedó sin asignar y marcada para revisión."
                    : $"Asignada por respaldo a {respaldo.NombreCompleto} y marcada para revisión."));
        }

        dao.Incidencias.GuardarRecomendacion(recomendacion);
        return recomendacion;
    }

    /// <summary>Estrategia de respaldo configurada por la organización (ADR 0006).</summary>
    private static TecnicoConCarga? AplicarRespaldo(
        ConfiguracionAsignacion configuracion, IList<TecnicoConCarga> candidatos) =>
        configuracion.EstrategiaRespaldo == "SIN_ASIGNAR"
            ? null
            : candidatos
                .OrderBy(c => c.IncidenciasAbiertas)
                // Desempate estable: sin esto, dos técnicos con la misma carga
                // se alternarían según el orden de la base.
                .ThenBy(c => c.IdUsuario)
                .First();

    private static string? AdvertenciaDe(RecomendacionAsignacion? recomendacion) =>
        recomendacion?.Origen != "RESPALDO"
            ? null
            : recomendacion.IdTecnicoSugerido is null
                ? "El servicio de asignación automática no respondió. La incidencia quedó " +
                  "registrada y sin asignar, marcada para que la revise un responsable."
                : "El servicio de asignación automática no respondió. Se asignó al técnico con " +
                  "menos carga y la incidencia quedó marcada para revisión.";

    /// <summary>
    /// Origen de una sugerencia. El proveedor simulado resuelve por heurísticas,
    /// así que decir «IA» sería falso: la trazabilidad tiene que distinguirlos.
    /// </summary>
    private static string OrigenDe(IProveedorIA proveedor) =>
        proveedor.Nombre.StartsWith("Simulado", StringComparison.OrdinalIgnoreCase)
            ? "HEURISTICA"
            : "IA";

    private static CategoriaIncidencia? PorNombre(IList<CategoriaIncidencia> categorias, string? nombre) =>
        nombre is null
            ? null
            : categorias.FirstOrDefault(c => string.Equals(c.Nombre, nombre, StringComparison.OrdinalIgnoreCase));

    private static void ValidarPertenece(
        Guid? valor, IEnumerable<Guid> permitidos, string campo, string mensaje)
    {
        if (valor is { } v && !permitidos.Contains(v)) throw BusinessException.De(campo, mensaje);
    }

    private AsignacionDto ADto(RecomendacionAsignacion r, IDictionary<Guid, string> nombres) =>
        new(r.Origen, r.IdTecnicoSugerido, NombreDe(nombres, r.IdTecnicoSugerido),
            r.Justificacion, r.MotivoRespaldo, r.FechaHora);

    private ClasificacionDto ADto(ClasificacionIncidencia c)
    {
        var categoria = c.IdCategoriaSugerida is { } idc
            ? dao.Catalogos.Categorias().FirstOrDefault(x => x.Id == idc)?.Nombre
            : null;

        var prioridad = c.IdPrioridadSugerida is { } idp
            ? dao.Catalogos.Prioridades().FirstOrDefault(x => x.Id == idp)?.Nombre
            : null;

        var numeroDuplicada = c.IdIncidenciaDuplicada is { } idd
            ? dao.Incidencias.GetById(idd)?.Numero
            : null;

        return new ClasificacionDto(
            c.Origen, c.IdCategoriaSugerida, categoria,
            c.IdPrioridadSugerida, prioridad,
            numeroDuplicada, c.Confianza, c.Justificacion);
    }

    private IDictionary<Guid, string> MapaDeUsuarios() =>
        seguridad.Usuarios
            .PorOrganizacion(contexto.IdOrganizacion)
            .ToDictionary(u => u.Id, u => u.NombreCompleto);

    private static string? NombreDe(IDictionary<Guid, string> mapa, Guid? id) =>
        id is { } valor && mapa.TryGetValue(valor, out var nombre) ? nombre : null;

    private void Exigir(string patente)
    {
        if (!contexto.Tiene(patente)) throw new PermisoDenegadoException(patente);
    }

    /// <summary>
    /// Alcanza con tener una. El modelo de permisos tiene pares donde ninguna
    /// implica a la otra —el administrador tiene VER_TODAS y no VER_PROPIAS—,
    /// así que exigir la de «propias» como mínimo lo dejaría afuera.
    /// </summary>
    private void ExigirAlguna(params string[] patentes)
    {
        if (!patentes.Any(contexto.Tiene))
            throw new PermisoDenegadoException(string.Join(" o ", patentes));
    }
}
