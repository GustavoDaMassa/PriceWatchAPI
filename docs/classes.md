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

<details id="dir-unit-domain-exceptions">
<summary><strong>Domain/Exceptions/</strong></summary>
<blockquote>

- [ExceptionHierarchyTests.cs](../tests/PriceWatch.UnitTests/Domain/Exceptions/ExceptionHierarchyTests.cs) — verifica hierarquia: `NotFoundException` é abstrata, `BusinessException` herda de `Exception`

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
