using Abril_Backend.Infrastructure.Repositories;
using Abril_Backend.Infrastructure.ExternalServices;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.InternalServices;
using Abril_Backend.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
var builder = WebApplication.CreateBuilder(args);
var databaseProvider = builder.Configuration["DatabaseProvider"];
var emailProvider = builder.Configuration["EmailProvider"];
var storageProvider = builder.Configuration["StorageProvider"];

// Add services to the container.
builder.Services.AddDbContextFactory<AppDbContext>(options =>
{
    if (databaseProvider == "SqlServer")
    {
        var conn = builder.Configuration.GetConnectionString("SqlServer");
        options.UseSqlServer(conn);
    }
    else if (databaseProvider == "PostgreSQL")
    {
        var conn = builder.Configuration.GetConnectionString("PostgreSQL");
        options.UseNpgsql(conn).UseSnakeCaseNamingConvention();
    }
    else
    {
        throw new Exception("Proveedor de BD no soportado");
    }
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
builder.Services.AddScoped<ScheduleRepository>();
builder.Services.AddScoped<StageRepository>();
builder.Services.AddScoped<SubSpecialtyRepository>();
builder.Services.AddScoped<SubStageRepository>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<UserProjectRepository>();
builder.Services.AddScoped<UserRegistrationTokenRepository>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<ExcelService>();
builder.Services.AddScoped<AuthRepository>();
builder.Services.AddScoped<JwtService>();
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings")
);

builder.Services.Configure<FrontendSettings>(builder.Configuration.GetSection("FrontendSettings"));
builder.Services.AddHttpClient<ReniecService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetConnectionString("ReniecService"));
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", builder.Configuration["Reniec:Token"]);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});
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
builder.Services.AddSwaggerGen();
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
    }
);
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
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => "API funcionando");

app.Run();
