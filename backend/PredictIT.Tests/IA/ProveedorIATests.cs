using FluentAssertions;
using PredictIT.Service.IA;
using Xunit;

namespace PredictIT.Tests.IA;

/// <summary>
/// Proveedor simulado y el decorador con disyuntor (ADR 0006 y 0009 · CP-07, CP-14).
/// </summary>
public class ProveedorIATests
{
    private static readonly Guid Ana = Guid.Parse("B0000000-0000-0000-0000-000000000004");
    private static readonly Guid Diego = Guid.Parse("B0000000-0000-0000-0000-000000000005");

    private static readonly string[] Categorias =
        ["Hardware", "Software", "Red", "Periféricos", "Energía", "Rendimiento", "Sin clasificar"];

    private static readonly string[] Prioridades = ["Baja", "Media", "Alta", "Crítica"];

    private static TecnicoCandidato Tecnico(
        Guid id,
        string nombre,
        (string Nombre, int Nivel)[]? especialidades = null,
        int abiertas = 0,
        int enEsteEquipo = 0) => new()
        {
            Id = id,
            NombreCompleto = nombre,
            Especialidades = (especialidades ?? [])
                .Select(e => new EspecialidadNivel(e.Nombre, e.Nivel)).ToList(),
            IncidenciasAbiertas = abiertas,
            ResueltasEnEsteEquipo = enEsteEquipo,
        };

    private static ContextoAsignacion Caso(
        string especialidad,
        params TecnicoCandidato[] candidatos) => new()
        {
            TituloIncidencia = "No hay internet en administración",
            DescripcionIncidencia = "Desde la mañana no se puede navegar.",
            Categoria = "Red",
            EspecialidadRequerida = especialidad,
            Prioridad = "Alta",
            CodigoEquipo = "PC-ADM-014",
            TipoEquipo = "PC de escritorio",
            UbicacionEquipo = "Administración",
            Candidatos = candidatos,
        };

    private static ContextoTriage Triage(
        string titulo,
        string descripcion,
        params IncidenciaAbierta[] abiertas) => new()
        {
            Titulo = titulo,
            Descripcion = descripcion,
            CategoriasPosibles = Categorias,
            PrioridadesPosibles = Prioridades,
            AbiertasDelEquipo = abiertas,
        };

    // ------------------------------------------------------------- asignación

    [Fact]
    public async Task La_especialidad_manda_sobre_la_carga()
    {
        // Es mejor el especialista de redes con tres tickets que alguien libre
        // que no sabe de redes.
        var especialista = Tecnico(Ana, "Ana", [("Redes", 3)], abiertas: 3);
        var libre = Tecnico(Diego, "Diego", [("Impresión", 2)], abiertas: 0);

        var r = await new ProveedorIASimulado()
            .RecomendarTecnicoAsync(Caso("Redes", especialista, libre));

        r.Ok.Should().BeTrue();
        r.Valor!.IdTecnico.Should().Be(Ana);
        r.Valor.Justificacion.Should().Contain("Redes");
    }

    [Fact]
    public async Task Entre_dos_especialistas_gana_el_de_menor_carga()
    {
        var cargado = Tecnico(Ana, "Ana", [("Redes", 2)], abiertas: 6);
        var liviano = Tecnico(Diego, "Diego", [("Redes", 2)], abiertas: 1);

        var r = await new ProveedorIASimulado()
            .RecomendarTecnicoAsync(Caso("Redes", cargado, liviano));

        r.Valor!.IdTecnico.Should().Be(Diego);
    }

    [Fact]
    public async Task Es_deterministico_ante_el_mismo_contexto()
    {
        // Sin esto no habría caso de prueba posible, y la asignación no se podría
        // auditar: el mismo caso tiene que dar siempre el mismo técnico.
        var caso = Caso("Redes",
            Tecnico(Ana, "Ana", [("Redes", 2)], abiertas: 2),
            Tecnico(Diego, "Diego", [("Redes", 2)], abiertas: 2));

        var proveedor = new ProveedorIASimulado();
        var primera = await proveedor.RecomendarTecnicoAsync(caso);
        var segunda = await proveedor.RecomendarTecnicoAsync(caso);
        var tercera = await new ProveedorIASimulado().RecomendarTecnicoAsync(caso);

        primera.Valor!.IdTecnico.Should().Be(segunda.Valor!.IdTecnico);
        primera.Valor.IdTecnico.Should().Be(tercera.Valor!.IdTecnico);
    }

    [Fact]
    public async Task Sin_candidatos_falla_en_lugar_de_devolver_un_id_vacio()
    {
        var r = await new ProveedorIASimulado().RecomendarTecnicoAsync(Caso("Redes"));

        r.Ok.Should().BeFalse();
        r.Falla.Should().Be(MotivoFalla.TecnicoInexistente);
    }

