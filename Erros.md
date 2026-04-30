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

**2026-04-30 · Fase 5.1 (domain-product-list) · Conflito de namespace nos testes**
- Erro: `CS0118 - "ProductList" é um namespace, mas é usado como um tipo` em múltiplos arquivos de teste
- Causa: o namespace dos testes `PriceWatch.UnitTests.Application.UseCases.ProductList` contém `ProductList` como parte do caminho, fazendo o compilador resolver `ProductList` como namespace em vez do tipo `PriceWatch.Domain.Entities.ProductList`
- Correção: adicionado alias `using DomainProductList = PriceWatch.Domain.Entities.ProductList` nos quatro arquivos de teste dentro desse namespace
