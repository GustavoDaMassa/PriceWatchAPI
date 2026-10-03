using PriceWatch.Application.DTOs.Offer;

namespace PriceWatch.Application.UseCases.Offers;

public interface ICreateOfferUseCase
{
    Task<OfferResponse> ExecuteAsync(string userId, CreateOfferRequest request);
}