    // ---------------------------------------------------------------- triage

    [Theory]
    [InlineData("No tengo internet", "No puedo navegar, el wifi no conecta", "Red")]
    [InlineData("La impresora no imprime", "Manda el trabajo y no sale nada", "Periféricos")]
    [InlineData("La PC no prende", "Aprieto el botón y no hace nada", "Hardware")]
    [InlineData("Excel tira error", "Al abrir el programa aparece un error", "Software")]
    [InlineData("Todo lentísimo", "La máquina tarda mucho y se cuelga", "Rendimiento")]
    public async Task Clasifica_por_los_terminos_del_reporte(
        string titulo, string descripcion, string esperada)
    {
        var r = await new ProveedorIASimulado().ClasificarAsync(Triage(titulo, descripcion));

        r.Ok.Should().BeTrue();
        r.Valor!.Categoria.Should().Be(esperada);
    }

    [Fact]
    public async Task No_inventa_categoria_cuando_el_texto_no_alcanza()
    {
        // Preferir null a adivinar: una categoría equivocada manda la incidencia
        // a la especialidad equivocada.
        var r = await new ProveedorIASimulado().ClasificarAsync(
            Triage("Consulta", "Necesito que alguien pase por mi escritorio."));

        r.Ok.Should().BeTrue();
        r.Valor!.Categoria.Should().BeNull();
        r.Valor.Confianza.Should().BeLessThan(0.5m);
    }

    [Fact]
    public async Task Reconoce_la_urgencia_de_lo_que_frena_a_toda_la_oficina()
    {
        var r = await new ProveedorIASimulado().ClasificarAsync(
            Triage("Se cayó el servidor", "No puedo trabajar, está toda la oficina parada."));

        r.Valor!.Prioridad.Should().Be("Crítica");
    }

    [Fact]
    public async Task Detecta_el_reporte_repetido_del_mismo_equipo()
    {
        var r = await new ProveedorIASimulado().ClasificarAsync(
            Triage(
                "La impresora no imprime nada",
                "Sigue sin imprimir.",
                new IncidenciaAbierta(Guid.NewGuid(), 41, "La impresora no imprime")));

        r.Valor!.DuplicadaDe.Should().Be(41);
    }

    [Fact]
    public async Task No_marca_como_duplicado_un_problema_distinto_del_mismo_equipo()
    {
        // Marcar duplicado un reporte nuevo hace que se pierda, que es peor que
        // no marcarlo. El umbral es exigente a propósito.
        var r = await new ProveedorIASimulado().ClasificarAsync(
            Triage(
                "No arranca el sistema operativo",
                "Queda en la pantalla negra.",
                new IncidenciaAbierta(Guid.NewGuid(), 41, "La impresora no imprime")));

        r.Valor!.DuplicadaDe.Should().BeNull();
    }

    [Fact]
    public async Task Ignora_los_acentos_y_las_mayusculas()
    {
        var con = await new ProveedorIASimulado().ClasificarAsync(
            Triage("Sin CONEXIÓN", "No hay conexión de RED"));
        var sin = await new ProveedorIASimulado().ClasificarAsync(
            Triage("sin conexion", "no hay conexion de red"));

        con.Valor!.Categoria.Should().Be(sin.Valor!.Categoria).And.Be("Red");
    }

    // --------------------------------------------------- CP-14: la IA se cae

