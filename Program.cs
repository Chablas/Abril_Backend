using Microsoft.OpenApi;
using Abril_Backend.Shared.Realtime;
using Abril_Backend.Shared.Services.Email.Interfaces;
using Abril_Backend.Shared.Services.Email.Services;
using Abril_Backend.Shared.Services.Graph.Interfaces;
using Abril_Backend.Shared.Services.Graph.Services;
using Abril_Backend.Infrastructure.Repositories;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Application.Services;
using Abril_Backend.Features.MejoraContinuaModule;
using Abril_Backend.Application.Services;
using Abril_Backend.Infrastructure.Services;
using Abril_Backend.Application.Interfaces;
using Abril_Backend.Features.AuthModule;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using Abril_Backend.Features.Costs;
using Abril_Backend.Features.ConfigurationModule;
using Abril_Backend.Features.Shared.Application.Services;
using Abril_Backend.Shared.Services.Reniec.Services;
using Abril_Backend.Shared.Services.Reniec.Interfaces;
using Abril_Backend.Features.Contractors;
using Abril_Backend.Features.Ssoma;
using Abril_Backend.Features.GestionAdministrativa;
using Abril_Backend.Features.Habilitacion;
using Abril_Backend.Features.UnidadDeProyectosModule;
using Abril_Backend.Features.Evaluaciones;
using Abril_Backend.Features.VecinosModule;
using Abril_Backend.Features.AccountingModule;
using Abril_Backend.Features.BoletinModule;
using Abril_Backend.Features.ArquitecturaComercialModule;
using Abril_Backend.Shared.Services.Sunat.Providers.Decolecta;
using Abril_Backend.Shared.Services.Sunat.Interfaces;
using Abril_Backend.Shared.Services.Decolecta.Interfaces;
using Abril_Backend.Shared.Services.Decolecta.Services;
using Abril_Backend.Shared.Interceptors;
using Microsoft.AspNetCore.Http.Features;
using FluentValidation;
using FluentValidation.AspNetCore;
using System.Threading.RateLimiting;

// Configuración global de Dapper: mapea automáticamente columnas snake_case (BD)
// a propiedades PascalCase (DTOs), igual que la convención que usa EF Core.
Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

// QuestPDF Community License — gratuita para uso interno/<$1M USD revenue anual.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

//builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 3_000_000_000;
});

// OJO: subir MaxRequestBodySize de Kestrel NO basta para subidas grandes multipart/form-data.
// El parseo de formularios tiene su propio tope, MultipartBodyLengthLimit, que por defecto es
// 128 MB. Al superarlo, el binding de [FromForm]/IFormFile lanza InvalidDataException ANTES de
// entrar al try/catch del controller → 500 genérico ("Ocurrió un error" en el front). Por eso
// un dossier ATS pesado (muchos trabajadores) fallaba aunque los PDFs chicos subían bien.
// Lo alineamos con el límite de Kestrel para que ambos topes sean el mismo. Los endpoints que
// quieran un tope menor lo siguen imponiendo con su propio [RequestSizeLimit].
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 3_000_000_000;
});

var databaseProvider = builder.Configuration["Database:DatabaseProvider"];
var emailProvider = builder.Configuration["Email:EmailProvider"];
var storageProvider = builder.Configuration["Storage:StorageProvider"];

// Add services to the container.
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<AuditoriaInterceptor>();

builder.Services.AddDbContextFactory<AppDbContext>((sp, options) =>
{
    if (databaseProvider == "SqlServer")
    {
        var conn = builder.Configuration["Database:SqlServer"];
        options.UseSqlServer(conn);
    }
    else if (databaseProvider == "PostgreSQL")
    {
        var conn = builder.Configuration["Database:PostgreSQL"];
        options.UseNpgsql(conn, npgsqlOptions =>
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null))
        .UseSnakeCaseNamingConvention();
    }
    else
    {
        throw new Exception("Proveedor de BD no soportado");
    }

    // Errores de EF Core más explícitos (mensaje y constraint name visibles en excepciones).
    options.EnableDetailedErrors();

    // En no-producción, además mostrar los valores reales de los parámetros SQL.
    // ⚠ NUNCA habilitar en Production: puede loggear contraseñas/PII.
    if (!builder.Environment.IsProduction())
        options.EnableSensitiveDataLogging();

    options.AddInterceptors(sp.GetRequiredService<AuditoriaInterceptor>());
});

