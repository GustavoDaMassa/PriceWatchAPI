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

</blockquote>
</details>

<details id="dir-domain-interfaces-services">
<summary><strong>Interfaces/Services/</strong></summary>
<blockquote>

- [IPasswordHasher.cs](../src/PriceWatch.Domain/Interfaces/Services/IPasswordHasher.cs) — `Hash(password)`, `Verify(password, hash)`
- [ITokenService.cs](../src/PriceWatch.Domain/Interfaces/Services/ITokenService.cs) — `GenerateToken(user)`
- [IEmailSender.cs](../src/PriceWatch.Domain/Interfaces/Services/IEmailSender.cs) — `SendVerificationEmailAsync(email, name, token)`

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

<details id="dir-application-dtos-productlist">
<summary><strong>DTOs/ProductList/</strong></summary>
<blockquote>

- [CreateProductListRequest.cs](../src/PriceWatch.Application/DTOs/ProductList/CreateProductListRequest.cs) — `record(Name, Description?)`
- [UpdateProductListRequest.cs](../src/PriceWatch.Application/DTOs/ProductList/UpdateProductListRequest.cs) — `record(Name, Description?)`
- [ProductListResponse.cs](../src/PriceWatch.Application/DTOs/ProductList/ProductListResponse.cs) — `record(Id, Name, Description?, CreatedAt)`
- [AnalysisItemDto.cs](../src/PriceWatch.Application/DTOs/ProductList/AnalysisItemDto.cs) — `record(ProductId, ProductName, CurrentPrice, TargetPrice, DistancePercent)`

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
- [ProductListDocument.cs](../src/PriceWatch.Infrastructure/Persistence/MongoDB/Documents/ProductListDocument.cs) — documento MongoDB para [ProductList](#ProductList); campos: `Id`, `UserId`, `Name`, `Description?`, `CreatedAt`

</blockquote>
</details>

<details id="dir-infra-mongodb-mappings">
<summary><strong>Persistence/MongoDB/Mappings/</strong></summary>
<blockquote>

- [UserMappings.cs](../src/PriceWatch.Infrastructure/Persistence/MongoDB/Mappings/UserMappings.cs) — `ToDocument(User)`, `ToDomain(UserDocument)`
- [ProductListMappings.cs](../src/PriceWatch.Infrastructure/Persistence/MongoDB/Mappings/ProductListMappings.cs) — `ToDocument(ProductList)`, `ToDomain(ProductListDocument)`

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

</blockquote>
</details>

<details id="dir-infra-settings">
<summary><strong>Settings/</strong></summary>
<blockquote>

- [MongoDbSettings.cs](../src/PriceWatch.Infrastructure/Settings/MongoDbSettings.cs) — `ConnectionString`, `DatabaseName`
- [JwtSettings.cs](../src/PriceWatch.Infrastructure/Settings/JwtSettings.cs) — `Secret`, `Issuer`, `Audience`, `ExpiresInHours`
- [SmtpSettings.cs](../src/PriceWatch.Infrastructure/Settings/SmtpSettings.cs) — `Host`, `Port`, `Username`, `Password`, `FromEmail`, `FromName`

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
