# PriceWatch — Mapa de Classes

---

<details id="dir-root">
<summary><strong>/ (raiz)</strong></summary>
<blockquote>

- [PriceWatch.sln](../PriceWatch.sln) — solução .NET 8
- [docker-compose.yml](../docker-compose.yml) — MongoDB 7, Redis 7, MailHog
- [.env.example](../.env.example) — variáveis de ambiente necessárias
- [.editorconfig](../.editorconfig) — estilo de código C#
- [.gitignore](../.gitignore)
- [Erros.md](../Erros.md) — bugs corrigidos
- [docs/schema.md](schema.md) — schema MongoDB, índices e regras de integridade

</blockquote>
</details>

---

## src/

<details id="dir-domain">
<summary><strong>PriceWatch.Domain/</strong></summary>
<blockquote>

<details id="dir-domain-entities">
<summary><strong>Entities/</strong></summary>
<blockquote>

<details id="User">
<summary><strong><a href="../src/PriceWatch.Domain/Entities/User.cs">User.cs</a> [class]</strong></summary>
<blockquote>

**atributos:** `Id`, `Name`, `Email`, `PasswordHash`, `IsEmailVerified`, `EmailVerificationToken`, `TokenExpiresAt`, `CreatedAt` — todos `private set`

**métodos:** `Create(name, email, passwordHash, verificationToken, tokenExpiresAt?)`, `Restore(...)`, `VerifyEmail(token)`, `RegenerateVerificationToken(newToken, expiresAt?)`

</blockquote>
</details>

<details id="ProductList">
<summary><strong><a href="../src/PriceWatch.Domain/Entities/ProductList.cs">ProductList.cs</a> [class]</strong></summary>
<blockquote>

**atributos:** `Id`, `UserId`, `Name`, `Description?`, `CreatedAt` — todos `private set`

**métodos:** `Create(userId, name, description)`, `Restore(id, userId, name, description, createdAt)`, `Update(name, description)`

</blockquote>
</details>

<details id="PriceSnapshot">
<summary><strong><a href="../src/PriceWatch.Domain/Entities/PriceSnapshot.cs">PriceSnapshot.cs</a> [class]</strong></summary>
<blockquote>

**atributos:** `Id`, `ProductId`, `Price`, `CapturedAt` — todos `private set`

**métodos:** `Create(productId, price)`, `Restore(id, productId, price, capturedAt)`

</blockquote>
</details>

<details id="TrackedProduct">
<summary><strong><a href="../src/PriceWatch.Domain/Entities/TrackedProduct.cs">TrackedProduct.cs</a> [class]</strong></summary>
<blockquote>

**atributos:** `Id`, `ListId`, `UserId`, `Url`, `Source`, `Name`, `ImageUrl?`, `TargetPrice`, `CurrentPrice`, `LowestPrice`, `CheckIntervalHours`, `NextCheckAt`, `LastCheckedAt?`, `IsActive`, `Metadata`, `CreatedAt` — todos `private set`

