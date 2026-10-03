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

    /// <summary>
    /// Cutover do monitoramento para o catálogo unificado (Product/StoreOffer/Subscription): os
    /// endpoints legados leem/escrevem no catálogo novo, o <c>StoreOfferPriceCheckWorker</c> assume
    /// o monitoramento e o <c>PriceCheckWorker</c> legado fica ocioso. Rollback: voltar para false.
    /// </summary>
    public bool UnifiedCatalogEnabled { get; set; }

    // o catálogo unificado precisa do backfill (idempotente) e do matching rodando
    public bool BackfillActive => BackfillEnabled || UnifiedCatalogEnabled;
    public bool MatchingActive => MatchingEnabled || UnifiedCatalogEnabled;
}
