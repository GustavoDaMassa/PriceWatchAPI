using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using PriceWatch.Application.Interfaces;
using PriceWatch.Application.UseCases.Auth;
using PriceWatch.Application.UseCases.ProductList;
using PriceWatch.Application.UseCases.TrackedProduct;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;
using PriceWatch.Infrastructure.Email;
using PriceWatch.Infrastructure.Fetchers;
using PriceWatch.Infrastructure.Persistence.MongoDB.Repositories;
using PriceWatch.Infrastructure.Security;
using PriceWatch.Infrastructure.Settings;
using PriceWatch.Infrastructure.Workers;

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
            .AddInfrastructure()
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

    private static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.Configure<SmtpSettings>(
            services.BuildServiceProvider()
                .GetRequiredService<IConfiguration>()
                .GetSection("Smtp"));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        // ProductList
        services.AddScoped<IProductListRepository, ProductListRepository>();

        // TrackedProduct
        services.AddScoped<ITrackedProductRepository, TrackedProductRepository>();
        services.AddScoped<IPriceSnapshotRepository, PriceSnapshotRepository>();
        services.AddScoped<IPriceFetcher, MercadoLivreFetcher>();
        services.AddScoped<IPriceFetcherResolver, PriceFetcherResolver>();
        services.AddHttpClient<MercadoLivreFetcher>();
        services.AddHostedService<PriceCheckWorker>();

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

        return services;
    }
}
