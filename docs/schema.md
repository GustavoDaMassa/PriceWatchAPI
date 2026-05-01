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
