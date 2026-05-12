using System.Reflection;

namespace PriceWatch.Infrastructure.Email;

public class EmailTemplateRenderer
{
    private static readonly Assembly Assembly = Assembly.GetExecutingAssembly();

    public string Render(string templateName, string locale, Dictionary<string, string> values)
    {
        var resourceName = FindResource(templateName, locale)
            ?? FindResource(templateName, "en")
            ?? throw new InvalidOperationException($"Email template '{templateName}' not found.");

        using var stream = Assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);

        return values.Aggregate(reader.ReadToEnd(),
            (html, kv) => html.Replace($"{{{{{kv.Key}}}}}", kv.Value));
    }

    private static string? FindResource(string templateName, string locale)
    {
        var name = $"PriceWatch.Infrastructure.Email.Templates.{templateName}.{locale}.html";
        return Assembly.GetManifestResourceNames().Contains(name) ? name : null;
    }
}
