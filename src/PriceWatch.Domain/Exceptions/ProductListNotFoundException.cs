namespace PriceWatch.Domain.Exceptions;

public class ProductListNotFoundException : NotFoundException
{
    public ProductListNotFoundException(string identifier)
        : base($"Product list '{identifier}' not found.") { }
}
