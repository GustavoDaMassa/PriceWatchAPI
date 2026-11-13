namespace PriceWatch.Domain.ValueObjects;

/// <summary>Veredito do juiz de IA sobre uma candidata: <c>Index</c> é a posição dela na lista enviada.</summary>
public record MatchJudgement(int Index, bool SameProduct, string Reason);
