namespace PriceWatch.Infrastructure.Settings;

/// <summary>
/// Liga/desliga comportamento novo do pivô multi-loja em produção. Tudo nasce desligado: a
/// migração do modelo legado acontece por etapas (ver docs/plano-v1.md), e cada etapa é
/// ativada por variável de ambiente (ex.: Features__BackfillEnabled=true) sem novo deploy de código.
/// </summary>
public class FeatureFlags
{
    public bool BackfillEnabled { get; set; }
    public bool MatchingEnabled { get; set; }
}
