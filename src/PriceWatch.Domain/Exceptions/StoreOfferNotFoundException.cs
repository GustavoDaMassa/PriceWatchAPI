namespace PriceWatch.Domain.Exceptions;

public class StoreOfferNotFoundException : NotFoundException
{
    public StoreOfferNotFoundException(string identifier)
        : base($"Store offer '{identifier}' not found.") { }
}
