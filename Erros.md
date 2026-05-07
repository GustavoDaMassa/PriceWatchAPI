# PriceWatch — Erros Registrados

> Formato: data · fase · erro · causa raiz · correção aplicada

---

**2026-04-30 · Fase 4.2 · Build quebrado em Infrastructure**
- Erro: `CS0234 - System.IdentityModel.Tokens.Jwt não existe no namespace System`
- Causa: `JwtTokenService` usa `JwtSecurityTokenHandler` do pacote `System.IdentityModel.Tokens.Jwt`, que não foi adicionado ao projeto `PriceWatch.Infrastructure` (só foi adicionado ao `PriceWatch.API`)
- Correção: `dotnet add PriceWatch.Infrastructure package System.IdentityModel.Tokens.Jwt --version 8.*`

**2026-04-30 · Fase 4.2 · Warning nullable em SmtpEmailSender**
- Erro: `CS8604 - Possível argumento de referência nula para password em AuthenticateAsync`
- Causa: `_settings.Password` é `string?` mas foi passado sem null check para `AuthenticateAsync`
- Correção: adicionada verificação `!string.IsNullOrEmpty(_settings.Password)` no guard antes da autenticação

**2026-04-30 · Fase 5.2 (domain-tracked-product) · BackgroundService não encontrado no Infrastructure**
- Erro: `CS0234 - "Hosting" não existe no namespace "Microsoft.Extensions"` em `PriceCheckWorker.cs`
- Causa: `BackgroundService` vive em `Microsoft.Extensions.Hosting.Abstractions`, pacote não referenciado no projeto `PriceWatch.Infrastructure`
- Correção: `dotnet add PriceWatch.Infrastructure package Microsoft.Extensions.Hosting.Abstractions`

**2026-04-30 · Fase 5.2 (domain-tracked-product) · Conflito de namespace TrackedProduct nos testes**
- Erro: `CS0234 - "TrackedProduct" não existe no namespace "PriceWatch.UnitTests.Domain.Entities"` em `GetListAnalysisUseCaseTests.cs`
- Causa: mesmo padrão do domínio anterior — namespace do teste contém "TrackedProduct" causando ambiguidade
- Correção: adicionado alias `using DomainTrackedProduct = PriceWatch.Domain.Entities.TrackedProduct`

**2026-04-30 · Fase 5.1 (domain-product-list) · Conflito de namespace nos testes**
- Erro: `CS0118 - "ProductList" é um namespace, mas é usado como um tipo` em múltiplos arquivos de teste
- Causa: o namespace dos testes `PriceWatch.UnitTests.Application.UseCases.ProductList` contém `ProductList` como parte do caminho, fazendo o compilador resolver `ProductList` como namespace em vez do tipo `PriceWatch.Domain.Entities.ProductList`
- Correção: adicionado alias `using DomainProductList = PriceWatch.Domain.Entities.ProductList` nos quatro arquivos de teste dentro desse namespace

---

**2026-05-01 · Fase 7 · using `Testcontainers.MongoDB` inexistente**
- Erro: `CS0234 - O nome de tipo ou namespace "MongoDB" não existe no namespace "Testcontainers"`
- Causa: o assembly do pacote chama-se `Testcontainers.MongoDb.dll` — namespace é `Testcontainers.MongoDb` (camelCase), não `Testcontainers.MongoDB`
- Correção: using corrigido para `using Testcontainers.MongoDb;`

**2026-05-01 · Fase 7 · Testcontainers/ryuk falha ao subir (Docker Hub 401)**
- Erro: `Docker API responded with status code=Unauthorized` ao iniciar ResourceReaper
- Causa: credenciais inválidas armazenadas em `~/.docker/config.json`; Testcontainers tenta autenticar no Docker Hub para puxar `testcontainers/ryuk`
- Correção: `TESTCONTAINERS_RYUK_DISABLED=true` definido em `launchSettings.json`; imagens puxadas manualmente com `docker pull` antes dos testes

**2026-05-01 · feat/user-management · FormatException ao lançar UserNotFoundException com ID inválido**
- Erro: `System.FormatException: Unrecognized Guid format` em testes que passam string `"unknown"` como userId
- Causa: use cases usavam `new UserNotFoundException(Guid.Parse(userId))` — `Guid.Parse` lança quando o valor não é um GUID válido (ex: string de teste)
- Correção: substituído por `new UserNotFoundException(userId)` usando o construtor de string em todos os use cases de usuário

**2026-05-01 · Fase 7 · JWT inválido — 401 em todos os endpoints autenticados nos testes**
- Erro: testes com JWT retornavam 401 — `AddJwtAuth` capturava o signing key no momento do registro de serviços, antes de `ConfigureAppConfiguration` aplicar o override do factory
- Causa: em .NET 8 `WebApplicationFactory`, `ConfigureAppConfiguration` não garante que os valores sobrepostos estejam disponíveis quando `AddApplicationServices` é executado; o key de validação JWT usava o valor de `appsettings.json` (vazio → fallback), enquanto `JwtTokenService` usava `IOptions<JwtSettings>` com o valor correto do override
- Correção: substituído `ConfigureAppConfiguration` para JWT por `PostConfigure<JwtBearerOptions>` + `services.Configure<JwtSettings>` dentro de `ConfigureTestServices`, que roda DEPOIS de todo o registro de serviços

**2026-05-06 · Testes manuais · CORS não configurado — Swagger retorna "Failed to fetch"**
- Erro: Swagger UI retorna "Failed to fetch" ao executar qualquer requisição; mensagem lista CORS, Network Failure ou URL scheme inválido como possíveis causas
- Causa: `Program.cs` não continha `AddCors()` nem `UseCors()` — CORS nunca foi adicionado ao projeto
- Correção: adicionado `AddCors()` com política permissiva em desenvolvimento (`AllowAnyOrigin/Method/Header`) e baseada em `AllowedOrigins` (config) em produção; `UseCors()` inserido no pipeline antes de `UseAuthentication` — commit `6c3ec97`

**2026-05-07 · Testes manuais · MercadoLivreFetcher retorna 403 ao buscar preço**
- Erro: endpoint de adicionar produto retorna BusinessException com "Mercado Livre API returned 403 for item 'MLBxxx'"
- Causa: `MercadoLivreFetcher` chamava `GET /items/{id}` sem header `Authorization` — a API do Mercado Livre exige OAuth client credentials mesmo para leitura de itens públicos via chamadas server-side
- Correção: implementado `MercadoLivreTokenService` (singleton com cache + renovação automática) que obtém access token via `POST /oauth/token` com `grant_type=client_credentials`; fetcher injeta o token em cada requisição — commit `de40ef2`

**2026-05-07 · Testes manuais · Swagger exibe rotas antigas após alteração de endpoints**
- Erro: Swagger UI continua exibindo rotas antigas após atualizar os controllers e reiniciar a API via `start-dev.sh`
- Causa: `start-dev.sh` usava `dotnet run` com build incremental, que reutilizava artefatos em cache e não regenerava o XML de documentação do Swagger
- Correção: separado em `dotnet build --no-restore -q` seguido de `dotnet run --no-build`, garantindo rebuild explícito antes de subir — commit `4f93e66`
