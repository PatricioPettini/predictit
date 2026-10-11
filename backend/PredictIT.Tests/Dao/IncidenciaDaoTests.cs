using FluentAssertions;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Factory;
using PredictIT.Domain.Negocio;
using PredictIT.Tests.Infraestructura;

namespace PredictIT.Tests.Dao;

/// <summary>
/// Acceso a incidencias, técnicos y análisis predictivo, contra la base real.
///
/// Van contra SQL Server y no contra dobles: los defectos que aparecieron en
/// esta capa —una palabra reservada, un largo de columna, un join que excluía a
/// todos los técnicos— eran todos invisibles a la lectura del código y sólo se
/// manifestaron al ejecutar.
/// </summary>
[Trait("Categoria", "Integracion")]
public class IncidenciaDaoTests
{
    private static IFactoryDao Como(Guid organizacion) =>
        new FactoryDao(Datos.Conexiones(), ContextoDePrueba.TodoPermitido(organizacion));

    // ------------------------------------------- aislamiento entre organizaciones

    [Fact]
    public void El_listado_de_incidencias_solo_devuelve_las_de_la_organizacion()
    {
        var delEstudio = Como(Datos.Estudio).Incidencias.GetAll();
        var deLaClinica = Como(Datos.Clinica).Incidencias.GetAll();

        delEstudio.Should().OnlyContain(i => i.IdOrganizacion == Datos.Estudio);
        deLaClinica.Should().OnlyContain(i => i.IdOrganizacion == Datos.Clinica);
    }

    [Fact]
    public void No_se_puede_leer_por_id_una_incidencia_de_otra_organizacion()
    {
        var delEstudio = Como(Datos.Estudio).Incidencias.GetAll().FirstOrDefault();
        delEstudio.Should().NotBeNull("el seed carga incidencias del Estudio");

        Como(Datos.Clinica).Incidencias.GetById(delEstudio!.Id).Should().BeNull();
    }

    [Fact]
    public void La_clasificacion_de_una_incidencia_ajena_no_se_puede_leer()
    {
        // ClasificacionIncidencia no tiene id_organizacion: el aislamiento
        // depende del join contra Incidencia. Si ese join se cayera, alcanzaría
        // con el identificador para leer datos de otro cliente.
        var dao = Como(Datos.Estudio).Incidencias;
        var incidencia = dao.GetAll().First();

        dao.GuardarClasificacion(new ClasificacionIncidencia
        {
            IdIncidencia = incidencia.Id,
            Origen = "HEURISTICA",
            Justificacion = "Prueba de aislamiento."
        });

        try
        {
            Como(Datos.Estudio).Incidencias.ClasificacionDe(incidencia.Id).Should().NotBeNull();
            Como(Datos.Clinica).Incidencias.ClasificacionDe(incidencia.Id).Should().BeNull();
        }
        finally
        {
            Datos.LimpiarResiduosDePrueba();
        }
    }

    [Fact]
    public void La_recomendacion_de_una_incidencia_ajena_no_se_puede_leer()
    {
        var dao = Como(Datos.Estudio).Incidencias;
        var incidencia = dao.GetAll().First();

        dao.GuardarRecomendacion(new RecomendacionAsignacion
        {
            IdIncidencia = incidencia.Id,
            Origen = "HEURISTICA",
            Justificacion = "Prueba de aislamiento."
        });

        try
        {
            Como(Datos.Estudio).Incidencias.RecomendacionDe(incidencia.Id).Should().NotBeNull();
            Como(Datos.Clinica).Incidencias.RecomendacionDe(incidencia.Id).Should().BeNull();
        }
        finally
        {
            Datos.LimpiarResiduosDePrueba();
        }
    }

    [Fact]
    public void Las_reglas_de_alerta_estan_aisladas_por_organizacion()
    {
        var delEstudio = Como(Datos.Estudio).Prediccion.ReglasDeLaOrganizacion();
        var deLaClinica = Como(Datos.Clinica).Prediccion.ReglasDeLaOrganizacion();

        delEstudio.Should().NotBeEmpty().And.OnlyContain(r => r.IdOrganizacion == Datos.Estudio);
        deLaClinica.Should().NotBeEmpty().And.OnlyContain(r => r.IdOrganizacion == Datos.Clinica);

        Como(Datos.Clinica).Prediccion.ReglaPorId(delEstudio.First().Id).Should().BeNull();
    }

    // ------------------------------------------------------ correlativo y alta

    [Fact]
    public void El_correlativo_es_por_organizacion_y_no_global()
    {
        // El número es lo que el cliente cita por teléfono: tiene que empezar en
        // 1 para cada organización, no continuar la numeración de otra.
        var proximoEstudio = Como(Datos.Estudio).Incidencias.ProximoNumero();
        var proximoClinica = Como(Datos.Clinica).Incidencias.ProximoNumero();

        proximoEstudio.Should().BeGreaterThan(0);
        proximoClinica.Should().BeGreaterThan(0);

        var maxEstudio = Como(Datos.Estudio).Incidencias.GetAll().Max(i => i.Numero);
        proximoEstudio.Should().Be(maxEstudio + 1);
    }

