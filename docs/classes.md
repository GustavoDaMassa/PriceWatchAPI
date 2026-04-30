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

**métodos:** `Create(listId, userId, url, source, name, targetPrice)`, `Restore(...)`, `RecordPrice(price) → PriceSnapshot`, `ShouldTriggerTargetAlert()`, `ShouldTriggerLowestAlert(previousLowest)`, `Deactivate()`

</blockquote>
</details>

<details id="Notification">
<summary><strong><a href="../src/PriceWatch.Domain/Entities/Notification.cs">Notification.cs</a> [class]</strong></summary>
<blockquote>

**atributos:** `Id`, `UserId`, `ProductId`, `ProductName`, `Type`, `Message`, `IsRead`, `CreatedAt` — todos `private set`

**métodos:** `Create(userId, productId, productName, type, currentPrice)` — gera Message pelo type; `Restore(...)`, `MarkAsRead()`

</blockquote>
</details>

</blockquote>
</details>

<details id="dir-domain-enums">
<summary><strong>Enums/</strong></summary>
<blockquote>

- [ProductSource.cs](../src/PriceWatch.Domain/Enums/ProductSource.cs) — `MercadoLivre, Kabum, Manual`
- [NotificationType.cs](../src/PriceWatch.Domain/Enums/NotificationType.cs) — `TargetPriceReached, NewLowestPrice`

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

**métodos:** `GetByListIdAsync(listId)`, `GetByIdAsync(id)`, `GetDueForCheckAsync()`, `CreateAsync(product)`, `UpdateAsync(product)`, `DeleteAsync(id)`

</blockquote>
</details>

<details id="IPriceSnapshotRepository">
<summary><strong><a href="../src/PriceWatch.Domain/Interfaces/Repositories/IPriceSnapshotRepository.cs">IPriceSnapshotRepository.cs</a> [interface]</strong></summary>
<blockquote>

**métodos:** `CreateAsync(snapshot)`, `GetByProductIdAsync(productId, limit=100)`

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

- [UserDocument.cs](../src/PriceWatch.Infrastructure/Persistence/MongoDB/Documents/UserDocument.cs) — documento MongoDB para [User](#User)
- [ProductListDocument.cs](../src/PriceWatch.Infrastructure/Persistence/MongoDB/Documents/ProductListDocument.cs) — documento MongoDB para [ProductList](#ProductList)
- [TrackedProductDocument.cs](../src/PriceWatch.Infrastructure/Persistence/MongoDB/Documents/TrackedProductDocument.cs) — documento MongoDB para [TrackedProduct](#TrackedProduct)
- [PriceSnapshotDocument.cs](../src/PriceWatch.Infrastructure/Persistence/MongoDB/Documents/PriceSnapshotDocument.cs) — documento MongoDB para [PriceSnapshot](#PriceSnapshot)
- [NotificationDocument.cs](../src/PriceWatch.Infrastructure/Persistence/MongoDB/Documents/NotificationDocument.cs) — documento MongoDB para [Notification](#Notification)

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

**Source:** `"mercadolivre"` — `FetchAsync` retorna `0m` (placeholder; integração ML futura)

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

</blockquote>
</details>

<details id="dir-infra-settings">
<summary><strong>Settings/</strong></summary>
<blockquote>

- [MongoDbSettings.cs](../src/PriceWatch.Infrastructure/Settings/MongoDbSettings.cs) — `ConnectionString`, `DatabaseName`
- [JwtSettings.cs](../src/PriceWatch.Infrastructure/Settings/JwtSettings.cs) — `Secret`, `Issuer`, `Audience`, `ExpiresInHours`
- [SmtpSettings.cs](../src/PriceWatch.Infrastructure/Settings/SmtpSettings.cs) — `Host`, `Port`, `Username`, `Password`, `FromEmail`, `FromName`
- [RedisSettings.cs](../src/PriceWatch.Infrastructure/Settings/RedisSettings.cs) — `ConnectionString`

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

</blockquote>
</details>

<details id="dir-api-extensions">
<summary><strong>Extensions/</strong></summary>
<blockquote>

- [ServiceCollectionExtensions.cs](../src/PriceWatch.API/Extensions/ServiceCollectionExtensions.cs) — `AddApplicationServices()` compondo `AddMongoDb`, `AddJwtAuth`, `AddInfrastructure`, `AddUseCases`

</blockquote>
</details>

- [Program.cs](../src/PriceWatch.API/Program.cs) — entry point; registra `GlobalExceptionHandler`, Swagger, Controllers
- [appsettings.json](../src/PriceWatch.API/appsettings.json)
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
- [Entities/TrackedProductTests.cs](../tests/PriceWatch.UnitTests/Domain/Entities/TrackedProductTests.cs) — 9 testes: `Create`, `RecordPrice` (4 variantes), `ShouldTriggerTargetAlert` (2), `ShouldTriggerLowestAlert`, `Deactivate`
- [Exceptions/ExceptionHierarchyTests.cs](../tests/PriceWatch.UnitTests/Domain/Exceptions/ExceptionHierarchyTests.cs) — verifica hierarquia: `NotFoundException` é abstrata, `BusinessException` herda de `Exception`

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

</blockquote>
</details>
