namespace PriceWatch.Domain.Exceptions;

public class TrackedProductNotFoundException : NotFoundException
{
    public TrackedProductNotFoundException(string identifier)
        : base($"Tracked product '{identifier}' not found.") { }
}
