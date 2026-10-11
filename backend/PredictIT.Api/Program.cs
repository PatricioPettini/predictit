using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PredictIT.Api.Infraestructura;
using PredictIT.BLL;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Factory;
using PredictIT.DAO.Helpers;
using PredictIT.Service.IA;
using PredictIT.Service.Prediccion;
using PredictIT.Service.Reportes;
using PredictIT.Service.Respaldos;
using PredictIT.Service.Seguridad;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- configuración

var negocio = ConCredencial(builder.Configuration.GetConnectionString("Negocio")
    ?? throw new InvalidOperationException("Falta ConnectionStrings:Negocio."));
var servicio = ConCredencial(builder.Configuration.GetConnectionString("Servicio")
    ?? throw new InvalidOperationException("Falta ConnectionStrings:Servicio."));

// La contraseña de la base no está en appsettings.json: sale del entorno.
//
// Un archivo de configuración versionado con la contraseña adentro la lleva a
// donde vaya el repositorio, y ahí ya no hay forma de rotarla. La cadena dice
// contra qué servidor y con qué usuario se conecta —eso no es secreto— y la
// contraseña se le agrega al arrancar. Si falta, la aplicación no arranca y
// dice cuál es la variable, en lugar de fallar después con «login failed».
static string ConCredencial(string cadena)
{
    if (cadena.Contains("Password=", StringComparison.OrdinalIgnoreCase))
        return cadena;

    var clave = Environment.GetEnvironmentVariable("PREDICTIT_DB_PASSWORD");
    if (string.IsNullOrWhiteSpace(clave))
        throw new InvalidOperationException(
            "Falta la contraseña de la base. Poné PREDICTIT_DB_PASSWORD en el "
            + "entorno (está en .env.ejemplo) o incluí Password= en la cadena "
            + "de conexión.");

    return cadena.TrimEnd(';') + ";Password=" + clave + ";";
}

builder.Services.AddSingleton(new ConexionesSql(negocio, servicio));

// La clave de firma no tiene valor por defecto a propósito: un default sería una
// clave conocida, y con una clave conocida cualquiera puede emitir un token válido.
var opcionesToken = new OpcionesToken
{
    Emisor = builder.Configuration["Jwt:Emisor"] ?? "PredictIT",
    Audiencia = builder.Configuration["Jwt:Audiencia"] ?? "PredictIT.Web",
    MinutosVigencia = builder.Configuration.GetValue("Jwt:MinutosVigencia", 480),
    Clave = builder.Configuration["Jwt:Clave"]
            ?? Environment.GetEnvironmentVariable("PREDICTIT_JWT_CLAVE")
            ?? throw new InvalidOperationException(
                "Falta la clave de firma del token. Configurá Jwt:Clave o PREDICTIT_JWT_CLAVE.")
};

// HS256 exige una clave de 256 bits. Con una más corta la API arranca igual y
// recién falla al emitir el primer token, con un error de la librería que no
// dice que el problema es la configuración.
if (Encoding.UTF8.GetByteCount(opcionesToken.Clave) < 32)
{
    throw new InvalidOperationException(
        "La clave de firma del token es demasiado corta: HS256 necesita al menos 32 bytes " +
        $"y la configurada tiene {Encoding.UTF8.GetByteCount(opcionesToken.Clave)}.");
}

builder.Services.AddSingleton(opcionesToken);

// ------------------------------------------------------------------- inyección

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<ILoggerService, LoggerService>();

// El contexto de sesión se resuelve por petición desde los claims del token.
// Antes de autenticar queda anónimo, que es justo lo que necesita el login.
builder.Services.AddScoped<IContextoSesion>(sp =>
{
    var accesor = sp.GetRequiredService<IHttpContextAccessor>();
    var usuario = accesor.HttpContext?.User;

    if (usuario?.Identity?.IsAuthenticated != true) return ContextoSesion.Anonimo();

    var idUsuario = Guid.TryParse(
        usuario.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? usuario.FindFirst("sub")?.Value,
        out var u) ? u : Guid.Empty;

    var idOrganizacion = Guid.TryParse(
        usuario.FindFirst(TokenService.ClaimOrganizacion)?.Value, out var o) ? o : Guid.Empty;

    if (idUsuario == Guid.Empty || idOrganizacion == Guid.Empty) return ContextoSesion.Anonimo();

    // Los permisos se leen de la base y no del token: si vinieran en el token,
    // revocar una patente no tendría efecto hasta que el token expirara.
    var seguridad = sp.GetRequiredService<ISeguridadService>();
    return seguridad.ReconstruirContexto(idUsuario, idOrganizacion) ?? ContextoSesion.Anonimo();
});

