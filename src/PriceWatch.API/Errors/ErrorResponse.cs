namespace PriceWatch.API.Errors;

public record ErrorResponse(int Status, string Error, string Message);