if (emailProvider == "SendGrid")
{
    builder.Services.AddSingleton<IEmailService, SendGridEmailService>();
} else if (emailProvider == "PowerAutomate")
{
    builder.Services.AddSingleton<IEmailService, PowerAutomateEmailService>();
}
else
{
    builder.Services.AddSingleton<IEmailService, SmtpEmailService>();
}

if (storageProvider == "Azure")
{
    builder.Services.AddSingleton<IFileStorageService, AzureBlobStorageService>();
}
else
{
    builder.Services.AddSingleton<IFileStorageService, LocalFileStorageService>();
}

builder.Services.AddSingleton<IStorageContainerResolver, StorageContainerResolver>();
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));

if (storageProvider == "Azure")
{
    builder.Services.AddScoped<IFileStorageService, AzureBlobStorageService>();
}
else
{
    builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
}

builder.Services.AddCostsModule(builder.Configuration);
builder.Services.AddContractorsModule();
builder.Services.AddConfigurationModule();
builder.Services.AddAuthModule(builder.Configuration);
builder.Services.AddSsomaModule();
builder.Services.AddHostedService<Abril_Backend.Features.SsomaModule.IndicadoresProactivosFeature.Infrastructure.SsomaIndicadoresCacheWarmup>();
builder.Services.AddGestionAdministrativaModule();
builder.Services.AddHabilitacionModule();
builder.Services.AddEvaluacionesModule();
builder.Services.AddUnidadDeProyectosModule();
builder.Services.AddMejoraContinuaModule();
builder.Services.AddVecinosModule();
builder.Services.AddAccountingModule();
builder.Services.AddBoletinModule();
builder.Services.AddArquitecturaComercialModule();

builder.Services.AddScoped<IConstructionSiteLogbookControlService, ConstructionSiteLogbookControlService>();
builder.Services.AddScoped<IIvtControlPdfService, IvtControlPdfService>();
builder.Services.AddScoped<IProjectResidentService, ProjectResidentService>();
builder.Services.AddScoped<IReminderService, ReminderService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IResidentReportIncidenceService, ResidentReportIncidenceService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IResidentMonitoringService, ResidentMonitoringService>();
builder.Services.AddScoped<IRoleService, RoleService>();
// IReniecService se registra vía AddHttpClient abajo para que el HttpClient tenga base URL y token configurados
builder.Services.AddScoped<IArquitecturaComercialService, ArquitecturaComercialService>();
builder.Services.AddScoped<ISharedFiltersService, SharedFiltersService>();

builder.Services.AddScoped<IConstructionSiteLogbookControlRepository, ConstructionSiteLogbookControlRepository>();
builder.Services.AddScoped<IIvtControlPdfRepository, IvtControlPdfRepository>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IProjectResidentRepository, ProjectResidentRepository>();
builder.Services.AddScoped<IUserPasswordTokenRepository, UserPasswordTokenRepository>();
builder.Services.AddScoped<IResidentReportIncidenceRepository, ResidentReportIncidenceRepository>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IJWTService, JwtService>();
builder.Services.AddScoped<IResidentMonitoringRepository, ResidentMonitoringRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IArquitecturaComercialRepository, ArquitecturaComercialRepository>();

builder.Services.AddScoped<AreaRepository>();
builder.Services.AddScoped<MilestoneRepository>();
builder.Services.AddScoped<PersonRepository>();
builder.Services.AddScoped<ProjectRepository>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<ExcelService>();
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("Email:EmailSettings")
);

