namespace PriceWatch.Domain.Enums;

/// <summary>Como uma oferta candidata foi encontrada. Valores persistidos: só crescem no final.</summary>
public enum CandidateReason
{
    /// <summary>GTIN idêntico ao do produto acompanhado (alta confiança; vem pré-selecionada na tela).</summary>
    Gtin,

    /// <summary>Busca por palavra-chave e pontuação por marca, modelo, atributos e título (o usuário confirma).</summary>
    Keyword
}