**métodos:** `Create(listId, userId, url, source, name, targetPrice)`, `Restore(...)`, `RecordPrice(price) → PriceSnapshot`, `ShouldTriggerTargetAlert()`, `ShouldTriggerLowestAlert(previousLowest)`, `Deactivate()`, `MarkMigratedToProduct(productId)` / `IsMigratedToProduct()` (Fase 4 — marca de idempotência do backfill pro pivô multi-loja, ver [BackfillTrackedProductsUseCase](#BackfillTrackedProductsUseCase))

</blockquote>
</details>

<details id="Notification">
<summary><strong><a href="../src/PriceWatch.Domain/Entities/Notification.cs">Notification.cs</a> [class]</strong></summary>
<blockquote>

**atributos:** `Id`, `UserId`, `ProductId`, `ProductName`, `Type`, `Message`, `IsRead`, `CreatedAt` — todos `private set`

**métodos:** `Create(userId, productId, productName, type, currentPrice)` — gera Message pelo type; `Restore(...)`, `MarkAsRead()`

</blockquote>
</details>

<details id="Product">
<summary><strong><a href="../src/PriceWatch.Domain/Entities/Product.cs">Product.cs</a> [class]</strong></summary>
<blockquote>

**atributos:** `Id`, `Gtin?`, `Name`, `Brand?`, `Model?`, `ImageUrl?`, `MatchStatus`, `SuggestedMatchProductId?`, `CreatedAt` — todos `private set`

**métodos:** `Create(name, gtin?, brand?, model?, imageUrl?)`, `Restore(...)`, `MarkAutoMatched()`, `SuggestMatch(candidateProductId)` — nunca funde, só marca `PendingReview` + grava o candidato (RF15/RI07), `Confirm()`, `Reject()`, `IsVisibleInComparison()` — `true` pra `Unmatched/AutoMatched/Confirmed`

**uso:** produto-mestre do pivô multi-loja (Fase 4) — catálogo compartilhado entre usuários, agrega N [StoreOffer](#StoreOffer)

</blockquote>
</details>

<details id="StoreOffer">
<summary><strong><a href="../src/PriceWatch.Domain/Entities/StoreOffer.cs">StoreOffer.cs</a> [class]</strong></summary>
<blockquote>

**atributos:** `Id`, `ProductId`, `Source`, `Url`, `Gtin?`, `CurrentPrice`, `LowestPrice`, `AffiliateUrl?`, `CheckIntervalHours`, `NextCheckAt`, `LastCheckedAt?`, `IsActive`, `CreatedAt` — todos `private set`

**métodos:** `Create(productId, source, url, gtin?)`, `Restore(...)`, `RecordPrice(price) → PriceSnapshot` (reaproveita a coleção `price_snapshots` do [TrackedProduct](#TrackedProduct) legado), `SetAffiliateUrl(url)`, `Activate()`, `Deactivate()`, `ReassignToProduct(productId)` — usado pelo motor de matching pra unificar ofertas do mesmo produto físico

**uso:** oferta de uma loja específica pra um [Product](#Product) (Fase 4)

</blockquote>
</details>

<details id="UserProductSubscription">
<summary><strong><a href="../src/PriceWatch.Domain/Entities/UserProductSubscription.cs">UserProductSubscription.cs</a> [class]</strong></summary>
<blockquote>

**atributos:** `Id`, `UserId`, `ProductId`, `ListId?`, `TargetPrice`, `IsActive`, `TargetAlertSent`, `CreatedAt` — todos `private set`

**métodos:** `Create(userId, productId, targetPrice, listId?)`, `Restore(...)`, `SetTargetPrice(price)`, `AssignToList(listId)`, `RemoveFromList()`, `Activate()`, `Deactivate()`, `ShouldTriggerTargetAlert(currentPrice)`, `MarkTargetAlertSent()`, `ReassignToProduct(productId)`

**uso:** vínculo por usuário com um [Product](#Product) (preço-alvo, alertas, lista) — substitui o papel "por usuário" que o [TrackedProduct](#TrackedProduct) legado carregava sozinho (Fase 4)

</blockquote>
</details>

</blockquote>
</details>

<details id="dir-domain-enums">
<summary><strong>Enums/</strong></summary>
<blockquote>

- [ProductSource.cs](../src/PriceWatch.Domain/Enums/ProductSource.cs) — `MercadoLivre, Kabum, Manual`
- [NotificationType.cs](../src/PriceWatch.Domain/Enums/NotificationType.cs) — `TargetPriceReached, NewLowestPrice`
- [MatchStatus.cs](../src/PriceWatch.Domain/Enums/MatchStatus.cs) — `Unmatched, AutoMatched, PendingReview, Confirmed, Rejected` (Fase 4 — pivô multi-loja)

</blockquote>
</details>

<details id="dir-domain-valueobjects">
<summary><strong>ValueObjects/</strong></summary>
<blockquote>

- [ProductFetchResult.cs](../src/PriceWatch.Domain/ValueObjects/ProductFetchResult.cs) — `record(Price, Name, ImageUrl?, Gtin?, Brand?, Model?)` — retorno de `IPriceFetcher.FetchAsync`; `Gtin`/`Brand`/`Model` adicionados na Fase 3 (aditivo, mudança compatível com o único uso existente)

</blockquote>
</details>

<details id="dir-domain-exceptions">
<summary><strong>Exceptions/</strong></summary>
<blockquote>

<details id="NotFoundException">
<summary><strong><a href="../src/PriceWatch.Domain/Exceptions/NotFoundException.cs">NotFoundException.cs</a> [abstract class]</strong></summary>
<blockquote>

**extends:** `Exception`

**construtor:**
- `protected NotFoundException(string message)`

**uso:** base para todas as exceções de recurso não encontrado no domínio

</blockquote>
</details>

<details id="BusinessException">
<summary><strong><a href="../src/PriceWatch.Domain/Exceptions/BusinessException.cs">BusinessException.cs</a> [class]</strong></summary>
<blockquote>

**extends:** `Exception`

**construtor:**
- `public BusinessException(string message)`

**uso:** violações de regra de negócio (ex: email já cadastrado, token expirado)

</blockquote>
</details>

<details id="UserNotFoundException">
<summary><strong><a href="../src/PriceWatch.Domain/Exceptions/UserNotFoundException.cs">UserNotFoundException.cs</a> [class]</strong></summary>
<blockquote>

**extends:** [NotFoundException](#NotFoundException)

**construtores:** `(string email)`, `(Guid id)`

</blockquote>
</details>

<details id="ProductListNotFoundException">
<summary><strong><a href="../src/PriceWatch.Domain/Exceptions/ProductListNotFoundException.cs">ProductListNotFoundException.cs</a> [class]</strong></summary>
<blockquote>

**extends:** [NotFoundException](#NotFoundException)

**construtor:** `(string identifier)`

</blockquote>
</details>

<details id="TrackedProductNotFoundException2">
<summary><strong><a href="../src/PriceWatch.Domain/Exceptions/TrackedProductNotFoundException.cs">TrackedProductNotFoundException.cs</a> [class]</strong></summary>
<blockquote>

**extends:** [NotFoundException](#NotFoundException)

**construtor:** `(string identifier)`

</blockquote>
</details>

<details id="NotificationNotFoundException">
<summary><strong><a href="../src/PriceWatch.Domain/Exceptions/NotificationNotFoundException.cs">NotificationNotFoundException.cs</a> [class]</strong></summary>
<blockquote>

**extends:** [NotFoundException](#NotFoundException)

**construtor:** `(string identifier)`

</blockquote>
</details>

<details id="ProductNotFoundException">
<summary><strong><a href="../src/PriceWatch.Domain/Exceptions/ProductNotFoundException.cs">ProductNotFoundException.cs</a> [class]</strong></summary>
<blockquote>

**extends:** [NotFoundException](#NotFoundException)

**construtor:** `(string identifier)` — Fase 4, pivô multi-loja

</blockquote>
</details>

<details id="StoreOfferNotFoundException">
<summary><strong><a href="../src/PriceWatch.Domain/Exceptions/StoreOfferNotFoundException.cs">StoreOfferNotFoundException.cs</a> [class]</strong></summary>
<blockquote>

**extends:** [NotFoundException](#NotFoundException)

**construtor:** `(string identifier)` — Fase 4, pivô multi-loja

</blockquote>
</details>

</blockquote>
</details>

<details id="dir-domain-interfaces-repositories">
<summary><strong>Interfaces/Repositories/</strong></summary>
<blockquote>

<details id="IUserRepository">
<summary><strong><a href="../src/PriceWatch.Domain/Interfaces/Repositories/IUserRepository.cs">IUserRepository.cs</a> [interface]</strong></summary>
<blockquote>

**métodos:** `GetByEmailAsync(email)`, `GetByIdAsync(id)`, `CreateAsync(user)`, `UpdateAsync(user)`

</blockquote>
</details>

<details id="IProductListRepository">
<summary><strong><a href="../src/PriceWatch.Domain/Interfaces/Repositories/IProductListRepository.cs">IProductListRepository.cs</a> [interface]</strong></summary>
<blockquote>

**métodos:** `GetByUserIdAsync(userId)`, `GetByIdAsync(id)`, `CreateAsync(list)`, `UpdateAsync(list)`, `DeleteAsync(id)`

</blockquote>
</details>

<details id="ITrackedProductRepository">
<summary><strong><a href="../src/PriceWatch.Domain/Interfaces/Repositories/ITrackedProductRepository.cs">ITrackedProductRepository.cs</a> [interface]</strong></summary>
<blockquote>

**métodos (doc pré-pivot incompleta — só o essencial listado):** `GetByListIdAsync(listId)`, `GetByIdAsync(id)`, `GetDueForCheckAsync()`, `CreateAsync(product)`, `UpdateAsync(product)`, `DeleteAsync(id)`

**adicionado na Fase 4 (pivô):** `GetAllAsync()` — usado por [BackfillTrackedProductsUseCase](#BackfillTrackedProductsUseCase) pra migrar todo o legado, independente de dono/status

</blockquote>
</details>

<details id="IPriceSnapshotRepository">
<summary><strong><a href="../src/PriceWatch.Domain/Interfaces/Repositories/IPriceSnapshotRepository.cs">IPriceSnapshotRepository.cs</a> [interface]</strong></summary>
<blockquote>

**métodos:** `CreateAsync(snapshot)`, `GetByProductIdAsync(productId, limit=100)`, `DeleteByProductIdAsync(productId)`

</blockquote>
</details>

<details id="IProductRepository">
<summary><strong><a href="../src/PriceWatch.Domain/Interfaces/Repositories/IProductRepository.cs">IProductRepository.cs</a> [interface]</strong></summary>
<blockquote>

**métodos:** `GetByIdAsync(id)`, `GetByGtinAsync(gtin, excludeId?)`, `GetWithoutGtinAsync(excludeId)`, `CreateAsync(product)`, `UpdateAsync(product)`

**uso:** Fase 4 (pivô) — [ProductRepository](#ProductRepository) é a implementação Mongo

</blockquote>
</details>

<details id="IStoreOfferRepository">
<summary><strong><a href="../src/PriceWatch.Domain/Interfaces/Repositories/IStoreOfferRepository.cs">IStoreOfferRepository.cs</a> [interface]</strong></summary>
<blockquote>

**métodos:** `GetByIdAsync(id)`, `GetBySourceAndUrlAsync(source, url)` — dedup na criação, `GetByProductIdAsync(productId)`, `GetDueForCheckAsync()`, `CreateAsync(offer)`, `UpdateAsync(offer)`

</blockquote>
</details>

<details id="IUserProductSubscriptionRepository">
<summary><strong><a href="../src/PriceWatch.Domain/Interfaces/Repositories/IUserProductSubscriptionRepository.cs">IUserProductSubscriptionRepository.cs</a> [interface]</strong></summary>
<blockquote>

**métodos:** `GetByIdAsync(id)`, `GetByUserIdAndProductIdAsync(userId, productId)`, `GetByProductIdAsync(productId)`, `GetByUserIdAsync(userId, listId?)`, `CreateAsync(subscription)`, `UpdateAsync(subscription)`, `DeleteAsync(id)`, `UnlinkByListIdAsync(listId)`

</blockquote>
</details>

</blockquote>
</details>

<details id="dir-domain-interfaces-services">
<summary><strong>Interfaces/Services/</strong></summary>
<blockquote>

- [IPasswordHasher.cs](../src/PriceWatch.Domain/Interfaces/Services/IPasswordHasher.cs) — `Hash(password)`, `Verify(password, hash)`
- [ITokenService.cs](../src/PriceWatch.Domain/Interfaces/Services/ITokenService.cs) — `GenerateToken(user)`
- [IEmailSender.cs](../src/PriceWatch.Domain/Interfaces/Services/IEmailSender.cs) — `SendVerificationEmailAsync(email, name, token)`
- [IPriceFetcher.cs](../src/PriceWatch.Domain/Interfaces/Services/IPriceFetcher.cs) — `string Source { get; }`, `FetchAsync(url) → decimal`
- [IAlertPublisher.cs](../src/PriceWatch.Domain/Interfaces/Services/IAlertPublisher.cs) — `PublishAsync(productId, userId, productName, type, currentPrice)`
- [IAffiliateLinkGenerator.cs](../src/PriceWatch.Domain/Interfaces/Services/IAffiliateLinkGenerator.cs) — `ProductSource ProductSource { get; }`, `CanHandle(url)`, `GenerateAsync(originalUrl) → string` (Fase 4/6 — pivô)
- [IProductMatchingEventPublisher.cs](../src/PriceWatch.Domain/Interfaces/Services/IProductMatchingEventPublisher.cs) — `PublishAsync(offerId, productId)` (Fase 5 — pivô)

</blockquote>
</details>

<details id="dir-domain-services">
<summary><strong>Services/</strong></summary>
<blockquote>

<details id="TitleSimilarity">
<summary><strong><a href="../src/PriceWatch.Domain/Services/TitleSimilarity.cs">TitleSimilarity.cs</a> [static partial class]</strong></summary>
<blockquote>

**métodos:** `Normalize(title) → string` (lowercase, remove pontuação, colapsa espaços), `Ratio(a, b) → double` (0–1, baseado em distância de Levenshtein)

**uso:** fallback de fuzzy matching quando não há GTIN (Fase 5) — usado por [MatchStoreOfferUseCase](#MatchStoreOfferUseCase); não é o algoritmo final, é o ponto de extensão v1 (ver docs/arquitetura.md)

</blockquote>
</details>

</blockquote>
</details>

</blockquote>
</details>

<details id="dir-application">
<summary><strong>PriceWatch.Application/</strong></summary>
<blockquote>

<details id="dir-application-dtos-auth">
<summary><strong>DTOs/Auth/</strong></summary>
<blockquote>

- [RegisterRequest.cs](../src/PriceWatch.Application/DTOs/Auth/RegisterRequest.cs) — `record(Name, Email, Password)`
- [LoginRequest.cs](../src/PriceWatch.Application/DTOs/Auth/LoginRequest.cs) — `record(Email, Password)`
- [AuthResponse.cs](../src/PriceWatch.Application/DTOs/Auth/AuthResponse.cs) — `record(Token)`

</blockquote>
</details>

<details id="dir-application-interfaces">
<summary><strong>Interfaces/</strong></summary>
<blockquote>

- [IPriceFetcherResolver.cs](../src/PriceWatch.Application/Interfaces/IPriceFetcherResolver.cs) — `Resolve(ProductSource) → IPriceFetcher`
- [IAffiliateLinkResolver.cs](../src/PriceWatch.Application/Interfaces/IAffiliateLinkResolver.cs) — `Resolve(ProductSource) → IAffiliateLinkGenerator`, `Resolve(url) → IAffiliateLinkGenerator` — nunca lança, cai no passthrough se nada específico for achado (Fase 4/6)

</blockquote>
</details>

<details id="dir-application-dtos-productlist">
<summary><strong>DTOs/ProductList/</strong></summary>
<blockquote>

- [CreateProductListRequest.cs](../src/PriceWatch.Application/DTOs/ProductList/CreateProductListRequest.cs) — `record(Name, Description?)`
- [UpdateProductListRequest.cs](../src/PriceWatch.Application/DTOs/ProductList/UpdateProductListRequest.cs) — `record(Name, Description?)`
- [ProductListResponse.cs](../src/PriceWatch.Application/DTOs/ProductList/ProductListResponse.cs) — `record(Id, Name, Description?, CreatedAt)`
- [AnalysisItemDto.cs](../src/PriceWatch.Application/DTOs/ProductList/AnalysisItemDto.cs) — `record(ProductId, ProductName, CurrentPrice, TargetPrice, DistancePercent)`

</blockquote>
</details>

<details id="dir-application-dtos-trackedproduct">
<summary><strong>DTOs/TrackedProduct/</strong></summary>
<blockquote>

- [AddProductRequest.cs](../src/PriceWatch.Application/DTOs/TrackedProduct/AddProductRequest.cs) — `record(ListId, Url, Source, Name, TargetPrice)`
- [UpdateProductRequest.cs](../src/PriceWatch.Application/DTOs/TrackedProduct/UpdateProductRequest.cs) — `record(TargetPrice, IsActive)`
- [TrackedProductResponse.cs](../src/PriceWatch.Application/DTOs/TrackedProduct/TrackedProductResponse.cs) — `record(Id, ListId, Name, Url, Source, TargetPrice, CurrentPrice, LowestPrice, IsActive, NextCheckAt)`
- [PriceSnapshotResponse.cs](../src/PriceWatch.Application/DTOs/TrackedProduct/PriceSnapshotResponse.cs) — `record(Id, Price, CapturedAt)`

</blockquote>
</details>

<details id="dir-application-dtos-notification">
<summary><strong>DTOs/Notification/</strong></summary>
<blockquote>

- [NotificationResponse.cs](../src/PriceWatch.Application/DTOs/Notification/NotificationResponse.cs) — `record(Id, ProductId, ProductName, Type, Message, IsRead, CreatedAt)`
- [AlertEvent.cs](../src/PriceWatch.Application/DTOs/Notification/AlertEvent.cs) — `record(ProductId, UserId, ProductName, Type, CurrentPrice, UserEmail)` — usado pelo Redis Streams

</blockquote>
</details>

<details id="dir-application-dtos-offer">
<summary><strong>DTOs/Offer/ (Fase 4 — pivô multi-loja)</strong></summary>
<blockquote>

- [CreateOfferRequest.cs](../src/PriceWatch.Application/DTOs/Offer/CreateOfferRequest.cs) — `record(Url, TargetPrice, ListId?)`
- [OfferResponse.cs](../src/PriceWatch.Application/DTOs/Offer/OfferResponse.cs) — `record(SubscriptionId, ProductId, OfferId, Name, Url, ImageUrl?, Source, TargetPrice, CurrentPrice, LowestPrice, IsActive, NextCheckAt)`
- [ProductComparisonResponse.cs](../src/PriceWatch.Application/DTOs/Offer/ProductComparisonResponse.cs) — `record(ProductId, Name, ImageUrl?, Offers: IReadOnlyList<StoreOfferSummary>)` + `record StoreOfferSummary(OfferId, Source, CurrentPrice, IsCheapest)`
- [StoreOfferCreatedEvent.cs](../src/PriceWatch.Application/DTOs/Offer/StoreOfferCreatedEvent.cs) — `record(OfferId, ProductId)` — payload do stream `store-offer-events` (Fase 5)

</blockquote>
</details>

<details id="dir-application-usecases-auth">
<summary><strong>UseCases/Auth/</strong></summary>
<blockquote>

- [RegisterUseCase.cs](../src/PriceWatch.Application/UseCases/Auth/RegisterUseCase.cs) — `ExecuteAsync(RegisterRequest)`
- [LoginUseCase.cs](../src/PriceWatch.Application/UseCases/Auth/LoginUseCase.cs) — `ExecuteAsync(LoginRequest) → AuthResponse`
- [VerifyEmailUseCase.cs](../src/PriceWatch.Application/UseCases/Auth/VerifyEmailUseCase.cs) — `ExecuteAsync(email, token)`
- [ResendVerificationUseCase.cs](../src/PriceWatch.Application/UseCases/Auth/ResendVerificationUseCase.cs) — `ExecuteAsync(email)`

</blockquote>
</details>

<details id="dir-application-usecases-productlist">
<summary><strong>UseCases/ProductList/</strong></summary>
<blockquote>

<details id="CreateListUseCase">
<summary><strong><a href="../src/PriceWatch.Application/UseCases/ProductList/CreateListUseCase.cs">CreateListUseCase.cs</a> [class]</strong></summary>
<blockquote>

**dependências:** [IProductListRepository](#IProductListRepository)

**métodos:** `ExecuteAsync(userId, CreateProductListRequest) → ProductListResponse`

</blockquote>
</details>

<details id="GetUserListsUseCase">
<summary><strong><a href="../src/PriceWatch.Application/UseCases/ProductList/GetUserListsUseCase.cs">GetUserListsUseCase.cs</a> [class]</strong></summary>
<blockquote>

**dependências:** [IProductListRepository](#IProductListRepository)

**métodos:** `ExecuteAsync(userId) → IEnumerable<ProductListResponse>`

</blockquote>
</details>

<details id="UpdateListUseCase">
<summary><strong><a href="../src/PriceWatch.Application/UseCases/ProductList/UpdateListUseCase.cs">UpdateListUseCase.cs</a> [class]</strong></summary>
<blockquote>

**dependências:** [IProductListRepository](#IProductListRepository)

**métodos:** `ExecuteAsync(listId, userId, UpdateProductListRequest)` — lança [ProductListNotFoundException](#ProductListNotFoundException) se não existe ou ownership inválida

</blockquote>
</details>

<details id="DeleteListUseCase">
<summary><strong><a href="../src/PriceWatch.Application/UseCases/ProductList/DeleteListUseCase.cs">DeleteListUseCase.cs</a> [class]</strong></summary>
<blockquote>

**dependências:** [IProductListRepository](#IProductListRepository)

**métodos:** `ExecuteAsync(listId, userId)` — verifica ownership antes de deletar

</blockquote>
</details>

<details id="GetListAnalysisUseCase">
<summary><strong><a href="../src/PriceWatch.Application/UseCases/ProductList/GetListAnalysisUseCase.cs">GetListAnalysisUseCase.cs</a> [class]</strong></summary>
<blockquote>

**dependências:** [IProductListRepository](#IProductListRepository) — `ITrackedProductRepository` será adicionado no domínio TrackedProduct

**métodos:** `ExecuteAsync(listId, userId) → IEnumerable<AnalysisItemDto>` — stub retorna vazio; implementação completa no domínio 5.2

</blockquote>
</details>

</blockquote>
</details>

<details id="dir-application-usecases-trackedproduct">
<summary><strong>UseCases/TrackedProduct/</strong></summary>
<blockquote>

<details id="AddProductUseCase">
<summary><strong><a href="../src/PriceWatch.Application/UseCases/TrackedProduct/AddProductUseCase.cs">AddProductUseCase.cs</a> [class]</strong></summary>
<blockquote>

**dependências:** [ITrackedProductRepository](#ITrackedProductRepository), [IPriceFetcherResolver](#IPriceFetcherResolver)

**métodos:** `ExecuteAsync(userId, AddProductRequest) → TrackedProductResponse` — fetcha preço inicial, chama `RecordPrice`

</blockquote>
</details>

<details id="GetProductsByListUseCase">
<summary><strong><a href="../src/PriceWatch.Application/UseCases/TrackedProduct/GetProductsByListUseCase.cs">GetProductsByListUseCase.cs</a> [class]</strong></summary>
<blockquote>

**dependências:** [ITrackedProductRepository](#ITrackedProductRepository)

**métodos:** `ExecuteAsync(listId) → IEnumerable<TrackedProductResponse>`

</blockquote>
</details>

<details id="UpdateProductUseCase">
<summary><strong><a href="../src/PriceWatch.Application/UseCases/TrackedProduct/UpdateProductUseCase.cs">UpdateProductUseCase.cs</a> [class]</strong></summary>
<blockquote>

**dependências:** [ITrackedProductRepository](#ITrackedProductRepository)

**métodos:** `ExecuteAsync(productId, userId, UpdateProductRequest)` — verifica ownership

</blockquote>
</details>

<details id="RemoveProductUseCase">
<summary><strong><a href="../src/PriceWatch.Application/UseCases/TrackedProduct/RemoveProductUseCase.cs">RemoveProductUseCase.cs</a> [class]</strong></summary>
<blockquote>

**dependências:** [ITrackedProductRepository](#ITrackedProductRepository)

**métodos:** `ExecuteAsync(productId, userId)` — verifica ownership

</blockquote>
</details>

<details id="GetPriceHistoryUseCase">
<summary><strong><a href="../src/PriceWatch.Application/UseCases/TrackedProduct/GetPriceHistoryUseCase.cs">GetPriceHistoryUseCase.cs</a> [class]</strong></summary>
<blockquote>

**dependências:** [IPriceSnapshotRepository](#IPriceSnapshotRepository), [ITrackedProductRepository](#ITrackedProductRepository)

**métodos:** `ExecuteAsync(productId, userId) → IEnumerable<PriceSnapshotResponse>`

</blockquote>
</details>

</blockquote>
</details>

<details id="dir-application-usecases-notification">
<summary><strong>UseCases/Notification/</strong></summary>
<blockquote>

<details id="GetNotificationsUseCase">
<summary><strong><a href="../src/PriceWatch.Application/UseCases/Notification/GetNotificationsUseCase.cs">GetNotificationsUseCase.cs</a> [class]</strong></summary>
<blockquote>

**dependências:** [INotificationRepository](#INotificationRepository)

**métodos:** `ExecuteAsync(userId, isRead?) → IEnumerable<NotificationResponse>`

</blockquote>
</details>

<details id="MarkAsReadUseCase">
<summary><strong><a href="../src/PriceWatch.Application/UseCases/Notification/MarkAsReadUseCase.cs">MarkAsReadUseCase.cs</a> [class]</strong></summary>
<blockquote>

**dependências:** [INotificationRepository](#INotificationRepository)

**métodos:** `ExecuteAsync(notificationId, userId)` — lança [NotificationNotFoundException](#NotificationNotFoundException) se não existe ou ownership inválida

</blockquote>
</details>

<details id="MarkAllAsReadUseCase">
<summary><strong><a href="../src/PriceWatch.Application/UseCases/Notification/MarkAllAsReadUseCase.cs">MarkAllAsReadUseCase.cs</a> [class]</strong></summary>
<blockquote>

**dependências:** [INotificationRepository](#INotificationRepository)

**métodos:** `ExecuteAsync(userId)` — marca todas as notificações não lidas do user

</blockquote>
</details>

<details id="ProcessAlertUseCase">
<summary><strong><a href="../src/PriceWatch.Application/UseCases/Notification/ProcessAlertUseCase.cs">ProcessAlertUseCase.cs</a> [class]</strong></summary>
<blockquote>

**dependências:** [INotificationRepository](#INotificationRepository), [IEmailSender](#IEmailSender)

**métodos:** `ExecuteAsync(userId, productId, productName, type, currentPrice, userEmail)` — cria Notification, salva, envia email

</blockquote>
</details>

</blockquote>
</details>

<details id="dir-application-usecases-offers">
<summary><strong>UseCases/Offers/ (Fases 4–6 — pivô multi-loja)</strong></summary>
<blockquote>

<details id="CreateOfferUseCase">
<summary><strong><a href="../src/PriceWatch.Application/UseCases/Offers/CreateOfferUseCase.cs">CreateOfferUseCase.cs</a> [class]</strong></summary>
<blockquote>

**dependências:** [IProductRepository](#IProductRepository), [IStoreOfferRepository](#IStoreOfferRepository), [IUserProductSubscriptionRepository](#IUserProductSubscriptionRepository), `IPriceFetcherResolver`, [IProductMatchingEventPublisher](#IProductMatchingEventPublisher)

**métodos:** `ExecuteAsync(userId, CreateOfferRequest) → OfferResponse` — dedup por (source, url); se oferta nova, cria [Product](#Product)+[StoreOffer](#StoreOffer) e publica evento de matching; sempre garante/atualiza a [UserProductSubscription](#UserProductSubscription) do usuário

</blockquote>
</details>

<details id="GetProductComparisonUseCase">
<summary><strong><a href="../src/PriceWatch.Application/UseCases/Offers/GetProductComparisonUseCase.cs">GetProductComparisonUseCase.cs</a> [class]</strong></summary>
<blockquote>

**dependências:** [IProductRepository](#IProductRepository), [IStoreOfferRepository](#IStoreOfferRepository)

**métodos:** `ExecuteAsync(productId) → ProductComparisonResponse` — ofertas ativas ordenadas por preço, marca a mais barata; lança [ProductNotFoundException](#ProductNotFoundException)

</blockquote>
</details>

<details id="RedirectOfferUseCase">
<summary><strong><a href="../src/PriceWatch.Application/UseCases/Offers/RedirectOfferUseCase.cs">RedirectOfferUseCase.cs</a> [class]</strong></summary>
<blockquote>

**dependências:** [IStoreOfferRepository](#IStoreOfferRepository), `IAffiliateLinkResolver`

**métodos:** `ExecuteAsync(offerId) → StoreOfferRedirectResult(OfferId, Source, AffiliateUrl)` — resolve/cacheia o link de afiliado em `StoreOffer.AffiliateUrl`; lança [StoreOfferNotFoundException](#StoreOfferNotFoundException)

</blockquote>
</details>

<details id="BackfillTrackedProductsUseCase">
<summary><strong><a href="../src/PriceWatch.Application/UseCases/Offers/BackfillTrackedProductsUseCase.cs">BackfillTrackedProductsUseCase.cs</a> [class]</strong></summary>
<blockquote>

**dependências:** [ITrackedProductRepository](#ITrackedProductRepository), [IProductRepository](#IProductRepository), [IStoreOfferRepository](#IStoreOfferRepository), [IUserProductSubscriptionRepository](#IUserProductSubscriptionRepository), [IProductMatchingEventPublisher](#IProductMatchingEventPublisher)

**métodos:** `ExecuteAsync() → int` (migrados) — migração aditiva do [TrackedProduct](#TrackedProduct) legado pro modelo novo; idempotente via `TrackedProduct.MarkMigratedToProduct/IsMigratedToProduct`; chamado por [BackfillWorker](#BackfillWorker)

</blockquote>
</details>

<details id="MatchStoreOfferUseCase">
<summary><strong><a href="../src/PriceWatch.Application/UseCases/Offers/MatchStoreOfferUseCase.cs">MatchStoreOfferUseCase.cs</a> [class]</strong></summary>
<blockquote>

**dependências:** [IProductRepository](#IProductRepository), [IStoreOfferRepository](#IStoreOfferRepository), [IUserProductSubscriptionRepository](#IUserProductSubscriptionRepository)

**métodos:** `ExecuteAsync(offerId, productId)` — consumido por [ProductMatchingConsumer](#ProductMatchingConsumer). GTIN bate com outro produto → funde de fato (`StoreOffer.ReassignToProduct`, subscrições realocadas/removidas se duplicadas, produto vira `AutoMatched`). Sem GTIN → fuzzy via [TitleSimilarity](#TitleSimilarity) (threshold 0.82 c/ marca igual, 0.92 sem) — nunca funde, só `Product.SuggestMatch()` (RF14/RF15/RI06/RI07)

</blockquote>
</details>

</blockquote>
</details>

</blockquote>
</details>

<details id="dir-infrastructure">
<summary><strong>PriceWatch.Infrastructure/</strong></summary>
<blockquote>

<details id="dir-infra-security">
<summary><strong>Security/</strong></summary>
<blockquote>

- [BcryptPasswordHasher.cs](../src/PriceWatch.Infrastructure/Security/BcryptPasswordHasher.cs) — implementa `IPasswordHasher` usando BCrypt.Net
- [JwtTokenService.cs](../src/PriceWatch.Infrastructure/Security/JwtTokenService.cs) — implementa `ITokenService` gerando JWT com claims `sub`, `email`

</blockquote>
</details>

<details id="dir-infra-email">
<summary><strong>Email/</strong></summary>
<blockquote>

- [SmtpEmailSender.cs](../src/PriceWatch.Infrastructure/Email/SmtpEmailSender.cs) — implementa `IEmailSender` via SMTP (MailKit)

</blockquote>
</details>

<details id="dir-infra-mongodb-documents">
<summary><strong>Persistence/MongoDB/Documents/</strong></summary>
<blockquote>

- [MongoDbIndexInitializer.cs](../src/PriceWatch.Infrastructure/Persistence/MongoDB/MongoDbIndexInitializer.cs) — cria os índices no startup; idempotente. Fase 4 (pivô) adicionou índices de `products` (gtin), `store_offers` (productId, check queue, source+url único) e `user_product_subscriptions` (userId+productId único, listId)
- [UserDocument.cs](../src/PriceWatch.Infrastructure/Persistence/MongoDB/Documents/UserDocument.cs) — documento MongoDB para [User](#User)
- [ProductListDocument.cs](../src/PriceWatch.Infrastructure/Persistence/MongoDB/Documents/ProductListDocument.cs) — documento MongoDB para [ProductList](#ProductList)
- [TrackedProductDocument.cs](../src/PriceWatch.Infrastructure/Persistence/MongoDB/Documents/TrackedProductDocument.cs) — documento MongoDB para [TrackedProduct](#TrackedProduct)
- [PriceSnapshotDocument.cs](../src/PriceWatch.Infrastructure/Persistence/MongoDB/Documents/PriceSnapshotDocument.cs) — documento MongoDB para [PriceSnapshot](#PriceSnapshot)
- [NotificationDocument.cs](../src/PriceWatch.Infrastructure/Persistence/MongoDB/Documents/NotificationDocument.cs) — documento MongoDB para [Notification](#Notification)
- [ProductDocument.cs](../src/PriceWatch.Infrastructure/Persistence/MongoDB/Documents/ProductDocument.cs) — documento MongoDB para [Product](#Product) (Fase 4, collection `products`)
- [StoreOfferDocument.cs](../src/PriceWatch.Infrastructure/Persistence/MongoDB/Documents/StoreOfferDocument.cs) — documento MongoDB para [StoreOffer](#StoreOffer) (Fase 4, collection `store_offers`)
- [UserProductSubscriptionDocument.cs](../src/PriceWatch.Infrastructure/Persistence/MongoDB/Documents/UserProductSubscriptionDocument.cs) — documento MongoDB para [UserProductSubscription](#UserProductSubscription) (Fase 4, collection `user_product_subscriptions`)

</blockquote>
</details>

<details id="dir-infra-mongodb-mappings">
<summary><strong>Persistence/MongoDB/Mappings/</strong></summary>
<blockquote>

- [UserMappings.cs](../src/PriceWatch.Infrastructure/Persistence/MongoDB/Mappings/UserMappings.cs) — `ToDocument(User)`, `ToDomain(UserDocument)`
- [ProductListMappings.cs](../src/PriceWatch.Infrastructure/Persistence/MongoDB/Mappings/ProductListMappings.cs) — `ToDocument(ProductList)`, `ToDomain(ProductListDocument)`
- [TrackedProductMappings.cs](../src/PriceWatch.Infrastructure/Persistence/MongoDB/Mappings/TrackedProductMappings.cs) — `ToDocument(TrackedProduct)`, `ToDomain(TrackedProductDocument)`
- [PriceSnapshotMappings.cs](../src/PriceWatch.Infrastructure/Persistence/MongoDB/Mappings/PriceSnapshotMappings.cs) — `ToDocument(PriceSnapshot)`, `ToDomain(PriceSnapshotDocument)`
- [NotificationMappings.cs](../src/PriceWatch.Infrastructure/Persistence/MongoDB/Mappings/NotificationMappings.cs) — `ToDocument(Notification)`, `ToDomain(NotificationDocument)`
- [ProductMappings.cs](../src/PriceWatch.Infrastructure/Persistence/MongoDB/Mappings/ProductMappings.cs) — `ToDocument(Product)`, `ToDomain(ProductDocument)` (Fase 4)
- [StoreOfferMappings.cs](../src/PriceWatch.Infrastructure/Persistence/MongoDB/Mappings/StoreOfferMappings.cs) — `ToDocument(StoreOffer)`, `ToDomain(StoreOfferDocument)` (Fase 4)
- [UserProductSubscriptionMappings.cs](../src/PriceWatch.Infrastructure/Persistence/MongoDB/Mappings/UserProductSubscriptionMappings.cs) — `ToDocument(UserProductSubscription)`, `ToDomain(UserProductSubscriptionDocument)` (Fase 4)

</blockquote>
</details>

<details id="dir-infra-mongodb-repositories">
<summary><strong>Persistence/MongoDB/Repositories/</strong></summary>
<blockquote>

<details id="UserRepository">
<summary><strong><a href="../src/PriceWatch.Infrastructure/Persistence/MongoDB/Repositories/UserRepository.cs">UserRepository.cs</a> [class]</strong></summary>
<blockquote>

**implements:** [IUserRepository](#IUserRepository)

**dependências:** `IMongoDatabase` — collection `users`

</blockquote>
</details>

<details id="ProductListRepository">
<summary><strong><a href="../src/PriceWatch.Infrastructure/Persistence/MongoDB/Repositories/ProductListRepository.cs">ProductListRepository.cs</a> [class]</strong></summary>
<blockquote>

**implements:** [IProductListRepository](#IProductListRepository)

**dependências:** `IMongoDatabase` — collection `product_lists`

</blockquote>
</details>

<details id="TrackedProductRepository">
<summary><strong><a href="../src/PriceWatch.Infrastructure/Persistence/MongoDB/Repositories/TrackedProductRepository.cs">TrackedProductRepository.cs</a> [class]</strong></summary>
<blockquote>

**implements:** [ITrackedProductRepository](#ITrackedProductRepository)

**dependências:** `IMongoDatabase` — collection `tracked_products`

</blockquote>
</details>

<details id="PriceSnapshotRepository">
<summary><strong><a href="../src/PriceWatch.Infrastructure/Persistence/MongoDB/Repositories/PriceSnapshotRepository.cs">PriceSnapshotRepository.cs</a> [class]</strong></summary>
<blockquote>

**implements:** [IPriceSnapshotRepository](#IPriceSnapshotRepository)

**dependências:** `IMongoDatabase` — collection `price_snapshots`

</blockquote>
</details>

<details id="ProductRepository">
<summary><strong><a href="../src/PriceWatch.Infrastructure/Persistence/MongoDB/Repositories/ProductRepository.cs">ProductRepository.cs</a> [class]</strong></summary>
<blockquote>

**implements:** [IProductRepository](#IProductRepository)

**dependências:** `IMongoDatabase` — collection `products` (Fase 4)

</blockquote>
</details>

<details id="StoreOfferRepository">
<summary><strong><a href="../src/PriceWatch.Infrastructure/Persistence/MongoDB/Repositories/StoreOfferRepository.cs">StoreOfferRepository.cs</a> [class]</strong></summary>
<blockquote>

**implements:** [IStoreOfferRepository](#IStoreOfferRepository)

**dependências:** `IMongoDatabase` — collection `store_offers` (Fase 4)

</blockquote>
</details>

<details id="UserProductSubscriptionRepository">
<summary><strong><a href="../src/PriceWatch.Infrastructure/Persistence/MongoDB/Repositories/UserProductSubscriptionRepository.cs">UserProductSubscriptionRepository.cs</a> [class]</strong></summary>
<blockquote>

**implements:** [IUserProductSubscriptionRepository](#IUserProductSubscriptionRepository)

**dependências:** `IMongoDatabase` — collection `user_product_subscriptions` (Fase 4)

</blockquote>
</details>

</blockquote>
</details>

<details id="NotificationRepository">
<summary><strong><a href="../src/PriceWatch.Infrastructure/Persistence/MongoDB/Repositories/NotificationRepository.cs">NotificationRepository.cs</a> [class]</strong></summary>
<blockquote>

**implements:** [INotificationRepository](#INotificationRepository)

**dependências:** `IMongoDatabase` — collection `notifications`; suporta filtro por `isRead?`

</blockquote>
</details>

</blockquote>
</details>

<details id="dir-infra-messaging">
<summary><strong>Messaging/</strong></summary>
<blockquote>

<details id="RedisStreamPublisher">
<summary><strong><a href="../src/PriceWatch.Infrastructure/Messaging/RedisStreamPublisher.cs">RedisStreamPublisher.cs</a> [class]</strong></summary>
<blockquote>

**implements:** [IAlertPublisher](#IAlertPublisher)

**dependências:** `IConnectionMultiplexer` (StackExchange.Redis)

**comportamento:** publica `AlertEvent` serializado como JSON no stream `"price-alerts"`

</blockquote>
</details>

<details id="RedisStreamConsumer">
<summary><strong><a href="../src/PriceWatch.Infrastructure/Messaging/RedisStreamConsumer.cs">RedisStreamConsumer.cs</a> [class]</strong></summary>
<blockquote>

**extends:** `BackgroundService`

**dependências:** `IConnectionMultiplexer`, `IServiceScopeFactory`, `ILogger<RedisStreamConsumer>`

**comportamento:** loop a cada 5s — consumer group `notification-group`; desserializa `AlertEvent`, chama [ProcessAlertUseCase](#ProcessAlertUseCase), faz `StreamAcknowledge`

</blockquote>
</details>

<details id="RedisStoreOfferEventPublisher">
<summary><strong><a href="../src/PriceWatch.Infrastructure/Messaging/RedisStoreOfferEventPublisher.cs">RedisStoreOfferEventPublisher.cs</a> [class]</strong></summary>
<blockquote>

**implements:** [IProductMatchingEventPublisher](#IProductMatchingEventPublisher)

**dependências:** `IConnectionMultiplexer`

**comportamento:** publica `StoreOfferCreatedEvent` serializado como JSON no stream `"store-offer-events"` (Fase 5)

</blockquote>
</details>

<details id="ProductMatchingConsumer">
<summary><strong><a href="../src/PriceWatch.Infrastructure/Messaging/ProductMatchingConsumer.cs">ProductMatchingConsumer.cs</a> [class]</strong></summary>
<blockquote>

**extends:** `BackgroundService`

**dependências:** `IConnectionMultiplexer`, `IServiceScopeFactory`, `ILogger<ProductMatchingConsumer>`

**comportamento:** loop a cada 5s — consumer group `matching-group`; desserializa `StoreOfferCreatedEvent`, chama [MatchStoreOfferUseCase](#MatchStoreOfferUseCase), faz `StreamAcknowledge`. Desligado nos testes de integração (mesmo padrão do [RedisStreamConsumer](#RedisStreamConsumer)) — Fase 5

</blockquote>
</details>

</blockquote>
</details>

<details id="dir-infra-fetchers">
<summary><strong>Fetchers/</strong></summary>
<blockquote>

<details id="MercadoLivreFetcher">
<summary><strong><a href="../src/PriceWatch.Infrastructure/Fetchers/MercadoLivreFetcher.cs">MercadoLivreFetcher.cs</a> [class]</strong></summary>
<blockquote>

**implements:** [IPriceFetcher](#IPriceFetcher)

**dependências:** `IHttpClientFactory`

**Source:** `"mercadolivre"` — `FetchAsync` retorna `0m` (placeholder; integração ML futura) — **nota: descrição pré-pivot desatualizada, a integração real com a API de catálogo do ML já existe.** Fase 3 (pivô): passou a extrair `Gtin`/`Brand`/`Model` do array `attributes` do payload (já consumido, só não desserializado antes) — ver `ProductFetchResult`

</blockquote>
</details>

<details id="PriceFetcherResolver">
<summary><strong><a href="../src/PriceWatch.Infrastructure/Fetchers/PriceFetcherResolver.cs">PriceFetcherResolver.cs</a> [class]</strong></summary>
<blockquote>

**implements:** [IPriceFetcherResolver](#IPriceFetcherResolver)

**dependências:** `IEnumerable<IPriceFetcher>` — padrão Strategy/OCP

**métodos:** `Resolve(ProductSource)` — lança [BusinessException](#BusinessException) se fetcher não encontrado

</blockquote>
</details>

<details id="KabumFetcher">
<summary><strong><a href="../src/PriceWatch.Infrastructure/Fetchers/KabumFetcher.cs">KabumFetcher.cs</a> [partial class]</strong></summary>
<blockquote>

**implements:** [IPriceFetcher](#IPriceFetcher)

**dependências:** `HttpClient`, `IOptions<LomadeeSettings>`

**comportamento:** busca via API de ofertas da Lomadee (`GET {baseUrl}/{appToken}/offer/_search?...&keyword=...`) — não há lookup direto por URL como no ML, então extrai palavra-chave do slug da URL e casa pelo campo `link` retornado (fallback: primeira oferta Kabum). Extrai `barcode` como GTIN (Fase 6)

**⚠️ não validado contra a API real** — endpoint/campos seguem a doc pública da Lomadee Developers; precisa de smoke test manual com credenciais reais (ver `docs/rastreabilidade.md`)

</blockquote>
</details>

</blockquote>
</details>

<details id="dir-infra-affiliates">
<summary><strong>Affiliates/ (Fases 4–6 — pivô multi-loja)</strong></summary>
<blockquote>

<details id="PassthroughAffiliateLinkGenerator">
<summary><strong><a href="../src/PriceWatch.Infrastructure/Affiliates/PassthroughAffiliateLinkGenerator.cs">PassthroughAffiliateLinkGenerator.cs</a> [class]</strong></summary>
<blockquote>

**implements:** [IAffiliateLinkGenerator](#IAffiliateLinkGenerator)

**comportamento:** `CanHandle` sempre `true`; `GenerateAsync` devolve a URL original sem transformação — fallback universal do `AffiliateLinkResolver` enquanto uma loja não tem rede de afiliados real integrada

</blockquote>
</details>

<details id="AffiliateLinkResolver">
<summary><strong><a href="../src/PriceWatch.Infrastructure/Affiliates/AffiliateLinkResolver.cs">AffiliateLinkResolver.cs</a> [class]</strong></summary>
<blockquote>

**implements:** `IAffiliateLinkResolver`

**dependências:** `IEnumerable<IAffiliateLinkGenerator>`

**métodos:** `Resolve(ProductSource)`, `Resolve(url)` — diferente do `PriceFetcherResolver`, nunca lança: cai no [PassthroughAffiliateLinkGenerator](#PassthroughAffiliateLinkGenerator) se nada específico bater

</blockquote>
</details>

<details id="LomadeeDeeplinkGenerator">
<summary><strong><a href="../src/PriceWatch.Infrastructure/Affiliates/LomadeeDeeplinkGenerator.cs">LomadeeDeeplinkGenerator.cs</a> [class]</strong></summary>
<blockquote>

**implements:** [IAffiliateLinkGenerator](#IAffiliateLinkGenerator)

**dependências:** `HttpClient`, `IOptions<LomadeeSettings>`, `ILogger<LomadeeDeeplinkGenerator>`

**comportamento:** converte a URL do Kabum em link "lomadeezado" via `GET {baseUrl}/{appToken}/deeplink/_create?...&url=...`; nunca lança — cai no passthrough (URL original) se a chamada falhar (Fase 6)

**⚠️ não validado contra a API real** — mesma ressalva do [KabumFetcher](#KabumFetcher)

</blockquote>
</details>

</blockquote>
</details>

<details id="dir-infra-workers">
<summary><strong>Workers/</strong></summary>
<blockquote>

<details id="PriceCheckWorker">
<summary><strong><a href="../src/PriceWatch.Infrastructure/Workers/PriceCheckWorker.cs">PriceCheckWorker.cs</a> [class]</strong></summary>
<blockquote>

**extends:** `BackgroundService`

**dependências:** `IServiceScopeFactory`, `ILogger<PriceCheckWorker>`

**comportamento:** loop a cada 1min — busca produtos com `nextCheckAt <= now`, fetcha preço, salva snapshot, atualiza produto. Publicação de alert tem TODO pendente (IAlertPublisher — domínio Notification)

</blockquote>
</details>

<details id="BackfillWorker">
<summary><strong><a href="../src/PriceWatch.Infrastructure/Workers/BackfillWorker.cs">BackfillWorker.cs</a> [class]</strong></summary>
<blockquote>

**extends:** `BackgroundService`

**dependências:** `IServiceScopeFactory`, `ILogger<BackfillWorker>`

**comportamento:** roda uma vez no startup, chama [BackfillTrackedProductsUseCase](#BackfillTrackedProductsUseCase) — idempotente (Fase 4)

</blockquote>
</details>

</blockquote>
</details>

<details id="dir-infra-settings">
<summary><strong>Settings/</strong></summary>
<blockquote>

- [MongoDbSettings.cs](../src/PriceWatch.Infrastructure/Settings/MongoDbSettings.cs) — `ConnectionString`, `DatabaseName`
- [JwtSettings.cs](../src/PriceWatch.Infrastructure/Settings/JwtSettings.cs) — `Secret`, `Issuer`, `Audience`, `ExpiresInHours`
- [SmtpSettings.cs](../src/PriceWatch.Infrastructure/Settings/SmtpSettings.cs) — `Host`, `Port`, `Username`, `Password`, `FromEmail`, `FromName`
- [RedisSettings.cs](../src/PriceWatch.Infrastructure/Settings/RedisSettings.cs) — `ConnectionString`
- [RateLimitingSettings.cs](../src/PriceWatch.Infrastructure/Settings/RateLimitingSettings.cs) — `AnonymousPermitLimit=30`, `AuthenticatedPermitLimit=120`, `WindowSeconds=60` (Fase 3)
- [LomadeeSettings.cs](../src/PriceWatch.Infrastructure/Settings/LomadeeSettings.cs) — `AppToken?`, `SourceId?`, `BaseUrl="https://api.lomadee.com/v2"` (Fase 6 — credenciais vazias até o dev se cadastrar como afiliado)

</blockquote>
</details>

</blockquote>
</details>

<details id="dir-api">
<summary><strong>PriceWatch.API/</strong></summary>
<blockquote>

<details id="dir-api-errors">
<summary><strong>Errors/</strong></summary>
<blockquote>

<details id="ErrorResponse">
<summary><strong><a href="../src/PriceWatch.API/Errors/ErrorResponse.cs">ErrorResponse.cs</a> [record]</strong></summary>
<blockquote>

**construtor:** `ErrorResponse(int Status, string Error, string Message)`

**uso:** corpo padronizado de resposta de erro em todos os endpoints

</blockquote>
</details>

</blockquote>
</details>

<details id="dir-api-middleware">
<summary><strong>Middleware/</strong></summary>
<blockquote>

<details id="GlobalExceptionHandler">
<summary><strong><a href="../src/PriceWatch.API/Middleware/GlobalExceptionHandler.cs">GlobalExceptionHandler.cs</a> [sealed class]</strong></summary>
<blockquote>

**implements:** `IExceptionHandler`

**dependências:** `ILogger<GlobalExceptionHandler>`

**métodos:**
- `ValueTask<bool> TryHandleAsync(HttpContext, Exception, CancellationToken)`

**mapeamento de status:**
| Exceção | HTTP |
|---|---|
| `NotFoundException` | 404 |
| `BusinessException` | 400 |
| qualquer outra | 500 |

</blockquote>
</details>

</blockquote>
</details>

<details id="dir-api-controllers">
<summary><strong>Controllers/</strong></summary>
<blockquote>

<details id="AuthController">
<summary><strong><a href="../src/PriceWatch.API/Controllers/AuthController.cs">AuthController.cs</a> [class]</strong></summary>
<blockquote>

**extends:** `ControllerBase`

**rota:** `api/auth`

**endpoints:** `POST /register`, `POST /login`, `POST /verify-email`, `POST /resend-verification`

</blockquote>
</details>

<details id="ProductListsController">
<summary><strong><a href="../src/PriceWatch.API/Controllers/ProductListsController.cs">ProductListsController.cs</a> [class]</strong></summary>
<blockquote>

**extends:** `ControllerBase`

**rota:** `api/lists` — requer `[Authorize]`

**endpoints:** `GET /` → 200, `POST /` → 201, `PUT /{id}` → 204, `DELETE /{id}` → 204, `GET /{id}/analysis` → 200

**helper privado:** `GetUserId()` — extrai claim `NameIdentifier` do JWT

</blockquote>
</details>

<details id="OffersController">
<summary><strong><a href="../src/PriceWatch.API/Controllers/OffersController.cs">OffersController.cs</a> [class]</strong></summary>
<blockquote>

**extends:** `ControllerBase`

**dependências:** [CreateOfferUseCase](#CreateOfferUseCase), [GetProductComparisonUseCase](#GetProductComparisonUseCase), [RedirectOfferUseCase](#RedirectOfferUseCase), `ILogger<OffersController>`

**endpoints:** `POST /api/offers` → 201, `GET /api/products/{id}/comparison` → 200, `GET /api/offers/{id}/redirect` → 302 (loga o clique antes de redirecionar)

**Fase 4/6 — pivô multi-loja.** Nota: a action de redirect chama-se `RedirectToOffer` (não `Redirect`) pra não colidir com `ControllerBase.Redirect(string)`

</blockquote>
</details>

</blockquote>
</details>

<details id="dir-api-ratelimiting">
<summary><strong>RateLimiting/ (Fase 3)</strong></summary>
<blockquote>

<details id="RateLimitPartitionSelector">
<summary><strong><a href="../src/PriceWatch.API/RateLimiting/RateLimitPartitionSelector.cs">RateLimitPartitionSelector.cs</a> [static class]</strong></summary>
<blockquote>

**métodos:** `Select(httpContext, anonymousPermitLimit, authenticatedPermitLimit, window) → RateLimitPartition<string>`

**comportamento:** autenticado → partição `user:{id}`; anônimo → partição `ip:{remoteIp}`. Extraído como método puro testável (sem precisar subir host) — usado em `Program.cs` via `AddOptions<RateLimiterOptions>().Configure<IOptions<RateLimitingSettings>>(...)`

</blockquote>
</details>

</blockquote>
</details>

<details id="dir-api-extensions">
<summary><strong>Extensions/</strong></summary>
<blockquote>

- [ServiceCollectionExtensions.cs](../src/PriceWatch.API/Extensions/ServiceCollectionExtensions.cs) — `AddApplicationServices()` compondo `AddMongoDb`, `AddJwtAuth` (Fase 3: fail-fast se `Jwt:Secret` vazio), `AddInfrastructure` (Fase 3: Polly retry/circuit breaker nos `AddHttpClient<...>`; Fases 4–6: DI do pivô multi-loja), `AddUseCases`

</blockquote>
</details>

- [Program.cs](../src/PriceWatch.API/Program.cs) — entry point; registra `GlobalExceptionHandler`, Swagger, Controllers. **Fase 3:** Serilog (`UseSerilog`, console sink, `CorrelationId` via `LogContext`), `AddRateLimiter`/`UseRateLimiter` (usa [RateLimitPartitionSelector](#RateLimitPartitionSelector))
- [appsettings.json](../src/PriceWatch.API/appsettings.json) — seções `Lomadee` (Fase 6) e `RateLimiting` (Fase 3) adicionadas
- [appsettings.Development.json](../src/PriceWatch.API/appsettings.Development.json)

</blockquote>
</details>

---

## tests/

<details id="dir-unit-tests">
<summary><strong>PriceWatch.UnitTests/</strong></summary>
<blockquote>

<details id="dir-unit-domain">
<summary><strong>Domain/</strong></summary>
<blockquote>

- [Entities/UserTests.cs](../tests/PriceWatch.UnitTests/Domain/Entities/UserTests.cs) — 7 testes: `Create`, `VerifyEmail`, `RegenerateVerificationToken`
- [Entities/ProductListTests.cs](../tests/PriceWatch.UnitTests/Domain/Entities/ProductListTests.cs) — 3 testes: `Create_ShouldSetUserIdAndCreatedAt`, `Create_ShouldSetNameAndDescription`, `Update_ShouldChangeNameAndDescription`
- [Entities/PriceSnapshotTests.cs](../tests/PriceWatch.UnitTests/Domain/Entities/PriceSnapshotTests.cs) — 1 teste: `Create_ShouldSetPriceAndCapturedAt`
- [Entities/TrackedProductTests.cs](../tests/PriceWatch.UnitTests/Domain/Entities/TrackedProductTests.cs) — 11 testes: `Create`, `RecordPrice` (4 variantes), `ShouldTriggerTargetAlert` (2), `ShouldTriggerLowestAlert`, `Deactivate`, `IsMigratedToProduct`/`MarkMigratedToProduct` (2, Fase 4)
- [Exceptions/ExceptionHierarchyTests.cs](../tests/PriceWatch.UnitTests/Domain/Exceptions/ExceptionHierarchyTests.cs) — verifica hierarquia: `NotFoundException` é abstrata, `BusinessException` herda de `Exception`
- [Entities/ProductTests.cs](../tests/PriceWatch.UnitTests/Domain/Entities/ProductTests.cs) — `Create`, `MarkAutoMatched`, `SuggestMatch`, `IsVisibleInComparison` (Theory por MatchStatus) — Fase 4/5
- [Entities/StoreOfferTests.cs](../tests/PriceWatch.UnitTests/Domain/Entities/StoreOfferTests.cs) — `Create`, `RecordPrice` (3 variantes), `SetAffiliateUrl`, `Deactivate`, `ReassignToProduct` — Fase 4/5
- [Entities/UserProductSubscriptionTests.cs](../tests/PriceWatch.UnitTests/Domain/Entities/UserProductSubscriptionTests.cs) — `Create`, `ShouldTriggerTargetAlert` (3), `SetTargetPrice`, `RemoveFromList`, `ReassignToProduct` — Fase 4/5

</blockquote>
</details>

<details id="dir-unit-domain-services">
<summary><strong>Domain/Services/ (Fase 5)</strong></summary>
<blockquote>

- [TitleSimilarityTests.cs](../tests/PriceWatch.UnitTests/Domain/Services/TitleSimilarityTests.cs) — 7 testes: `Normalize` (lowercase/pontuação), `Ratio` (idêntico, diferente, simétrico)

</blockquote>
</details>

<details id="dir-unit-usecases-auth">
<summary><strong>Application/UseCases/Auth/</strong></summary>
<blockquote>

- [RegisterUseCaseTests.cs](../tests/PriceWatch.UnitTests/Application/UseCases/Auth/RegisterUseCaseTests.cs) — 2 testes
- [LoginUseCaseTests.cs](../tests/PriceWatch.UnitTests/Application/UseCases/Auth/LoginUseCaseTests.cs) — testes de login
- [VerifyEmailUseCaseTests.cs](../tests/PriceWatch.UnitTests/Application/UseCases/Auth/VerifyEmailUseCaseTests.cs) — testes de verificação
- [ResendVerificationUseCaseTests.cs](../tests/PriceWatch.UnitTests/Application/UseCases/Auth/ResendVerificationUseCaseTests.cs) — testes de reenvio

</blockquote>
</details>

<details id="dir-unit-usecases-productlist">
<summary><strong>Application/UseCases/ProductList/</strong></summary>
<blockquote>

- [CreateListUseCaseTests.cs](../tests/PriceWatch.UnitTests/Application/UseCases/ProductList/CreateListUseCaseTests.cs) — 2 testes
- [GetUserListsUseCaseTests.cs](../tests/PriceWatch.UnitTests/Application/UseCases/ProductList/GetUserListsUseCaseTests.cs) — 2 testes
- [UpdateListUseCaseTests.cs](../tests/PriceWatch.UnitTests/Application/UseCases/ProductList/UpdateListUseCaseTests.cs) — 3 testes (inclui ownership e not-found)
- [DeleteListUseCaseTests.cs](../tests/PriceWatch.UnitTests/Application/UseCases/ProductList/DeleteListUseCaseTests.cs) — 2 testes (inclui ownership)

</blockquote>
</details>

<details id="dir-unit-usecases-trackedproduct">
<summary><strong>Application/UseCases/TrackedProduct/</strong></summary>
<blockquote>

- [AddProductUseCaseTests.cs](../tests/PriceWatch.UnitTests/Application/UseCases/TrackedProduct/AddProductUseCaseTests.cs) — 2 testes: fetch inicial e RecordPrice
- [GetProductsByListUseCaseTests.cs](../tests/PriceWatch.UnitTests/Application/UseCases/TrackedProduct/GetProductsByListUseCaseTests.cs) — 1 teste
- [GetListAnalysisUseCaseTests.cs](../tests/PriceWatch.UnitTests/Application/UseCases/TrackedProduct/GetListAnalysisUseCaseTests.cs) — 1 teste: ordenação por DistancePercent

</blockquote>
</details>

<details id="dir-unit-usecases-notification">
<summary><strong>Application/UseCases/Notification/</strong></summary>
<blockquote>

- [GetNotificationsUseCaseTests.cs](../tests/PriceWatch.UnitTests/Application/UseCases/Notification/GetNotificationsUseCaseTests.cs) — 2 testes: retorno e filtro por isRead
- [MarkAsReadUseCaseTests.cs](../tests/PriceWatch.UnitTests/Application/UseCases/Notification/MarkAsReadUseCaseTests.cs) — 3 testes: mark, not-found, ownership
- [ProcessAlertUseCaseTests.cs](../tests/PriceWatch.UnitTests/Application/UseCases/Notification/ProcessAlertUseCaseTests.cs) — 2 testes: create notification e send email

</blockquote>
</details>

<details id="dir-unit-usecases-offers">
<summary><strong>Application/UseCases/Offers/ (Fases 4–6 — pivô multi-loja)</strong></summary>
<blockquote>

- [CreateOfferUseCaseTests.cs](../tests/PriceWatch.UnitTests/Application/UseCases/Offers/CreateOfferUseCaseTests.cs) — 6 testes: oferta nova, subscrição, reuso por (source,url), reuso de subscrição, publicação/não-publicação do evento de matching
- [GetProductComparisonUseCaseTests.cs](../tests/PriceWatch.UnitTests/Application/UseCases/Offers/GetProductComparisonUseCaseTests.cs) — 4 testes: not-found, ordenação por preço, marcação de mais barata, exclusão de ofertas inativas
- [RedirectOfferUseCaseTests.cs](../tests/PriceWatch.UnitTests/Application/UseCases/Offers/RedirectOfferUseCaseTests.cs) — 3 testes: not-found, geração+cache do link, reuso de link já cacheado
- [BackfillTrackedProductsUseCaseTests.cs](../tests/PriceWatch.UnitTests/Application/UseCases/Offers/BackfillTrackedProductsUseCaseTests.cs) — 5 testes: criação, marca migrado, pula já migrado, dedup entre 2 legados com mesma (source,url), subscrição inativa
- [MatchStoreOfferUseCaseTests.cs](../tests/PriceWatch.UnitTests/Application/UseCases/Offers/MatchStoreOfferUseCaseTests.cs) — 9 testes: match por GTIN, GTIN sem outro produto, fuzzy só sugere (não funde), sem candidato, idempotência, offer/not-found, dedup de subscrição duplicada, reatribuição de subscrição, não rebaixa `Confirmed`

</blockquote>
</details>

<details id="dir-unit-notification-domain">
<summary><strong>Domain/Entities/ (Notification)</strong></summary>
<blockquote>

- [NotificationTests.cs](../tests/PriceWatch.UnitTests/Domain/Entities/NotificationTests.cs) — 4 testes: `IsRead=false`, message format TargetPrice, message format NewLowest, `MarkAsRead`

</blockquote>
</details>

<details id="dir-unit-infrastructure">
<summary><strong>Infrastructure/Fetchers/</strong></summary>
<blockquote>

- [PriceFetcherResolverTests.cs](../tests/PriceWatch.UnitTests/Infrastructure/Fetchers/PriceFetcherResolverTests.cs) — 2 testes: resolve ML fetcher e throw em source desconhecido
- [KabumFetcherTests.cs](../tests/PriceWatch.UnitTests/Infrastructure/Fetchers/KabumFetcherTests.cs) — 8 testes: match exato por link, keyword extraída do slug, fallback pra primeira oferta Kabum, sem oferta/só não-Kabum/erro API/preço inválido → `BusinessException`, `CanHandle`, `ProductSource` — HTTP mockado via `Moq.Protected`, sem credenciais reais (Fase 6)

</blockquote>
</details>

<details id="dir-unit-infrastructure-affiliates">
<summary><strong>Infrastructure/Affiliates/ (Fase 6)</strong></summary>
<blockquote>

- [LomadeeDeeplinkGeneratorTests.cs](../tests/PriceWatch.UnitTests/Infrastructure/Affiliates/LomadeeDeeplinkGeneratorTests.cs) — 6 testes: sucesso, erro da API → fallback pra URL original, exceção HTTP → fallback sem lançar, resposta sem `shortUrl` → fallback, `CanHandle`, `ProductSource` — HTTP mockado, sem credenciais reais

</blockquote>
</details>

</blockquote>
</details>

<details id="dir-integration-tests">
<summary><strong>PriceWatch.IntegrationTests/</strong></summary>
<blockquote>

<details id="dir-integration-middleware">
<summary><strong>Middleware/</strong></summary>
<blockquote>

- [GlobalExceptionHandlerTests.cs](../tests/PriceWatch.IntegrationTests/Middleware/GlobalExceptionHandlerTests.cs) — verifica mapeamento 404/400/500 e corpo `ErrorResponse`

</blockquote>
</details>

<details id="dir-integration-fixtures">
<summary><strong>Fixtures/ (doc pré-pivot incompleta — só o que mudou nas Fases 3–6 listado)</strong></summary>
<blockquote>

- [FakePriceFetcher.cs](../tests/PriceWatch.IntegrationTests/Fixtures/FakePriceFetcher.cs) — substitui `IPriceFetcher` real nos testes; deriva GTIN único por URL por padrão (hash), `SetSharedGtin(url, gtin)` pra forçar colisão deliberada — ver Erros.md sobre o bug de contaminação entre testes que motivou isso (Fase 5)
- [PriceWatchWebApplicationFactory.cs](../tests/PriceWatch.IntegrationTests/Fixtures/PriceWatchWebApplicationFactory.cs) — Fase 3: override de `RateLimiting:*Limit` alto pra não interferir na suíte; remove [ProductMatchingConsumer](#ProductMatchingConsumer) dos hosted services (Fase 5), igual já fazia com `PriceCheckWorker`/`RedisStreamConsumer`

</blockquote>
</details>

<details id="dir-integration-offers">
<summary><strong>Offers/ (Fase 4)</strong></summary>
<blockquote>

- [OfferFlowTests.cs](../tests/PriceWatch.IntegrationTests/Offers/OfferFlowTests.cs) — 7 testes: criar oferta, dedup por URL, 401 sem token, comparação not-found, comparação com destaque de mais barata, redirect 302, redirect not-found

</blockquote>
</details>

<details id="dir-integration-ratelimiting">
<summary><strong>RateLimiting/ (Fase 3)</strong></summary>
<blockquote>

- [RateLimitPartitionSelectorTests.cs](../tests/PriceWatch.IntegrationTests/RateLimiting/RateLimitPartitionSelectorTests.cs) — 3 testes: partição por IP (anônimo), partição por `user:{id}` (autenticado), rejeição acima do limite configurado. Fica no projeto de Integration (não Unit) porque referencia tipos do `PriceWatch.API`, que Unit não referencia

</blockquote>
</details>

<details id="dir-integration-matching">
<summary><strong>Matching/ (Fase 5)</strong></summary>
<blockquote>

- [MatchingFlowTests.cs](../tests/PriceWatch.IntegrationTests/Matching/MatchingFlowTests.cs) — cenário ponta a ponta com Mongo/Redis reais: duas ofertas com GTIN compartilhado (via `FakePriceFetcher.SetSharedGtin`) viram produtos separados na criação e são fundidas chamando [MatchStoreOfferUseCase](#MatchStoreOfferUseCase) direto (o consumer assíncrono está desligado nos testes) — verifica reatribuição de oferta, `AutoMatched`, dedup de subscrição duplicada, e o endpoint de comparação refletindo o merge

</blockquote>
</details>

</blockquote>
</details>
