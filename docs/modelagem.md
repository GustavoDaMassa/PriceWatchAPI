# PriceWatch — Modelagem (Pivô Multi-Loja, Modo Produto)

> Diagramas formais exigidos pelo Modo Produto. Evoluem o modelo atual (`TrackedProduct` 1:1) para
> suportar comparação de produto entre lojas. `TrackedProduct` é mantido durante a transição (ver
> `arquitetura.md`, seção "Pivô Multi-Loja") — não aparece redefinido aqui, só o modelo-alvo novo.

## Diagrama de Classes

```mermaid
classDiagram
    class Product {
        +string Id
        +string? Gtin
        +string Name
        +string? Brand
        +string? Model
        +string? ImageUrl
        +MatchStatus MatchStatus
        +string? SuggestedMatchProductId
        +DateTime CreatedAt
        +SuggestMatch(string candidateId) void
        +Confirm() void
        +Reject() void
    }

    class MatchStatus {
        <<enumeration>>
        Unmatched
        AutoMatched
        PendingReview
        Confirmed
        Rejected
    }

    class StoreOffer {
        +string Id
        +string ProductId
        +ProductSource Source
        +string Url
        +string? Gtin
        +decimal CurrentPrice
        +decimal LowestPrice
        +string? AffiliateUrl
        +DateTime? LastCheckedAt
        +DateTime NextCheckAt
        +bool IsActive
        +RecordPrice(decimal price) PriceSnapshot
    }

    class ProductSource {
        <<enumeration>>
        MercadoLivre
        Kabum
        Manual
    }

    class UserProductSubscription {
        +string Id
        +string UserId
        +string ProductId
        +string? ListId
        +decimal TargetPrice
        +bool IsActive
        +bool TargetAlertSent
        +SetTargetPrice(decimal price) void
    }

    class IPriceFetcher {
        <<interface>>
        +ProductSource ProductSource
        +CanHandle(string url) bool
        +FetchAsync(string url) ProductFetchResult
    }

    class ProductFetchResult {
        <<record>>
        +decimal Price
        +string Name
        +string? ImageUrl
        +string? Gtin
        +string? Brand
        +string? Model
    }

    class IAffiliateLinkGenerator {
        <<interface>>
        +ProductSource ProductSource
        +CanHandle(string url) bool
        +GenerateAsync(string originalUrl) string
    }

    class MatchStoreOfferUseCase {
        +ExecuteAsync(string offerId, string productId) void
        -FindBestFuzzyMatch(Product product, candidates) Product?
        -MergeAsync(StoreOffer offer, Product source, Product target) void
    }

    class ProductMatchingConsumer {
        <<BackgroundService>>
        -ProcessEntryAsync(StreamEntry entry) void
    }

    class TitleSimilarity {
        <<static>>
        +Normalize(string title) string
        +Ratio(string a, string b) double
    }

    Product "1" *-- "N" StoreOffer : agrega
    Product --> MatchStatus
    Product "1" ..> "0..1" Product : SuggestedMatchProductId (fuzzy, não confirmado)
    StoreOffer --> ProductSource
    Product "1" <-- "N" UserProductSubscription : rastreado por
    StoreOffer ..> IPriceFetcher : alimentado por
    IPriceFetcher ..> ProductFetchResult : retorna
    StoreOffer ..> IAffiliateLinkGenerator : link resolvido por
    ProductMatchingConsumer ..> MatchStoreOfferUseCase : delega pra
    MatchStoreOfferUseCase ..> TitleSimilarity : usa no fallback sem GTIN
    MatchStoreOfferUseCase ..> Product : funde (GTIN) ou sugere (fuzzy)
    ProductMatchingConsumer ..> Product : cria/atualiza
    ProductMatchingConsumer ..> StoreOffer : consome evento de
```

## Diagrama de Objetos (cenário de exemplo)

Usuário rastreia um "iPhone 15 128GB" — adicionou primeiro pelo Mercado Livre, depois pela extensão
no Kabum. O matching casou as duas ofertas via GTIN.

```mermaid
classDiagram
    class product_iphone15 {
        <<Product instance>>
        Id = "prod-8f21"
        Gtin = "00190199719729"
        Name = "iPhone 15 128GB Azul"
        Brand = "Apple"
        Model = "A3092"
        MatchStatus = AutoMatched
    }

    class offer_ml {
        <<StoreOffer instance>>
        Id = "offer-a1"
        ProductId = "prod-8f21"
        Source = MercadoLivre
        Gtin = "00190199719729"
        CurrentPrice = 5299.00
        LowestPrice = 5099.00
        IsActive = true
    }

    class offer_kabum {
        <<StoreOffer instance>>
        Id = "offer-b2"
        ProductId = "prod-8f21"
        Source = Kabum
        Gtin = "00190199719729"
        CurrentPrice = 5149.90
        LowestPrice = 5149.90
        IsActive = true
    }

    class subscription_user1 {
        <<UserProductSubscription instance>>
        Id = "sub-c3"
        UserId = "user-42"
        ProductId = "prod-8f21"
        TargetPrice = 5000.00
        IsActive = true
    }

    product_iphone15 "1" *-- "2" offer_ml
    product_iphone15 *-- offer_kabum
    product_iphone15 "1" <-- "1" subscription_user1
```

