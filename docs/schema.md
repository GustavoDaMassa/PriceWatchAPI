# PriceWatch — Schema MongoDB

> MongoDB é schemaless. Este documento descreve o schema aplicado por convenção,
> os índices criados pelo `MongoDbIndexInitializer` e as regras de integridade
> garantidas pela camada de domínio.

---

## Collections

### `users`

| Campo | Tipo | Obrigatório | Observação |
|---|---|---|---|
| `_id` | string (GUID) | sim | gerado por `User.Create()` |
| `name` | string | sim | |
| `email` | string | sim | único — índice `idx_users_email_unique` |
| `passwordHash` | string | sim | BCrypt |
| `isEmailVerified` | bool | sim | false até `VerifyEmail()` ser chamado |
| `emailVerificationToken` | string? | não | null após verificação |
| `tokenExpiresAt` | DateTime? | não | null após verificação |
| `createdAt` | DateTime | sim | UTC |

**Índices:**
| Nome | Campos | Opções |
|---|---|---|
| `idx_users_email_unique` | `email asc` | unique |

---

### `product_lists`

| Campo | Tipo | Obrigatório | Observação |
|---|---|---|---|
| `_id` | string (GUID) | sim | |
| `userId` | string | sim | ref → `users._id` |
| `name` | string | sim | |
| `description` | string? | não | |
| `createdAt` | DateTime | sim | UTC |

**Índices:**
| Nome | Campos | Opções |
|---|---|---|
| `idx_product_lists_userId` | `userId asc` | — |

---

### `tracked_products`

| Campo | Tipo | Obrigatório | Observação |
|---|---|---|---|
| `_id` | string (GUID) | sim | |
| `listId` | string | sim | ref → `product_lists._id` |
| `userId` | string | sim | ref → `users._id` |
| `url` | string | sim | |
| `source` | int (enum) | sim | `ProductSource`: 0=MercadoLivre, 1=Kabum, 2=Manual |
| `name` | string | sim | |
| `imageUrl` | string? | não | |
| `targetPrice` | decimal | sim | |
| `currentPrice` | decimal | sim | atualizado por `RecordPrice()` |
| `lowestPrice` | decimal | sim | mínimo histórico; atualizado por `RecordPrice()` |
| `checkIntervalHours` | int | sim | fixo: 1 |
| `nextCheckAt` | DateTime | sim | UTC; avançado por `RecordPrice()` |
| `lastCheckedAt` | DateTime? | não | UTC |
| `isActive` | bool | sim | false via `Deactivate()` |
| `metadata` | object | sim | dict string→string; usos futuros |
| `createdAt` | DateTime | sim | UTC |

**Índices:**
| Nome | Campos | Uso |
|---|---|---|
| `idx_tracked_products_check_queue` | `isActive asc, nextCheckAt asc` | `PriceCheckWorker` — query crítica |
| `idx_tracked_products_listId` | `listId asc` | `GetProductsByListUseCase` |
| `idx_tracked_products_userId` | `userId asc` | verificações de ownership |

---

### `price_snapshots`

| Campo | Tipo | Obrigatório | Observação |
|---|---|---|---|
| `_id` | string (GUID) | sim | |
| `productId` | string | sim | ref → `tracked_products._id` |
| `price` | decimal | sim | |
| `capturedAt` | DateTime | sim | UTC |

**Índices:**
| Nome | Campos | Uso |
|---|---|---|
| `idx_price_snapshots_productId_capturedAt` | `productId asc, capturedAt desc` | `GetPriceHistoryUseCase` |

---

### `notifications`

| Campo | Tipo | Obrigatório | Observação |
|---|---|---|---|
| `_id` | string (GUID) | sim | |
| `userId` | string | sim | ref → `users._id` |
| `productId` | string | sim | ref → `tracked_products._id` |
| `productName` | string | sim | snapshot do nome no momento do alerta |
| `type` | int (enum) | sim | `NotificationType`: 0=TargetPriceReached, 1=NewLowestPrice |
| `message` | string | sim | gerado por `Notification.Create()` com base no type |
| `isRead` | bool | sim | false até `MarkAsRead()` |
| `createdAt` | DateTime | sim | UTC |

**Índices:**
| Nome | Campos | Uso |
|---|---|---|
| `idx_notifications_userId_isRead` | `userId asc, isRead asc` | `GetNotificationsUseCase` com filtro |
| `idx_notifications_userId_createdAt` | `userId asc, createdAt desc` | listagem ordenada por data |

---

### `products` (catálogo compartilhado — pivô multi-loja, implementado)

