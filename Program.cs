using Microsoft.OpenApi;
using Abril_Backend.Shared.Services.Email.Interfaces;
using Abril_Backend.Shared.Services.Email.Services;
using Abril_Backend.Infrastructure.Repositories;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Services;
using Abril_Backend.Application.Services;
using Abril_Backend.Application.Interfaces;
using Abril_Backend.Features.MicrosoftAuth;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using Abril_Backend.Features.Costs;
using Abril_Backend.Features.ConfigurationModule;
using Abril_Backend.Shared.Services.Reniec.Services;
using Abril_Backend.Shared.Services.Reniec.Interfaces;
using Abril_Backend.Features.Contractors;
using Abril_Backend.Features.Ssoma;
using Abril_Backend.Features.Habilitacion;
using Abril_Backend.Shared.Services.Sunat.Providers.Decolecta;
using Abril_Backend.Shared.Services.Sunat.Interfaces;
using Abril_Backend.Shared.Interceptors;
using FluentValidation;
using FluentValidation.AspNetCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

var databaseProvider = builder.Configuration["Database:DatabaseProvider"];
var emailProvider = builder.Configuration["Email:EmailProvider"];
var storageProvider = builder.Configuration["Storage:StorageProvider"];

// Add services to the container.
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

builder.Services.AddCostsModule();
builder.Services.AddContractorsModule();
builder.Services.AddConfigurationModule();
builder.Services.AddMicrosoftAuthModule(builder.Configuration);
builder.Services.AddSsomaModule();
builder.Services.AddHabilitacionModule();

builder.Services.AddScoped<IConstructionSiteLogbookControlService, ConstructionSiteLogbookControlService>();
builder.Services.AddScoped<IIvtControlPdfService, IvtControlPdfService>();
builder.Services.AddScoped<IProjectResidentService, ProjectResidentService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IReminderService, ReminderService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IResidentReportIncidenceService, ResidentReportIncidenceService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IResidentMonitoringService, ResidentMonitoringService>();
builder.Services.AddScoped<IRoleService, RoleService>();
// IReniecService se registra vía AddHttpClient abajo para que el HttpClient tenga base URL y token configurados
builder.Services.AddScoped<IArquitecturaComercialService, ArquitecturaComercialService>();

builder.Services.AddScoped<IConstructionSiteLogbookControlRepository, ConstructionSiteLogbookControlRepository>();
builder.Services.AddScoped<IMilestoneScheduleRepository, MilestoneScheduleRepository>();
builder.Services.AddScoped<IMilestoneScheduleHistoryRepository, MilestoneScheduleHistoryRepository>();
builder.Services.AddScoped<IIvtControlPdfRepository, IvtControlPdfRepository>();
builder.Services.AddScoped<IUserProjectRepository, UserProjectRepository>();
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
builder.Services.AddScoped<DashboardRepository>();
builder.Services.AddScoped<LayerRepository>();
builder.Services.AddScoped<LessonRepository>();
builder.Services.AddScoped<MilestoneRepository>();
builder.Services.AddScoped<MilestoneScheduleRepository>();
builder.Services.AddScoped<MilestoneScheduleHistoryRepository>();
builder.Services.AddScoped<PersonRepository>();
builder.Services.AddScoped<PhaseRepository>();
builder.Services.AddScoped<PhaseStageSubStageSubSpecialtyRepository>();
builder.Services.AddScoped<ProjectRepository>();
builder.Services.AddScoped<StageRepository>();
builder.Services.AddScoped<SubSpecialtyRepository>();
builder.Services.AddScoped<SubStageRepository>();
builder.Services.AddScoped<UserProjectRepository>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<ExcelService>();
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("Email:EmailSettings")
);

builder.Services.Configure<FrontendSettings>(builder.Configuration.GetSection("FrontendSettings"));
builder.Services.AddHttpClient<IReniecService, ReniecService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Reniec:ReniecService"]!);
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", builder.Configuration["Reniec:Token"]!);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});
builder.Services.AddHttpClient<IDelegatedMailService, GraphDelegatedMailService>(client =>
{
    client.BaseAddress = new Uri("https://graph.microsoft.com/");
});
builder.Services.AddHttpClient<ISunatService, DecolectaSunatService>(client =>
{
    var baseUrl = builder.Configuration["Sunat:BaseUrl"];
    if (!string.IsNullOrEmpty(baseUrl))
        client.BaseAddress = new Uri(baseUrl);
});
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

builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        p => p
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod()
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

            NameClaimType = ClaimTypes.NameIdentifier
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

var app = builder.Build();
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
app.UseHttpsRedirection();
app.UseCors("AllowAngular");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => "API funcionando");

app.Run();