Neste cenário, a tela de comparação (RF16) destaca `offer_kabum` (R$ 5.149,90) como a oferta mais
barata — o usuário ainda não atingiu o `TargetPrice` (R$ 5.000,00) em nenhuma das duas.

## Modelo Entidade-Relacionamento (MongoDB — coleções novas)

```mermaid
erDiagram
    products {
        string _id PK
        string gtin "nullable, indexado — chave de match"
        string name
        string brand "nullable"
        string model "nullable"
        string imageUrl "nullable"
        string matchStatus "enum: Unmatched|AutoMatched|PendingReview|Confirmed|Rejected"
        string suggestedMatchProductId "nullable — preenchido só no fuzzy match (PendingReview)"
        datetime createdAt
    }

    store_offers {
        string _id PK
        string productId FK
        int source "enum ProductSource"
        string url
        string gtin "nullable"
        decimal currentPrice
        decimal lowestPrice
        string affiliateUrl "nullable, cache"
        datetime lastCheckedAt "nullable"
        datetime nextCheckAt
        bool isActive
    }

    user_product_subscriptions {
        string _id PK
        string userId FK
        string productId FK
        string listId FK "nullable"
        decimal targetPrice
        bool isActive
        bool targetAlertSent
    }

    price_snapshots {
        string _id PK
        string offerId FK "antes productId — aponta pra store_offers agora"
        decimal price
        datetime capturedAt
    }

    users ||--o{ user_product_subscriptions : assina
    products ||--o{ store_offers : "agrega (1:N)"
    products ||--o{ user_product_subscriptions : "rastreado por (1:N)"
    store_offers ||--o{ price_snapshots : gera
```

### Índices novos necessários

| Coleção | Índice | Uso |
|---|---|---|
| `products` | `gtin asc` (não-único — pode haver `Product` sem GTIN ainda `PendingReview`) | Join do matching EAN-first |
| `store_offers` | `productId asc` | Carregar ofertas de um produto pra comparação |
| `store_offers` | `isActive asc, nextCheckAt asc` | Worker de verificação de preço (substitui o índice equivalente hoje em `tracked_products`) |
| `user_product_subscriptions` | `userId asc, productId asc` (único) | Evitar subscrição duplicada; consulta "meus produtos" |
| `price_snapshots` | `offerId asc, capturedAt desc` | Histórico por oferta (evolução do índice atual por `productId`) |

### Nota de convivência com `docs/schema.md`

As coleções `users`, `product_lists`, `tracked_products`, `price_snapshots` (legado) e `notifications`
documentadas em `schema.md` **continuam existindo sem alteração** durante a transição (ver decisão
de migração aditiva em `arquitetura.md`). Este documento descreve o **schema-alvo** das coleções
novas; `schema.md` só será atualizado para refletir o estado real depois que o código da Fase 4 for
implementado — não antes, para não documentar como "implementado" algo que ainda não existe.

> Atualização de 02/10/2026: a Fase 4 já está implementada e `schema.md` agora documenta `products`,
> `store_offers` e `user_product_subscriptions` com os campos e índices reais. As coleções da v1 (abaixo)
> aparecem em `schema.md` apenas como "planejadas".

---

# Modelagem da v1 (02/10/2026)

> Acrescenta ao modelo-alvo as entidades do fluxo da v1: descoberta em outras lojas, confirmação do usuário,
> cliques, push. Nada abaixo está implementado. Ver `arquitetura.md` ("Arquitetura da v1").

## Diagrama de Classes (v1)

