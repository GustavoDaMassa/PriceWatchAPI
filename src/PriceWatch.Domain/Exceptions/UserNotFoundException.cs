namespace PriceWatch.Domain.Exceptions;

public class UserNotFoundException : NotFoundException
{
    public UserNotFoundException(string email)
        : base($"User with email '{email}' not found.") { }

    public UserNotFoundException(Guid id)
        : base($"User with ID '{id}' not found.") { }
}
