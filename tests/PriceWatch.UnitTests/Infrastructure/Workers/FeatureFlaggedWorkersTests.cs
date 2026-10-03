using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PriceWatch.Infrastructure.Messaging;
using PriceWatch.Infrastructure.Settings;
using PriceWatch.Infrastructure.Workers;
using StackExchange.Redis;

namespace PriceWatch.UnitTests.Infrastructure.Workers;

public class FeatureFlaggedWorkersTests
{
    [Fact]
    public async Task BackfillWorker_WhenBackfillIsDisabled_DoesNotTouchTheDatabase()
    {
        var scopeFactory = new Mock<IServiceScopeFactory>();
        var worker = new BackfillWorker(
            scopeFactory.Object,
            Options.Create(new FeatureFlags { BackfillEnabled = false }),
            NullLogger<BackfillWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask!;

        scopeFactory.Verify(f => f.CreateScope(), Times.Never);
    }

    [Fact]
    public async Task BackfillWorker_WhenBackfillIsEnabled_RunsTheBackfill()
    {
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Throws(new InvalidOperationException("stop here"));
        var worker = new BackfillWorker(
            scopeFactory.Object,
            Options.Create(new FeatureFlags { BackfillEnabled = true }),
            NullLogger<BackfillWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask!;

        scopeFactory.Verify(f => f.CreateScope(), Times.Once);
    }

    [Fact]
    public async Task ProductMatchingConsumer_WhenMatchingIsDisabled_DoesNotConnectToRedis()
    {
        var redis = new Mock<IConnectionMultiplexer>();
        var consumer = new ProductMatchingConsumer(
            redis.Object,
            new Mock<IServiceScopeFactory>().Object,
            Options.Create(new FeatureFlags { MatchingEnabled = false }),
            NullLogger<ProductMatchingConsumer>.Instance);

        await consumer.StartAsync(CancellationToken.None);
        await consumer.ExecuteTask!;

        redis.Verify(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()), Times.Never);
    }
}