    [Fact]
    public void El_alta_fuerza_la_organizacion_del_contexto()
    {
        // Aunque la entidad venga con otra organización, se guarda con la de la
        // sesión: si no, alcanzaría con mandar otro identificador para escribir
        // en los datos de otro cliente.
        var dao = Como(Datos.Estudio);
        var catalogos = dao.Catalogos;

        var incidencia = new Incidencia
        {
            IdOrganizacion = Datos.Clinica,   // se ignora a propósito
            Titulo = "Prueba de alta desde el DAO",
            Descripcion = "Verifica que la organización se tome del contexto de sesión.",
            IdEquipo = Datos.PcAdm014,
            IdEstado = catalogos.EstadoIncidenciaInicial()!.Id,
            IdPrioridad = catalogos.Prioridades().First().Id,
            IdUsuarioReportante = Datos.Solicitante,
            Fecha = DateTime.Now
        };

        var id = dao.Incidencias.Insert(incidencia);

        try
        {
            Como(Datos.Estudio).Incidencias.GetById(id).Should().NotBeNull();
            Como(Datos.Clinica).Incidencias.GetById(id).Should().BeNull();
        }
        finally
        {
            // La prueba limpia lo que creó. La base de demostración es la misma
            // que se usa para mostrar el sistema, y sin esto cada corrida dejaba
            // una incidencia de prueba en el listado del solicitante.
            Datos.LimpiarResiduosDePrueba();
        }
    }

    [Fact]
    public void Las_incidencias_no_se_borran()
    {
        // Son el historial que alimenta el análisis predictivo. Borrarlas dejaría
        // al motor sin antecedentes justo del equipo que más falló.
        var dao = Como(Datos.Estudio).Incidencias;
        var accion = () => dao.Delete(dao.GetAll().First().Id);

        accion.Should().Throw<NotSupportedException>();
    }

    // ------------------------------------------------------------- técnicos

