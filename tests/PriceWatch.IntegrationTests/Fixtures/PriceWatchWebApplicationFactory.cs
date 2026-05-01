using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using PriceWatch.Domain.Interfaces.Services;
using PriceWatch.Infrastructure.Messaging;
using PriceWatch.Infrastructure.Settings;
using PriceWatch.Infrastructure.Workers;
using Testcontainers.MongoDb;
using Testcontainers.Redis;

namespace PriceWatch.IntegrationTests.Fixtures;

public class PriceWatchWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string TestJwtSecret = "test-secret-key-at-least-32-characters-long!!";
    private const string TestJwtIssuer = "PriceWatch";
    private const string TestJwtAudience = "PriceWatchUsers";

    public FakeEmailSender FakeEmail { get; } = new();

    private readonly MongoDbContainer _mongo = new MongoDbBuilder()
        .WithImage("mongo:7.0")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _mongo.StartAsync();
        await _redis.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _mongo.DisposeAsync();
        await _redis.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // strings de conexão injetadas via ConfigureAppConfiguration para que
        // MongoClient e ConnectionMultiplexer sejam criados apontando para os containers
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MongoDb:ConnectionString"] = _mongo.GetConnectionString(),
                ["MongoDb:DatabaseName"] = "pricewatch_test",
                ["Redis:ConnectionString"] = _redis.GetConnectionString()
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // remove workers — evita loop de check e consumer durante os testes
            var workers = services
                .Where(d => d.ServiceType == typeof(IHostedService) &&
                           (d.ImplementationType == typeof(PriceCheckWorker) ||
                            d.ImplementationType == typeof(RedisStreamConsumer)))
                .ToList();
            foreach (var w in workers)
                services.Remove(w);

            // garante que JwtTokenService assina com o mesmo segredo que o bearer valida
            // PostConfigure roda DEPOIS de AddApplicationServices — evita race na leitura da config
            services.Configure<JwtSettings>(s =>
            {
                s.Secret = TestJwtSecret;
                s.Issuer = TestJwtIssuer;
                s.Audience = TestJwtAudience;
                s.ExpiryHours = 1;
            });

            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSecret));
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = TestJwtIssuer,
                    ValidAudience = TestJwtAudience,
                    IssuerSigningKey = key
                };
            });

            // substitui email real pelo fake
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(FakeEmail);
        });
    }
}