| Campo | Tipo | Obrigatório | Observação |
|---|---|---|---|
| `_id` | string (GUID) | sim | |
| `gtin` | string? | não | chave de match EAN-first |
| `name` | string | sim | |
| `brand` | string? | não | |
| `model` | string? | não | |
| `imageUrl` | string? | não | |
| `matchStatus` | int (enum) | sim | `MatchStatus`: Unmatched, AutoMatched, PendingReview, Confirmed, Rejected |
| `suggestedMatchProductId` | string? | não | preenchido só no fuzzy match (`PendingReview`) |
| `createdAt` | DateTime | sim | UTC |

**Índices:** `idx_products_gtin` (`gtin asc`, não único).

---

### `store_offers` (implementado)

| Campo | Tipo | Obrigatório | Observação |
|---|---|---|---|
| `_id` | string (GUID) | sim | |
| `productId` | string | sim | ref → `products._id` |
| `source` | int (enum) | sim | `ProductSource`: 0=MercadoLivre, 1=Kabum, 2=Manual (só cresce no final, RNF14) |
| `url` | string | sim | |
| `gtin` | string? | não | |
| `currentPrice` / `lowestPrice` | decimal | sim | atualizados por `RecordPrice()` |
| `affiliateUrl` | string? | não | cache do link de afiliado |
| `checkIntervalHours` | int | sim | padrão 1; pode ser aumentado por oferta |
| `nextCheckAt` | DateTime | sim | UTC |
| `lastCheckedAt` | DateTime? | não | UTC |
| `isActive` | bool | sim | |
| `createdAt` | DateTime | sim | UTC |

**Índices:** `idx_store_offers_productId` (`productId asc`), `idx_store_offers_check_queue` (`isActive asc, nextCheckAt asc`), `idx_store_offers_source_url_unique` (`source asc, url asc`, único).

---

### `user_product_subscriptions` (implementado)

| Campo | Tipo | Obrigatório | Observação |
|---|---|---|---|
| `_id` | string (GUID) | sim | |
| `userId` | string | sim | ref → `users._id` |
| `productId` | string | sim | ref → `products._id` |
| `listId` | string? | não | ref → `product_lists._id` |
| `targetPrice` | decimal | sim | |
| `isActive` | bool | sim | |
| `targetAlertSent` | bool | sim | evita alerta repetido por queda |
| `createdAt` | DateTime | sim | UTC |

**Índices:** `idx_subscriptions_userId_productId_unique` (`userId asc, productId asc`, único), `idx_subscriptions_listId` (`listId asc`).

> Nota sobre `price_snapshots`: no código atual `StoreOffer.RecordPrice()` cria o snapshot com o id da **oferta** no campo `productId` (`PriceSnapshot.Create(Id, price)`), e `TrackedProduct` faz o mesmo com o id do produto legado. O campo mantém o nome `productId` nas duas situações, e o índice continua `productId asc, capturedAt desc`.

---

## Coleções planejadas para a v1 (ainda **não** implementadas)

Descritas em `arquitetura.md` ("Arquitetura da v1") e `modelagem.md`. Este documento só passará a tratá-las como implementadas quando o código existir.

| Coleção | Campos principais | Índices previstos |
|---|---|---|
| `offer_candidates` | `productId`, `source`, `url`, snapshot do fetch (nome, preço, GTIN, marca, modelo, imagem), `score`, `reason` (`Gtin`/`Keyword`), `expiresAt` | `productId`; TTL em `expiresAt`; único `productId+source+url` |
| `offer_match_feedback` | `userId`, `candidateId`, `productId`, `url`, `verdict`, `track`, `createdAt` | único `userId+candidateId` |
| `click_logs` | `offerId`, `userId`, `createdAt` | `offerId+createdAt` |
| `device_tokens` | `userId`, `token`, `platform`, `createdAt` | `userId`; único `token` |

---

## Integridade referencial

MongoDB não tem foreign keys. As regras são garantidas pela camada de Application:

| Regra | Garantida por |
|---|---|
| `product_lists.userId` deve existir em `users` | `[Authorize]` + JWT válido na requisição |
| `tracked_products.listId` deve existir em `product_lists` | `GetByIdAsync` antes de `CreateAsync` em `AddProductUseCase` |
| Remoção de lista em cascata (produtos vinculados) | `DeleteListUseCase` → `IProductListRepository.DeleteAsync` deve deletar os tracked_products |
| `notifications.userId` deve existir | criada apenas pelo `ProcessAlertUseCase` a partir de produto já validado |

---

## Inicialização

Os índices são criados pelo `MongoDbIndexInitializer` durante o startup da aplicação (antes de `app.Run()`). A operação é idempotente — executar múltiplas vezes não cria índices duplicados.

```csharp
// Program.cs
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider
        .GetRequiredService<MongoDbIndexInitializer>()
        .InitializeAsync();
}
```
