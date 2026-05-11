using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using PriceWatch.Application.Interfaces;
using PriceWatch.Application.UseCases.Auth;
using PriceWatch.Infrastructure.Persistence.MongoDB;
using PriceWatch.Application.UseCases.Notification;
using PriceWatch.Application.UseCases.ProductList;
using PriceWatch.Application.UseCases.TrackedProduct;
using PriceWatch.Application.UseCases.Users;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;
using PriceWatch.Infrastructure.Email;
using PriceWatch.Infrastructure.Fetchers;
using PriceWatch.Infrastructure.Messaging;
using PriceWatch.Infrastructure.Persistence.MongoDB.Repositories;
using PriceWatch.Infrastructure.Security;
using PriceWatch.Infrastructure.Settings;
using PriceWatch.Infrastructure.Workers;
using StackExchange.Redis;

namespace PriceWatch.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddMongoDb(configuration)
            .AddJwtAuth(configuration)
            .AddInfrastructure(configuration)
            .AddUseCases();

        return services;
    }

    private static IServiceCollection AddMongoDb(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MongoDbSettings>(configuration.GetSection("MongoDb"));

        services.AddSingleton<IMongoClient>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
            return new MongoClient(settings.ConnectionString);
        });

        services.AddScoped<IMongoDatabase>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
            var client = sp.GetRequiredService<IMongoClient>();
            return client.GetDatabase(settings.DatabaseName);
        });

        services.AddScoped<MongoDbIndexInitializer>();

        return services;
    }

    private static IServiceCollection AddJwtAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));

        var jwtSettings = configuration.GetSection("Jwt").Get<JwtSettings>()!;
        var key = Encoding.UTF8.GetBytes(jwtSettings.Secret ?? "default-dev-secret-change-in-production");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(key)
                };
            });

        return services;
    }

    private static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SmtpSettings>(configuration.GetSection("Smtp"));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        // ProductList
        services.AddScoped<IProductListRepository, ProductListRepository>();

        // TrackedProduct
        services.AddScoped<ITrackedProductRepository, TrackedProductRepository>();
        services.AddScoped<IPriceSnapshotRepository, PriceSnapshotRepository>();
        services.Configure<MercadoLivreSettings>(configuration.GetSection("MercadoLivre"));
        services.AddHttpClient<MercadoLivreTokenService>();
        services.AddSingleton<MercadoLivreTokenService>();
        services.AddScoped<IPriceFetcher, MercadoLivreFetcher>();
        services.AddScoped<IPriceFetcherResolver, PriceFetcherResolver>();
        services.AddHttpClient<MercadoLivreFetcher>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "PriceWatchAPI/1.0");
        });
        services.AddHostedService<PriceCheckWorker>();

        // Notification
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.Configure<RedisSettings>(configuration.GetSection("Redis"));
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<RedisSettings>>().Value;
            return ConnectionMultiplexer.Connect(settings.ConnectionString);
        });
        services.AddScoped<IAlertPublisher, RedisStreamPublisher>();
        services.AddHostedService<RedisStreamConsumer>();

        return services;
    }

    private static IServiceCollection AddUseCases(this IServiceCollection services)
    {
        services.AddScoped<RegisterUseCase>();
        services.AddScoped<LoginUseCase>();
        services.AddScoped<VerifyEmailUseCase>();
        services.AddScoped<ResendVerificationUseCase>();

        // ProductList
        services.AddScoped<CreateListUseCase>();
        services.AddScoped<GetUserListsUseCase>();
        services.AddScoped<UpdateListUseCase>();
        services.AddScoped<DeleteListUseCase>();
        services.AddScoped<GetListAnalysisUseCase>();

        // TrackedProduct
        services.AddScoped<AddProductUseCase>();
        services.AddScoped<GetProductsByListUseCase>();
        services.AddScoped<UpdateProductUseCase>();
        services.AddScoped<RemoveProductUseCase>();
        services.AddScoped<GetPriceHistoryUseCase>();

        // Notification
        services.AddScoped<GetNotificationsUseCase>();
        services.AddScoped<MarkAsReadUseCase>();
        services.AddScoped<MarkAllAsReadUseCase>();
        services.AddScoped<ProcessAlertUseCase>();

        // Users
        services.AddScoped<GetProfileUseCase>();
        services.AddScoped<ChangePasswordUseCase>();
        services.AddScoped<ChangeEmailUseCase>();
        services.AddScoped<DeleteAccountUseCase>();

        return services;
    }
}
