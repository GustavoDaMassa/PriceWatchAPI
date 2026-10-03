# Runbook: cutover para o catálogo unificado (M1)

> Quem executa: o dev, no servidor (home server hoje; VPS depois). Ações em produção (SSH, Docker, Mongo, `.env`) o Claude não executa; ele prepara os comandos.
> Pré-requisito: a imagem com o M1 já está no ar (merge na `main` + Watchtower) **com todas as flags desligadas**, que é o padrão. Nada muda para os usuários até a etapa 2.

Convenções: diretório `~/servidor/pricewatch`, compose `docker-compose.prod.yml`, containers `pricewatch-api` e `pricewatch-mongo`. Substitua `$MONGO_USER`, `$MONGO_PASSWORD` e `$MONGO_DATABASE` pelos valores do `.env` (não cole esses valores em chats nem em commits).

## Etapa 0: backup

```bash
cd ~/servidor/pricewatch
mkdir -p ~/backups
docker compose -f docker-compose.prod.yml exec -T pricewatch-mongo \
  mongodump -u "$MONGO_USER" -p "$MONGO_PASSWORD" --authenticationDatabase admin \
  --db "$MONGO_DATABASE" --archive --gzip > ~/backups/pricewatch-pre-cutover-$(date +%F-%H%M).archive.gz
ls -lh ~/backups | tail -3
```

Restauração, se necessária (apaga o banco atual): `mongorestore --archive --gzip --drop` com o mesmo arquivo.

## Etapa 1: backfill e matching (sem trocar quem atende os usuários)

1. No `.env`, acrescente:
   ```
   FEATURE_BACKFILL_ENABLED=true
   FEATURE_MATCHING_ENABLED=true
   ```
2. Atualize o compose e recrie só a API (Watchtower atualiza a imagem, mas variáveis novas exigem recriar):
   ```bash
   cd ~/servidor/pricewatch && git pull
   docker compose -f docker-compose.prod.yml up -d pricewatch-api
   docker compose -f docker-compose.prod.yml logs --tail=50 pricewatch-api | grep -i -E "backfill|error"
   ```
   Esperado no log: `Backfill: migrated N legacy tracked products...`. O backfill é idempotente.
3. Verifique as contagens (no `mongosh` do container):
   ```javascript
   // docker compose -f docker-compose.prod.yml exec pricewatch-mongo mongosh -u ... -p ... --authenticationDatabase admin pricewatch_db
   const tracked = db.tracked_products.countDocuments({});
   const subs = db.user_product_subscriptions.countDocuments({});
   const offers = db.store_offers.countDocuments({});
   print({ tracked, subs, offers });          // subs deve ser == tracked (ou um pouco menor se houver itens unificados pelo matching)
   // todo item legado deve ter assinatura com o MESMO id
   const legacyIds = db.tracked_products.find({}, { _id: 1 }).toArray().map(d => d._id);
   print("sem assinatura:", legacyIds.filter(id => !db.user_product_subscriptions.findOne({ _id: id })).length);
   ```
   Interprete "sem assinatura" > 0 com cuidado: só é aceitável para usuários com dois itens legados que o matching unificou no mesmo produto.
4. Deixe rodar algumas horas e confira `docker compose ... logs pricewatch-api` sem erros.

## Etapa 2: cutover

1. No `.env`: `FEATURE_UNIFIED_CATALOG_ENABLED=true`.
2. `docker compose -f docker-compose.prod.yml up -d pricewatch-api`
3. Confira:
   - Log: `Legacy price check disabled (Features:UnifiedCatalogEnabled=true); worker idle.` e nenhuma mensagem de erro em `StoreOfferPriceCheckWorker`.
   - Na SPA e na extensão, com a sua conta: os itens aparecem, é possível editar o preço-alvo, mover de lista e ver o histórico.
   - Em até 1 hora, `db.price_snapshots` ganha registros novos com `productId` igual a ids de `store_offers`.
4. Usuários podem ver a lista vazia por alguns segundos enquanto a API sobe e o backfill do startup roda.

## Rollback

1. `.env`: `FEATURE_UNIFIED_CATALOG_ENABLED=false` (pode manter backfill e matching) e `docker compose ... up -d pricewatch-api`.
2. O worker legado volta a monitorar os `tracked_products`. **Itens criados ou editados durante o modo unificado não existem no modelo legado** e ficam escondidos até um novo cutover. Se isso for inaceitável, restaure o backup da Etapa 0.

## Depois de estável (limpeza futura)

Após algumas semanas sem rollback: remover o `PriceCheckWorker`, a `LegacyProductCatalogFacade` e os casos de uso legados, e arquivar a coleção `tracked_products`.
