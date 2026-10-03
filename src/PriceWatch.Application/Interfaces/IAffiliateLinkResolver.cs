using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Services;

namespace PriceWatch.Application.Interfaces;

public interface IAffiliateLinkResolver
{
    IAffiliateLinkGenerator Resolve(ProductSource source);
    IAffiliateLinkGenerator Resolve(string url);
}
