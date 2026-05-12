using System.Reflection;

namespace PriceWatch.Infrastructure.Email;

public class EmailTemplateRenderer
{
    private static readonly Assembly Assembly = Assembly.GetExecutingAssembly();

    public string Render(string templateName, Dictionary<string, string> values)
    {
        var resourceName = $"PriceWatch.Infrastructure.Email.Templates.{templateName}.html";

        using var stream = Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Email template '{templateName}' not found.");
        using var reader = new StreamReader(stream);

        return values.Aggregate(reader.ReadToEnd(),
            (html, kv) => html.Replace($"{{{{{kv.Key}}}}}", kv.Value));
    }
}