// La fábrica de seguridad va antes y no depende del contexto: es lo que rompe
// el ciclo contexto -> seguridad -> DAO -> contexto.
builder.Services.AddScoped<IFactoryDaoSeguridad>(sp =>
    new FactoryDaoSeguridad(sp.GetRequiredService<ConexionesSql>()));
builder.Services.AddScoped<IBitacoraService, BitacoraService>();
builder.Services.AddScoped<ISeguridadService, SeguridadService>();

builder.Services.AddScoped<IFactoryDao>(sp =>
    new FactoryDao(sp.GetRequiredService<ConexionesSql>(), sp.GetRequiredService<IContextoSesion>()));

// ------------------------------------------------- analisis predictivo e IA

// El motor es logica pura y sin estado: una sola instancia alcanza.
// Cifrado reversible (CU.Arq.006). La clave sale de configuracion o de la
// variable de entorno, nunca del codigo. Sin clave el servicio existe pero se
// niega a cifrar, en vez de guardar en claro sin avisar.
builder.Services.AddSingleton<ICifradoService>(_ => new CifradoService(
    builder.Configuration["Cifrado:Clave"]
    ?? Environment.GetEnvironmentVariable("PREDICTIT_CLAVE_CIFRADO")));

// El generador de PDF no tiene estado: una sola instancia alcanza. La licencia
// Community de QuestPDF se declara en su constructor estatico.
builder.Services.AddSingleton<IReporteService, ReporteService>();

builder.Services.AddSingleton(new MotorPredictivo());

// El registro de disyuntores es singleton porque el estado del circuito tiene
// que sobrevivir a la peticion; el disyuntor concreto es por organizacion
// (ADR 0009).
builder.Services.AddSingleton(new RegistroDisyuntores(
    fallosParaAbrir: builder.Configuration.GetValue("Ia:FallosParaAbrir", 5),
    esperaParaProbar: TimeSpan.FromSeconds(
        builder.Configuration.GetValue("Ia:SegundosParaProbar", 60))));

builder.Services.AddHttpClient(nameof(ProveedorIAClaude));

// El proveedor se resuelve por peticion porque la eleccion la hace cada
// organizacion en su configuracion. La clave sale de la variable de entorno y
// no de la columna api_key_cifrada: todavia no hay servicio de cifrado, y
// guardar una clave en claro en la base seria peor que no guardarla.
builder.Services.AddScoped<IProveedorIA>(sp =>
{
    var contexto = sp.GetRequiredService<IContextoSesion>();
    var registro = sp.GetRequiredService<RegistroDisyuntores>();
    var log = sp.GetRequiredService<ILoggerService>();

    // PREDICTIT_IA_FALLAR_CADA fuerza que el proveedor simulado falle una de
    // cada N llamadas. Es lo que hace demostrable el CP-14 y las transiciones
    // del disyuntor sobre el sistema corriendo, sin tocar codigo: con 1 falla
    // siempre. Sin la variable, nunca falla.
    var fallarCada = int.TryParse(
        Environment.GetEnvironmentVariable("PREDICTIT_IA_FALLAR_CADA"), out var cada) ? cada : 0;

    IProveedorIA interno = new ProveedorIASimulado(() => fallarCada);

    // Anonimo: no hay organizacion de la cual leer configuracion. Igual se
    // devuelve un proveedor valido para no obligar a los llamadores a
    // manejar el nulo.
    if (contexto.IdOrganizacion != Guid.Empty)
    {
        var configuracion = sp.GetRequiredService<IFactoryDao>()
            .ConfiguracionAsignacion.DeLaOrganizacion();

        // La clave de la organizacion, guardada cifrada, gana sobre la del
        // entorno: si el cliente cargo la suya, es la que quiso usar.
        var cifrado = sp.GetRequiredService<ICifradoService>();
        string? clave = null;

        if (cifrado.EsCifrado(configuracion?.ApiKeyCifrada))
        {
            try
            {
                clave = cifrado.Descifrar(configuracion!.ApiKeyCifrada!);
            }
            catch (InvalidOperationException ex)
            {
                // Pasa si rotaron la clave de cifrado sin volver a cargar las
                // credenciales. Se avisa y se cae al entorno: es mejor asignar
                // con heuristicas que no asignar.
                log.Error("No se pudo descifrar la clave de API de la organizacion.", ex);
            }
        }

        clave ??= Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

        if (configuracion?.ProveedorIa == "CLAUDE")
        {
            if (string.IsNullOrWhiteSpace(clave))
            {
                // Se avisa y se sigue con el simulado. Dejar el sistema sin
                // asignacion por falta de una clave seria peor que asignar con
                // heuristicas y decirlo.
                log.Advertencia(
                    "La organizacion tiene configurado el proveedor CLAUDE pero no hay " +
                    "ANTHROPIC_API_KEY: se usa el proveedor simulado.");
            }
            else
            {
                var http = sp.GetRequiredService<IHttpClientFactory>()
                    .CreateClient(nameof(ProveedorIAClaude));

                interno = new ProveedorIAClaude(http, new OpcionesClaude
                {
                    ApiKey = clave,
                    Modelo = configuracion.Modelo ?? "claude-haiku-4-5",
                    TimeoutSegundos = configuracion.TimeoutSegundos
                });
            }
        }
    }

    return new ProveedorIAConDisyuntor(
        interno,
        registro.Para(contexto.IdOrganizacion),
        aviso => log.Advertencia(aviso));
});