    [Fact]
    public void Los_candidatos_incluyen_a_los_tecnicos_internos()
    {
        // Este es el defecto que apareció al ejecutar: el join pasaba por
        // Usuario_Organizacion, que sólo tiene filas del Partner, y devolvía
        // cero técnicos con una consulta que se leía correcta.
        var candidatos = Como(Datos.Estudio).Tecnicos.CandidatosPara(Datos.PcAdm014);

        candidatos.Should().NotBeEmpty();
        candidatos.Should().Contain(c => c.IdUsuario == Datos.Tecnico1);
        candidatos.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c.NombreCompleto));
    }

    [Fact]
    public void Los_candidatos_traen_especialidades_y_carga()
    {
        var candidatos = Como(Datos.Estudio).Tecnicos.CandidatosPara(Datos.PcAdm014);

        candidatos.Should().Contain(c => c.Especialidades.Count > 0,
            "el seed carga especialidades para los técnicos del Estudio");
        candidatos.Should().OnlyContain(c => c.IncidenciasAbiertas >= 0);
    }

    [Fact]
    public void Sin_equipo_los_candidatos_no_traen_historial_pero_si_carga()
    {
        var candidatos = Como(Datos.Estudio).Tecnicos.CandidatosPara(idEquipo: null);

        candidatos.Should().NotBeEmpty();
        candidatos.Should().OnlyContain(c => c.ResueltasEnEsteEquipo == 0);
    }

    [Fact]
    public void Un_tecnico_de_otra_organizacion_no_es_candidato()
    {
        var delEstudio = Como(Datos.Estudio).Tecnicos.CandidatosPara(Datos.PcAdm014)
            .Select(c => c.IdUsuario).ToHashSet();

        var deLaClinica = Como(Datos.Clinica).Tecnicos.CandidatosPara(idEquipo: null)
            .Select(c => c.IdUsuario).ToHashSet();

        // El Partner atiende a las dos, así que puede aparecer en ambas listas.
        // Lo que no puede es que un técnico interno del Estudio sea candidato
        // para la Clínica.
        deLaClinica.Should().NotContain(Datos.Tecnico1);
        delEstudio.Should().Contain(Datos.Tecnico1);
    }

    // ------------------------------------------------- catálogos y predicción

    [Fact]
    public void Los_catalogos_nuevos_traen_las_columnas_que_el_motor_necesita()
    {
        // Las tres columnas se agregaron con una migración. Si no se hubiera
        // aplicado, esto falla acá y no en silencio dentro del motor.
        var catalogos = Como(Datos.Estudio).Catalogos;

        catalogos.TiposDeMantenimiento().Should().Contain(t => t.EsPreventivo);
        catalogos.TiposDeMantenimiento().Should().Contain(t => !t.EsPreventivo);
        catalogos.Categorias().Should().Contain(c => c.IdEspecialidad != null);
        catalogos.Prioridades().Should().Contain(p => p.HorasObjetivo != null);
        catalogos.Prioridades().Should().Contain(p => p.HorasObjetivo == null,
            "la prioridad más baja no tiene objetivo pactado");
    }

    [Fact]
    public void El_estado_inicial_de_incidencia_no_es_terminal()
    {
        var estado = Como(Datos.Estudio).Catalogos.EstadoIncidenciaInicial();

        estado.Should().NotBeNull();
        estado!.EsFinal.Should().BeFalse();
    }

    [Fact]
    public void El_estado_inicial_de_alerta_es_activa()
    {
        var estado = Como(Datos.Estudio).Catalogos.EstadoAlertaInicial();

        estado.Should().NotBeNull();
        estado!.EsFinal.Should().BeFalse();
        estado.Nombre.Should().Be("Activa");
    }

    [Fact]
    public void Una_evaluacion_de_riesgo_se_guarda_y_se_lee_de_vuelta()
    {
        var dao = Como(Datos.Estudio).Prediccion;

        dao.GuardarEvaluacion(new EvaluacionRiesgo
        {
            IdEquipo = Datos.PcAdm014,
            Fecha = DateTime.Now,
            Score = 73,
            Nivel = NivelRiesgo.Alto,
            Detalle = """[{"regla":"Prueba","intensidad":1}]"""
        });

        try
        {
            var ultima = dao.UltimaEvaluacion(Datos.PcAdm014);

            ultima.Should().NotBeNull();
            ultima!.Score.Should().Be(73);
            ultima.Nivel.Should().Be(NivelRiesgo.Alto);

            // Y no se ve desde la otra organización.
            Como(Datos.Clinica).Prediccion.UltimaEvaluacion(Datos.PcAdm014).Should().BeNull();
        }
        finally
        {
            Datos.LimpiarResiduosDePrueba();
        }
    }

    [Fact]
    public void Una_regla_se_guarda_con_la_organizacion_del_contexto()
    {
        var dao = Como(Datos.Estudio).Prediccion;

        // Identificador fijo, no uno nuevo por corrida: `GuardarRegla` hace MERGE,
        // así que con un GUID fijo la prueba sobreescribe su propia fila. Con
        // uno nuevo cada vez, la base de demostración acumulaba una regla por
        // ejecución de la suite y la pantalla de reglas terminaba mostrando
        // trece donde el seed carga cinco.
        var regla = new ReglaAlerta
        {
            Id = Datos.ReglaDePrueba,
            IdOrganizacion = Datos.Clinica,   // se ignora a propósito
            Nombre = "Regla de prueba (automatizada)",
            Tipo = TipoRegla.AntiguedadEquipo,
            Condicion = """{"aniosUmbral":7}""",
            Peso = 12,
            Activa = false
        };

        dao.GuardarRegla(regla);

        try
        {
            var guardada = Como(Datos.Estudio).Prediccion.ReglaPorId(regla.Id);
            guardada.Should().NotBeNull();
            guardada!.IdOrganizacion.Should().Be(Datos.Estudio);
            guardada.Peso.Should().Be(12);
            guardada.Activa.Should().BeFalse();
            guardada.Tipo.Should().Be(TipoRegla.AntiguedadEquipo);

            Como(Datos.Clinica).Prediccion.ReglaPorId(regla.Id).Should().BeNull();

            // Guardar de nuevo el mismo identificador modifica, no duplica.
            regla.Peso = 34;
            Como(Datos.Estudio).Prediccion.GuardarRegla(regla);
            Como(Datos.Estudio).Prediccion.ReglaPorId(regla.Id)!.Peso.Should().Be(34);
        }
        finally
        {
            Datos.LimpiarResiduosDePrueba();
        }
    }

    [Fact]
    public void Las_reglas_inactivas_solo_aparecen_cuando_se_piden()
    {
        var dao = Como(Datos.Estudio).Prediccion;

        var regla = new ReglaAlerta
        {
            Id = Datos.ReglaInactivaDePrueba,
            Nombre = "Inactiva de prueba (automatizada)",
            Tipo = TipoRegla.GarantiaPorVencer,
            Condicion = """{"diasAviso":30}""",
            Peso = 5,
            Activa = false
        };
        dao.GuardarRegla(regla);

        try
        {
            dao.ReglasDeLaOrganizacion(soloActivas: true)
                .Should().NotContain(r => r.Id == regla.Id);
            dao.ReglasDeLaOrganizacion(soloActivas: false)
                .Should().Contain(r => r.Id == regla.Id);
        }
        finally
        {
            Datos.LimpiarResiduosDePrueba();
        }
    }

    [Fact]
    public void La_configuracion_de_asignacion_esta_aislada_por_organizacion()
    {
        var delEstudio = Como(Datos.Estudio).ConfiguracionAsignacion.DeLaOrganizacion();

        delEstudio.Should().NotBeNull("el seed carga la configuración del Estudio");
        delEstudio!.IdOrganizacion.Should().Be(Datos.Estudio);
        delEstudio.ProveedorIa.Should().BeOneOf("SIMULADO", "CLAUDE");
    }
}
