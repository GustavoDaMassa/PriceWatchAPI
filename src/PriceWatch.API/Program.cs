using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using PriceWatch.API.Extensions;
using PriceWatch.API.Middleware;
using PriceWatch.API.RateLimiting;
using PriceWatch.Infrastructure.Persistence.MongoDB;
using PriceWatch.Infrastructure.Settings;
using Serilog;

var localEnvPath = Path.Combine(Directory.GetCurrentDirectory(), "local.env");
if (File.Exists(localEnvPath))
    foreach (var line in File.ReadAllLines(localEnvPath)
        .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith('#')))
    {
        var eq = line.IndexOf('=');
        if (eq > 0) Environment.SetEnvironmentVariable(line[..eq].Trim(), line[(eq + 1)..].Trim());
    }

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "PriceWatch.API")
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}"));

builder.Services.AddProxyHeaders();
builder.Services.AddControllers();
builder.Services.AddHealthChecks()
    .AddCheck<PriceWatch.API.Health.DependenciesHealthCheck>("dependencies", tags: new[] { "ready" });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "PriceWatch API",
        Version = "v1",
        Description = "API para monitoramento de preços de produtos. Monitora automaticamente URLs do Mercado Livre e dispara alertas quando o preço-alvo é atingido."
    });

    c.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Insira o token JWT obtido em POST /api/auth/login. Exemplo: Bearer eyJhbGci..."
    });

    c.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        else
            policy
                .WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [])
                .AllowAnyMethod()
                .AllowAnyHeader();
    });
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddApplicationServices(builder.Configuration);

// Configuração lida via IOptions (lazy, resolvido só no primeiro uso em runtime) —
// não em uma var capturada aqui, pois builder.Configuration só recebe os overrides de
// teste (WebApplicationFactory.ConfigureAppConfiguration) no momento de builder.Build(),
// depois que este trecho de Program.cs já teria rodado (ver Erros.md, padrão já usado
// para MongoDb/Redis/Jwt).
builder.Services.Configure<RateLimitingSettings>(builder.Configuration.GetSection("RateLimiting"));

builder.Services.AddRateLimiter(options => options.RejectionStatusCode = StatusCodes.Status429TooManyRequests);

builder.Services.AddOptions<RateLimiterOptions>()
    .Configure<IOptions<RateLimitingSettings>>((rateLimiterOptions, rateLimitingOptions) =>
    {
        var settings = rateLimitingOptions.Value;
        var window = TimeSpan.FromSeconds(settings.WindowSeconds);

        rateLimiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            RateLimitPartitionSelector.Select(
                httpContext, settings.AnonymousPermitLimit, settings.AuthenticatedPermitLimit, window));
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Use(async (context, next) =>
{
    using (Serilog.Context.LogContext.PushProperty("CorrelationId", context.TraceIdentifier))
        await next();
});

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
// liveness não toca em dependências (o orquestrador reinicia se falhar); readiness confere Mongo e Redis
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = c => c.Tags.Contains("ready")
});

// initialize MongoDB indexes before accepting requests
using (var scope = app.Services.CreateScope())
{
    var indexInitializer = scope.ServiceProvider.GetRequiredService<MongoDbIndexInitializer>();
    await indexInitializer.InitializeAsync();
}

app.Run();

public partial class Program { }