// El servicio de respaldos se conecta a `master`: BACKUP y RESTORE no se pueden
// ejecutar desde la base que se esta respaldando. La cadena se deriva de la de
// negocio cambiandole el catalogo, asi no hay una tercera credencial.
builder.Services.AddSingleton(sp =>
    new ConexionesMaestro(sp.GetRequiredService<ConexionesSql>().Negocio));

builder.Services.AddSingleton(new OpcionesRespaldo
{
    RutaContenedor = builder.Configuration["Respaldos:RutaContenedor"]
                     ?? "/var/opt/mssql/backups",
    TimeoutMinutos = builder.Configuration.GetValue("Respaldos:TimeoutMinutos", 10)
});

builder.Services.AddSingleton<IRespaldoService, RespaldoService>();

builder.Services.AddScoped<IFactoryBusiness, FactoryBusiness>();

// El barrido predictivo programado. Con Horas = 0 no corre, que es lo que
// quieren las pruebas de integracion: no necesitan un job compitiendo por la
// base mientras verifican.
builder.Services.AddSingleton(new OpcionesBarrido
{
    Intervalo = TimeSpan.FromHours(builder.Configuration.GetValue("Prediccion:HorasEntreBarridos", 24.0)),
    EsperaInicial = TimeSpan.FromMinutes(
        builder.Configuration.GetValue("Prediccion:MinutosAntesDelPrimero", 2.0))
});
builder.Services.AddHostedService<BarridoPredictivo>();

// --------------------------------------------------------------- autenticación

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opciones =>
    {
        opciones.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = opcionesToken.Emisor,
            ValidAudience = opcionesToken.Audiencia,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opcionesToken.Clave)),
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "unique_name"
        };
    });
builder.Services.AddAuthorization();

// ------------------------------------------------------- límite de intentos

// El bloqueo por usuario —cinco intentos fallidos y la cuenta queda cerrada—
// ya está, y no alcanza. Contra un ataque que prueba una contraseña común
// sobre muchos usuarios distintos nunca se juntan cinco intentos en la misma
// cuenta, así que el bloqueo no dispara jamás. El límite por origen es el que
// corta ese caso.
//
// Diez por minuto: un usuario que se equivoca y reintenta hace tres o cuatro,
// y un ataque hace cientos. La cola en cero es a propósito: encolar los
// intentos de más los ejecuta más tarde en lugar de rechazarlos, que es lo
// contrario de lo que se busca.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    o.AddPolicy(Politicas.IntentosDeLogin, contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            // Detrás de un proxy la dirección real viene en X-Forwarded-For.
            // Si se particionara por la del proxy, todos los usuarios caerían
            // en la misma partición y el límite los afectaría a todos juntos.
            partitionKey: DireccionDe(contexto),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("Seguridad:IntentosPorMinuto", 10),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Cuando rechaza, lo asienta. Un rechazo silencioso deja al administrador
    // sin forma de saber que alguien está probando contraseñas.
    o.OnRejected = async (contexto, _) =>
    {
        var registro = contexto.HttpContext.RequestServices
            .GetService<PredictIT.Service.Seguridad.ILoggerService>();

        registro?.Advertencia(
            $"Se rechazó un intento de autenticación por exceso de pedidos desde "
            + $"{DireccionDe(contexto.HttpContext)}.");

        // Y lo cuenta, que es lo que permite ver la tendencia. El aviso del
        // log dice que pasó una vez; la métrica dice si pasa todo el tiempo.
        PredictIT.Api.Infraestructura.Metricas.RechazosPorLimite
            .WithLabels(Politicas.IntentosDeLogin).Inc();

        contexto.HttpContext.Response.Headers.RetryAfter = "60";
        await contexto.HttpContext.Response.WriteAsJsonAsync(new
        {
            mensaje = "Demasiados intentos desde esta dirección. Probá de nuevo en un minuto."
        });
    };
});

static string DireccionDe(HttpContext contexto)
{
    var reenviada = contexto.Request.Headers["X-Forwarded-For"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(reenviada))
        return reenviada.Split(',')[0].Trim();

    return contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
}

// -------------------------------------------------------------------- frontend

