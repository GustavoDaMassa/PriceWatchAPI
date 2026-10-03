using System.Globalization;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Services;

namespace PriceWatch.Domain.Services;

/// <summary>
/// Textos do alerta por push no idioma do usuário (português se o locale começa com "pt"; senão
/// inglês). Os preços do produto são sempre em reais: só o separador numérico segue o idioma.
/// </summary>
public static class AlertTexts
{
    public static PushMessage Push(string locale, NotificationType type, string productName, decimal price)
    {
        var portuguese = locale.StartsWith("pt", StringComparison.OrdinalIgnoreCase);
        var culture = CultureInfo.GetCultureInfo(portuguese ? "pt-BR" : "en-US");

        var title = (type, portuguese) switch
        {
            (NotificationType.TargetPriceReached, true) => "Preço-alvo atingido",
            (NotificationType.TargetPriceReached, false) => "Target price reached",
            (NotificationType.NewLowestPrice, true) => "Novo menor preço",
            (NotificationType.NewLowestPrice, false) => "New lowest price",
            (_, true) => "Atualização de preço",
            _ => "Price update"
        };

        return new PushMessage(
            title,
            $"{productName}: R$ {price.ToString("N2", culture)}",
            new Dictionary<string, string> { ["type"] = type.ToString() });
    }
}
