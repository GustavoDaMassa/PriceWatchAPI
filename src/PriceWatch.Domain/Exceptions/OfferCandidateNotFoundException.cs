namespace PriceWatch.Domain.Exceptions;

public class OfferCandidateNotFoundException : NotFoundException
{
    public OfferCandidateNotFoundException(string identifier)
        : base($"Offer candidate '{identifier}' not found.") { }
}