    [Fact]
    public async Task CP14_con_el_proveedor_caido_la_consulta_falla_sin_lanzar()
    {
        // Que el servicio externo no responda es un caso previsto del negocio.
        // Si lanzara, el registro de la incidencia se caería con él.
        var caido = new ProveedorIACaido();

        var r = await caido.RecomendarTecnicoAsync(Caso("Redes", Tecnico(Ana, "Ana")));

        r.Ok.Should().BeFalse();
        r.Falla.Should().Be(MotivoFalla.Timeout);
        r.Detalle.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CP14_tras_cinco_fallos_el_disyuntor_deja_de_intentar()
    {
        // El punto del ADR 0009: sin disyuntor, la incidencia número veinte paga
        // el timeout completo igual que la primera.
        var caido = new ProveedorIACaido();
        var avisos = new List<string>();
        var proveedor = new ProveedorIAConDisyuntor(
            caido, new Disyuntor(fallosParaAbrir: 5), avisos.Add);

        var caso = Caso("Redes", Tecnico(Ana, "Ana"));

        for (var i = 0; i < 5; i++) await proveedor.RecomendarTecnicoAsync(caso);

        caido.Intentos.Should().Be(5);
        proveedor.Disyuntor.Estado.Should().Be(EstadoDisyuntor.Abierto);

        var siguiente = await proveedor.RecomendarTecnicoAsync(caso);

        siguiente.Falla.Should().Be(MotivoFalla.CircuitoAbierto);
        caido.Intentos.Should().Be(5, "con el circuito abierto no se vuelve a llamar al proveedor");
    }

    [Fact]
    public async Task CP14_la_apertura_del_circuito_se_avisa_una_sola_vez()
    {
        var avisos = new List<string>();
        var proveedor = new ProveedorIAConDisyuntor(
            new ProveedorIACaido(), new Disyuntor(fallosParaAbrir: 3), avisos.Add);

        var caso = Caso("Redes", Tecnico(Ana, "Ana"));
        for (var i = 0; i < 8; i++) await proveedor.RecomendarTecnicoAsync(caso);

        avisos.Should().HaveCount(1);
        avisos[0].Should().Contain("Circuito abierto");
    }

    [Fact]
    public async Task El_disyuntor_cuenta_tambien_al_proveedor_que_lanza()
    {
        // Un proveedor que lanza en lugar de devolver el fallo no debe saltearse
        // el disyuntor: si no, nunca abre y se pierde la protección.
        var proveedor = new ProveedorIAConDisyuntor(
            new ProveedorIAQueLanza(), new Disyuntor(fallosParaAbrir: 2));

        var caso = Caso("Redes", Tecnico(Ana, "Ana"));

        var primera = await proveedor.RecomendarTecnicoAsync(caso);
        var segunda = await proveedor.RecomendarTecnicoAsync(caso);

        primera.Falla.Should().Be(MotivoFalla.Error);
        segunda.Falla.Should().Be(MotivoFalla.Error);
        proveedor.Disyuntor.Estado.Should().Be(EstadoDisyuntor.Abierto);
    }

    [Fact]
    public async Task Un_exito_despues_de_fallos_mantiene_el_circuito_cerrado()
    {
        var proveedor = new ProveedorIAConDisyuntor(
            new ProveedorIAAlternante(), new Disyuntor(fallosParaAbrir: 3));

        var caso = Caso("Redes", Tecnico(Ana, "Ana", [("Redes", 2)]));

        // Falla, anda, falla, anda…: molesto, pero no es un servicio caído.
        for (var i = 0; i < 10; i++) await proveedor.RecomendarTecnicoAsync(caso);

        proveedor.Disyuntor.Estado.Should().Be(EstadoDisyuntor.Cerrado);
    }

    /// <summary>Doble que lanza en lugar de devolver el fallo.</summary>
    private sealed class ProveedorIAQueLanza : IProveedorIA
    {
        public string Nombre => "Que lanza";

        public Task<ResultadoIA<ClasificacionIA>> ClasificarAsync(
            ContextoTriage contexto, CancellationToken ct = default) =>
            throw new HttpRequestException("conexión rechazada");

        public Task<ResultadoIA<RecomendacionIA>> RecomendarTecnicoAsync(
            ContextoAsignacion contexto, CancellationToken ct = default) =>
            throw new HttpRequestException("conexión rechazada");

        public Task<ResultadoIA<GuiaReparacionIA>> SugerirReparacionAsync(
            ContextoReparacion contexto, CancellationToken ct = default) =>
            throw new HttpRequestException("conexión rechazada");
    }

    /// <summary>Doble que falla una vez de cada dos.</summary>
    private sealed class ProveedorIAAlternante : IProveedorIA
    {
        private int _n;
        private readonly ProveedorIASimulado _bueno = new();

        public string Nombre => "Alternante";

        public Task<ResultadoIA<ClasificacionIA>> ClasificarAsync(
            ContextoTriage contexto, CancellationToken ct = default) =>
            _n++ % 2 == 0
                ? Task.FromResult(ResultadoIA<ClasificacionIA>.Mal(MotivoFalla.Timeout, "no anduvo"))
                : _bueno.ClasificarAsync(contexto, ct);

        public Task<ResultadoIA<RecomendacionIA>> RecomendarTecnicoAsync(
            ContextoAsignacion contexto, CancellationToken ct = default) =>
            _n++ % 2 == 0
                ? Task.FromResult(ResultadoIA<RecomendacionIA>.Mal(MotivoFalla.Timeout, "no anduvo"))
                : _bueno.RecomendarTecnicoAsync(contexto, ct);

        public Task<ResultadoIA<GuiaReparacionIA>> SugerirReparacionAsync(
            ContextoReparacion contexto, CancellationToken ct = default) =>
            _n++ % 2 == 0
                ? Task.FromResult(ResultadoIA<GuiaReparacionIA>.Mal(MotivoFalla.Timeout, "no anduvo"))
                : _bueno.SugerirReparacionAsync(contexto, ct);
    }
}