builder.Services.Configure<FrontendSettings>(builder.Configuration.GetSection("FrontendSettings"));
// Cliente único hacia la API de Decolecta (RENIEC + SUNAT) con rotación de tokens desde
// la tabla decolecta_token: el token ya no va fijo en DefaultRequestHeaders porque
// DecolectaApiClient lo pone por request y rota al siguiente cuando uno agota su cuota.
builder.Services.AddScoped<IDecolectaTokenStore, DecolectaTokenStore>();
builder.Services.AddHttpClient<IDecolectaApiClient, DecolectaApiClient>(client =>
{
    var baseUrl = builder.Configuration["Sunat:BaseUrl"]
        ?? builder.Configuration["Reniec:ReniecService"]
        ?? "https://api.decolecta.com";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});
builder.Services.AddScoped<IReniecService, ReniecService>();
builder.Services.AddHttpClient<IDelegatedMailService, GraphDelegatedMailService>(client =>
{
    client.BaseAddress = new Uri("https://graph.microsoft.com/");
});

// Resolver de grupos de correo (desglosa grupos → miembros antes de enviar).
// Registrado globalmente para que cualquier módulo lo pueda inyectar (p. ej. recordatorios
// de lecciones aprendidas vía PowerAutomate). Lo implementa GraphUserService.
builder.Services.AddScoped<IEmailGroupResolver, GraphUserService>();
builder.Services.AddScoped<ISunatService, DecolectaSunatService>();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("sunat-ruc", httpContext =>
    {
        if (httpContext.User.Identity?.IsAuthenticated == true)
            return RateLimitPartition.GetNoLimiter("authenticated");

        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromHours(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, _) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"message\":\"Has superado el límite de 5 consultas por hora. Intenta más tarde.\"}"
        );
    };
});
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddControllers()
    .AddJsonOptions(opts => opts.JsonSerializerOptions.Converters.Add(new Abril_Backend.Shared.Helpers.NullableDateOnlyJsonConverter()));
builder.Services.AddSignalR();
builder.Services.AddScoped<IRealtimeNotifier, RealtimeNotifier>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        p => p
            .SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
    );
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingresa el token JWT. Ejemplo: eyJhbGci..."
    });
    c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer"), new List<string>() }
    });
    c.MapType<IFormFile>(() => new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" });
});
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options => {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])
            ),

            NameClaimType = ClaimTypes.NameIdentifier,

            // El JWT vive 2 min y se refresca contra user_session; sin esto, el
            // ClockSkew por defecto (5 min) lo mantendría válido ~7 min reales.
            ClockSkew = TimeSpan.Zero
        };

        // El cliente de SignalR (WebSocket) no puede mandar el header Authorization,
        // así que envía el JWT por query string; lo leemos solo para las rutas /hubs.
        // Mismo truco para las rutas .../contenido: son <img src="...">, y el navegador
        // no manda headers custom en un <img> — por eso ahí también se acepta el token
        // por query string (ver ObservacionesController.GetFotoContenido).
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/hubs") || path.Value!.EndsWith("/contenido")))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    })
    .AddJwtBearer("AzureAd", options =>
    {
        options.Authority = $"https://login.microsoftonline.com/{builder.Configuration["AzureAd:TenantId"]}/v2.0";
        options.Audience = builder.Configuration["AzureAd:ClientId"];
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            NameClaimType = "preferred_username"
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes("Bearer", "AzureAd")
        .RequireAuthenticatedUser()
        .Build();
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Manejo global de fallos de conectividad con la BD → 503 con detalle real (no 500 opaco).
builder.Services.AddExceptionHandler<Abril_Backend.Shared.Exceptions.DatabaseExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// PdfSharpCore trae un proveedor de imágenes incompatible con ImageSharp 3.x (lanza
// MissingMethodException al dibujar cualquier imagen en un PDF, p. ej. la firma de facturas).
// Se reemplaza por uno que usa las APIs de ImageSharp 3.x y conserva la transparencia.
MigraDocCore.DocumentObjectModel.MigraDoc.DocumentObjectModel.Shapes.ImageSource.ImageSourceImpl
    = new Abril_Backend.Shared.Services.Pdf.ImageSharp3ImageSource();

app.UseCors("AllowAngular");
app.UseExceptionHandler();
app.UseStaticFiles();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Abril API v1");
        c.RoutePrefix = "swagger"; // opcional
    });
}
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationsHub>("/hubs/notifications");
app.MapGet("/", () => "API funcionando");

app.Run();
