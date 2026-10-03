using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.Offers;

/// <summary>
/// Mantém <c>StoreOffer.IsActive</c> coerente com os assinantes do produto: uma oferta só é
/// monitorada se existir ao menos uma assinatura ativa. Chamado sempre que uma assinatura é
/// criada, reativada, pausada ou removida.
/// </summary>
public class SyncOfferActivityUseCase
{
    private readonly IStoreOfferRepository _offers;
    private readonly IUserProductSubscriptionRepository _subscriptions;

    public SyncOfferActivityUseCase(
        IStoreOfferRepository offers,
        IUserProductSubscriptionRepository subscriptions)
    {
        _offers = offers;
        _subscriptions = subscriptions;
    }

    public async Task ExecuteAsync(string productId)
    {
        var anyActiveSubscription = (await _subscriptions.GetByProductIdAsync(productId)).Any(s => s.IsActive);
        var offers = await _offers.GetByProductIdAsync(productId);

        foreach (var offer in offers)
        {
            if (anyActiveSubscription && !offer.IsActive)
                offer.Activate();
            else if (!anyActiveSubscription && offer.IsActive)
                offer.Deactivate();
            else
                continue;

            await _offers.UpdateAsync(offer);
        }
    }
}
