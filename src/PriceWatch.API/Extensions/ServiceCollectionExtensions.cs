using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Polly;
using Polly.Extensions.Http;
using PriceWatch.Application.Interfaces;
using PriceWatch.Application.UseCases.Auth;
using PriceWatch.Application.UseCases.Catalog;
using PriceWatch.Infrastructure.Persistence.MongoDB;
using PriceWatch.Application.UseCases.Notification;
using PriceWatch.Application.UseCases.Offers;
using PriceWatch.Application.UseCases.ProductList;
using PriceWatch.Application.UseCases.Stores;
using PriceWatch.Application.UseCases.TrackedProduct;
using PriceWatch.Application.UseCases.Users;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;
using PriceWatch.Infrastructure.Affiliates;
using PriceWatch.Infrastructure.Email;
using PriceWatch.Infrastructure.Fetchers;
using PriceWatch.Infrastructure.Messaging;
using PriceWatch.Infrastructure.Network;
using PriceWatch.Infrastructure.Persistence.MongoDB.Repositories;
using PriceWatch.Infrastructure.Search;
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

        var jwtSettings = configuration.GetSection("Jwt").Get<JwtSettings>()
            ?? throw new InvalidOperationException("Missing 'Jwt' configuration section.");

        if (string.IsNullOrWhiteSpace(jwtSettings.Secret))
            throw new InvalidOperationException(
                "Jwt:Secret must be set via configuration/environment variable — no default is allowed.");

        var key = Encoding.UTF8.GetBytes(jwtSettings.Secret);

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
        services.AddHttpClient<MercadoLivreTokenService>()
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());
        services.AddSingleton<MercadoLivreTokenService>();
        services.AddSingleton<IMercadoLivreTokenProvider>(sp => sp.GetRequiredService<MercadoLivreTokenService>());
        services.AddScoped<IPriceFetcher, MercadoLivreFetcher>();
        services.AddScoped<IPriceFetcherResolver, PriceFetcherResolver>();
        services.AddHttpClient<MercadoLivreFetcher>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "PriceWatchAPI/1.0");
        })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());
        services.AddHostedService<PriceCheckWorker>();
        services.AddHostedService<StoreOfferPriceCheckWorker>();

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

        // Pivô multi-loja: Product / StoreOffer / UserProductSubscription
        services.Configure<FeatureFlags>(configuration.GetSection("Features"));
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IStoreOfferRepository, StoreOfferRepository>();
        services.AddScoped<IUserProductSubscriptionRepository, UserProductSubscriptionRepository>();
        services.AddScoped<IClickLogRepository, ClickLogRepository>();
        services.AddScoped<IOfferCandidateRepository, OfferCandidateRepository>();
        services.AddScoped<IOfferMatchFeedbackRepository, OfferMatchFeedbackRepository>();
        services.AddHostedService<BackfillWorker>();
        services.AddScoped<IProductMatchingEventPublisher, RedisStoreOfferEventPublisher>();
        services.AddHostedService<ProductMatchingConsumer>();

        // descoberta de ofertas em outras lojas (M3): cada buscador nunca lança e fica indisponível sem credenciais
        services.AddHttpClient<KabumOfferSearcher>()
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());
        services.AddScoped<IOfferSearcher>(sp => sp.GetRequiredService<KabumOfferSearcher>());
        services.AddHttpClient("mercadolivre-search")
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());
        services.AddScoped<IOfferSearcher>(sp => new MercadoLivreOfferSearcher(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("mercadolivre-search"),
            sp.GetRequiredService<IMercadoLivreTokenProvider>(),
            sp.GetRequiredService<IOptions<MercadoLivreSettings>>(),
            sp.GetRequiredService<MercadoLivreFetcher>(),
            sp.GetRequiredService<ILogger<MercadoLivreOfferSearcher>>()));

        // resolução de links curtos com defesa contra SSRF (só IPs públicos, validado também na conexão)
        services.AddSingleton<IHostAddressResolver, DnsHostAddressResolver>();
        services.AddHttpClient<IUrlResolver, SafeUrlResolver>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (compatible; PriceWatchBot/1.0; +https://pricewatch.gustavohdev.com.br)");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                ConnectTimeout = TimeSpan.FromSeconds(5),
                ConnectCallback = SafeUrlResolver.ConnectAsync
            });

        // Lomadee/SocialSoul — agregador de afiliados escolhido na Fase 6 (cobre Kabum, ver docs/arquitetura.md)
        services.Configure<LomadeeSettings>(configuration.GetSection("Lomadee"));
        services.AddScoped<IPriceFetcher, KabumFetcher>();
        services.AddHttpClient<KabumFetcher>()
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());
        // Passthrough sempre por último — CanHandle universal funciona como fallback do resolver
        services.AddScoped<IAffiliateLinkGenerator, LomadeeDeeplinkGenerator>();
        services.AddHttpClient<LomadeeDeeplinkGenerator>()
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());
        services.AddScoped<IAffiliateLinkGenerator, PassthroughAffiliateLinkGenerator>();
        services.AddScoped<IAffiliateLinkResolver, AffiliateLinkResolver>();

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
        services.AddScoped<GetUserProductsUseCase>();
        services.AddScoped<UpdateProductUseCase>();
        services.AddScoped<AssignProductToListUseCase>();
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

        // Offers (pivô multi-loja)
        services.AddScoped<CreateOfferUseCase>();
        services.AddScoped<GetProductComparisonUseCase>();
        services.AddScoped<RedirectOfferUseCase>();
        services.AddScoped<BackfillTrackedProductsUseCase>();
        services.AddScoped<MatchStoreOfferUseCase>();
        services.AddScoped<ICreateOfferUseCase>(sp => sp.GetRequiredService<CreateOfferUseCase>());
        services.AddScoped<ImportFromLinkUseCase>();
        services.AddScoped<CheckOfferPriceUseCase>();
        services.AddScoped<GetSupportedStoresUseCase>();
        services.AddScoped<LegacyProductCatalogFacade>();
        services.AddScoped<UnifiedProductCatalogFacade>();
        // escolhido a cada request pela flag (lazy via IOptions): cutover e rollback sem novo deploy
        services.AddScoped<IProductCatalogFacade>(sp =>
            sp.GetRequiredService<IOptions<FeatureFlags>>().Value.UnifiedCatalogEnabled
                ? sp.GetRequiredService<UnifiedProductCatalogFacade>()
                : sp.GetRequiredService<LegacyProductCatalogFacade>());
        services.AddScoped<SyncOfferActivityUseCase>();

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
}
