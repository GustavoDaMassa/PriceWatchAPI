namespace PriceWatch.Domain.Exceptions;

public class ProductNotFoundException : NotFoundException
{
    public ProductNotFoundException(string identifier)
        : base($"Product '{identifier}' not found.") { }
}
