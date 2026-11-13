using System.Globalization;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Services;

namespace PriceWatch.Domain.Services;

/// <summary>
/// Textos do alerta (push, notificação no site e e-mail) no idioma do usuário (português se o locale
/// começa com "pt"; senão inglês). Os preços do produto são sempre em reais: só o separador numérico
/// segue o idioma.
/// </summary>
public static class AlertTexts
{
    /// <summary>Texto da notificação no site e do corpo do e-mail de alerta.</summary>
    public static string Message(string locale, NotificationType type, string productName, decimal price)
    {
        var portuguese = IsPortuguese(locale);
        var value = Price(portuguese, price);

        return (type, portuguese) switch
        {
            (NotificationType.TargetPriceReached, true) => $"'{productName}' atingiu o seu preço-alvo! Preço atual: {value}",
            (NotificationType.TargetPriceReached, false) => $"'{productName}' reached your target price! Current price: {value}",
            (NotificationType.NewLowestPrice, true) => $"'{productName}' chegou ao menor preço já visto: {value}",
            (NotificationType.NewLowestPrice, false) => $"'{productName}' hit a new lowest price: {value}",
            (_, true) => $"Atualização de preço de '{productName}': {value}",
            _ => $"Price update for '{productName}': {value}"
        };
    }

    /// <summary>Assunto do e-mail de alerta.</summary>
    public static string EmailSubject(string locale, NotificationType type, string productName)
    {
        var portuguese = IsPortuguese(locale);

        return (type, portuguese) switch
        {
            (NotificationType.TargetPriceReached, true) => $"PriceWatch: '{productName}' atingiu o seu preço-alvo!",
            (NotificationType.TargetPriceReached, false) => $"PriceWatch: '{productName}' reached your target price!",
            (_, true) => $"PriceWatch: novo menor preço para '{productName}'!",
            _ => $"PriceWatch: New lowest price for '{productName}'!"
        };
    }

    private static bool IsPortuguese(string locale) => locale.StartsWith("pt", StringComparison.OrdinalIgnoreCase);

    private static string Price(bool portuguese, decimal price) =>
        "R$ " + price.ToString("N2", CultureInfo.GetCultureInfo(portuguese ? "pt-BR" : "en-US"));

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