const string PoliticaCors = "frontend";
builder.Services.AddCors(o => o.AddPolicy(PoliticaCors, p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origenes").Get<string[]>()
                 ?? ["http://localhost:5173"])
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();

/*
    Swagger con tres cosas que no vienen por omisión.

    1. Las descripciones salen de los comentarios `///` del código, por el XML
       que generan los dos proyectos. Sin esto la documentación de la API es una
       lista de rutas sin una línea que diga qué hace cada una.
    2. La portada dice qué es el sistema y con qué usuarios se prueba, porque
       quien abre Swagger no necesariamente tiene el LEEME al lado.
    3. El botón «Authorize», que es el que hace la diferencia entre mirar la API
       y probarla: casi todos los endpoints piden JWT, y sin esto hay que salir
       a buscar una herramienta aparte para mandar la cabecera.
*/
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "PredictIT",
        Version = "v1",
        Description =
            "Gestión y mantenimiento predictivo de equipos informáticos para PyMEs.\n\n"
            + "Trabajo Final de Ingeniería · Patricio Pettini · Universidad Abierta "
            + "Interamericana.\n\n"
            + "**Para probar:** entrar por `POST /api/auth/login` con "
            + "`admin / Admin.2026`, copiar el token de la respuesta y pegarlo en "
            + "«Authorize», arriba a la derecha. Los otros usuarios de demostración "
            + "son `tecnico1 / Tecnico.2026` y `solicitante / Usuario.2026`, y "
            + "muestran perfiles con distintos permisos: el mismo endpoint devuelve "
            + "200 o 403 según quién lo pida.",
    });

    foreach (var xml in Directory.GetFiles(AppContext.BaseDirectory, "PredictIT.*.xml"))
    {
        c.IncludeXmlComments(xml, includeControllerXmlComments: true);
    }

    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "El token que devuelve `POST /api/auth/login`. Va solo, sin "
                    + "escribirle «Bearer» adelante: eso lo agrega Swagger.",
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer",
                },
            },
            Array.Empty<string>()
        },
    });
});

var app = builder.Build();

// El manejador va primero para que atrape también lo que falle más adentro de
// la tubería.
app.UseMiddleware<ManejadorDeExcepciones>();

// Y las cabeceras van justo después, para que las lleve toda respuesta —la de
// error incluida— y no sólo las que llegan hasta el controlador.
app.UseMiddleware<CabecerasDeSeguridad>();

// Swagger en desarrollo, y en cualquier entorno donde se lo habilite a mano.
//
// El caso concreto es el contenedor de demostración: corre como Production
// —porque es lo correcto para un contenedor— y aun así conviene poder abrir
// Swagger para mostrar la API. La alternativa era declararlo Development, que
// cambia además el manejo de errores y las páginas de excepción.
if (app.Environment.IsDevelopment()
    || builder.Configuration.GetValue("Swagger:Habilitado", false))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(PoliticaCors);

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/api/salud", () => Results.Ok(new { estado = "ok", hora = DateTime.Now }));

/*
    Métricas en formato Prometheus (ADR 0015).

    Se expone sólo si hay un token configurado, y exige ese token. Sin token el
    endpoint no existe: las métricas dicen qué rutas hay, con qué frecuencia se
    usan y cuánto tardan, que es material de reconocimiento para quien busque
    por dónde entrar.

    No usa el JWT del sistema porque un recolector no es una persona: no tiene
    sesión ni rota credenciales, y pedirle que se autentique como usuario
    obligaría a crear un usuario de servicio con permisos, que es peor.
*/
var tokenDeMetricas = builder.Configuration.GetValue<string>("Metricas:Token");

if (!string.IsNullOrWhiteSpace(tokenDeMetricas))
{
    app.MapGet("/api/metricas", async (HttpContext contexto, RegistroDisyuntores disyuntores) =>
    {
        var enviado = contexto.Request.Headers["X-Metricas-Token"].FirstOrDefault();

        // Comparación en tiempo constante, igual que la de contraseñas: una
        // comparación común filtra cuántos caracteres acertó quien prueba.
        var esperado = System.Text.Encoding.UTF8.GetBytes(tokenDeMetricas);
        var recibido = System.Text.Encoding.UTF8.GetBytes(enviado ?? string.Empty);

        if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                esperado, recibido))
        {
            return Results.StatusCode(StatusCodes.Status404NotFound);
        }

        Metricas.Actualizar(disyuntores);

        contexto.Response.ContentType = "text/plain; version=0.0.4; charset=utf-8";
        await Prometheus.Metrics.DefaultRegistry.CollectAndExportAsTextAsync(
            contexto.Response.Body);

        return Results.Empty;
    });
}

app.Run();

/// <summary>Punto de anclaje para que las pruebas de integración monten la API.</summary>
public partial class Program { }