```mermaid
classDiagram
    class OfferCandidate {
        +string Id
        +string ProductId
        +ProductSource Source
        +string Url
        +ProductFetchResult Snapshot
        +double Score
        +CandidateReason Reason
        +DateTime ExpiresAt
    }

    class CandidateReason {
        <<enumeration>>
        Gtin
        Keyword
    }

    class OfferMatchFeedback {
        +string Id
        +string UserId
        +string CandidateId
        +string ProductId
        +string Url
        +MatchVerdict Verdict
        +bool Track
        +DateTime CreatedAt
    }

    class MatchVerdict {
        <<enumeration>>
        Same
        NotSame
    }

    class ClickLog {
        +string Id
        +string OfferId
        +string UserId
        +DateTime CreatedAt
    }

    class DeviceToken {
        +string Id
        +string UserId
        +string Token
        +string Platform
        +DateTime CreatedAt
    }

    class IOfferSearcher {
        <<interface>>
        +ProductSource ProductSource
        +SearchByGtinAsync(string gtin) ProductFetchResult[]
        +SearchByKeywordAsync(string brand, string model, string name) ProductFetchResult[]
    }

    class IStoreCapabilities {
        <<interface>>
        +For(ProductSource source) StoreCapabilities
    }

    class StoreCapabilities {
        <<record>>
        +bool CanFetchByUrl
        +bool CanSearchByGtin
        +bool CanSearchByKeyword
        +bool CanGenerateAffiliateLink
    }

    class IPushSender {
        <<interface>>
        +SendAsync(string userId, string title, string body) void
    }

    class ImportFromLinkUseCase {
        +ExecuteAsync(string userId, string url) OfferResponse
    }

    class DiscoverOffersUseCase {
        +ExecuteAsync(string userId, string productId) OfferCandidate[]
    }

    class ConfirmOfferMatchUseCase {
        +ExecuteAsync(string userId, string candidateId, bool isSame, bool track, decimal? targetPrice) void
    }

    Product "1" <-- "N" OfferCandidate : sugerida para
    OfferCandidate --> CandidateReason
    OfferCandidate "1" <-- "N" OfferMatchFeedback : julgada por
    OfferMatchFeedback --> MatchVerdict
    StoreOffer "1" <-- "N" ClickLog : clicada em
    DiscoverOffersUseCase ..> IOfferSearcher : consulta
    DiscoverOffersUseCase ..> IStoreCapabilities : filtra lojas
    DiscoverOffersUseCase ..> OfferCandidate : cria
    DiscoverOffersUseCase ..> TitleSimilarity : pontua sem GTIN
    ConfirmOfferMatchUseCase ..> OfferMatchFeedback : grava
    ConfirmOfferMatchUseCase ..> MatchStoreOfferUseCase : reaproveita a fusão
    ImportFromLinkUseCase ..> CreateOfferUseCase : delega
    IStoreCapabilities --> StoreCapabilities
    DeviceToken ..> IPushSender : alvo do push
```

## Modelo Entidade-Relacionamento (v1 — coleções novas)

```mermaid
erDiagram
    offer_candidates {
        string _id PK
        string productId FK
        int source "enum ProductSource"
        string url
        string name
        decimal price
        string gtin "nullable"
        string brand "nullable"
        string model "nullable"
        string imageUrl "nullable"
        double score
        string reason "enum: Gtin|Keyword"
        datetime expiresAt "TTL"
    }

    offer_match_feedback {
        string _id PK
        string userId FK
        string candidateId FK
        string productId FK
        string url
        string verdict "enum: Same|NotSame"
        bool track
        datetime createdAt
    }

    click_logs {
        string _id PK
        string offerId FK
        string userId FK
        datetime createdAt
    }

    device_tokens {
        string _id PK
        string userId FK
        string token "único"
        string platform "android"
        datetime createdAt
    }

    products ||--o{ offer_candidates : "sugerida para"
    offer_candidates ||--o{ offer_match_feedback : "julgada por"
    users ||--o{ offer_match_feedback : "julga"
    store_offers ||--o{ click_logs : "clicada em"
    users ||--o{ click_logs : "clica"
    users ||--o{ device_tokens : "possui"
```

### Índices novos necessários (v1)

| Coleção | Índice | Uso |
|---|---|---|
| `offer_candidates` | `productId asc` | Listar candidatas de um produto |
| `offer_candidates` | `expiresAt` com TTL | Expirar candidatas antigas |
| `offer_candidates` | `productId+source+url` (único) | Evitar candidata duplicada |
| `offer_match_feedback` | `userId+candidateId` (único) | Um voto por usuário e candidata |
| `click_logs` | `offerId+createdAt` | Relatório de cliques por oferta |
| `device_tokens` | `userId asc`; `token` (único) | Enviar push a todos os dispositivos do usuário |

### Cenário de exemplo (fluxo da v1)

A usuária cola um link do Mercado Livre de um "Notebook Asus Vivobook 15 256GB". A API cria o `Product` e a
`StoreOffer` do ML e gera o link de afiliado. A descoberta procura nas outras lojas: acha duas candidatas — uma
com o mesmo GTIN (pré-selecionada, `reason = Gtin`) e outra por palavra-chave com marca e capacidade iguais
(`reason = Keyword`, `score` menor). Ela confirma a primeira ("é o mesmo produto" e "acompanhar") e rejeita a segunda
("não é"). A API grava os dois `OfferMatchFeedback`, cria a `StoreOffer` da loja confirmada, funde no mesmo
`Product` (`MatchStatus = Confirmed`) e passa a monitorar as duas ofertas. A tela de comparação (RF16) mostra as
duas ofertas lado a lado.
