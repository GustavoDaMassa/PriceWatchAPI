namespace PriceWatch.Domain.Exceptions;

/// <summary>A "nova busca" do produto já voltou vazia vezes demais seguidas: não vale buscar de novo por enquanto.</summary>
public class NoMoreSimilarProductsException : BusinessException
{
    public NoMoreSimilarProductsException(string productId)
        : base($"No more similar products were found for '{productId}' for now. Try again later.") { }
}
