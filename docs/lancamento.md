# PriceWatch v1: beta fechado e lançamento (M10)

> Estado em 03/10/2026: todo o código da v1 está pronto e mergeado nas `main` locais (API, SPA, extensão, app, landing), **sem push**. O que falta é validação externa (Fase 0) e execução em produção, que dependem do dev. Este documento é o roteiro dessa reta final. Detalhes de infraestrutura: `runbook-vps.md`; cutover do catálogo: `runbook-cutover.md`; cadastros e contas: `proximos-passos.md`.

## 1. Portões de lançamento (todos obrigatórios)

| # | Portão | Como comprovar |
|---|---|---|
| 1 | Pelo menos uma rede de afiliados aprovada com link gerado por API (Lomadee) e ML com decisão de link tomada | Smoke test de `LomadeeDeeplinkGenerator` com link que abre a loja com o `sourceId` |
| 2 | Buscas reais funcionando: ML (GTIN/catálogo) e Kabum (Lomadee) | Script de smoke test (seção 2) com resultados plausíveis |
| 3 | Seletores da Kabum validados no site real (extensão e app) | Botão aparece e envia a URL canônica em 3 produtos reais |
| 4 | Push real: Firebase configurado, token registrado, push recebido no aparelho | Alerta de teste chega com app fechado |
| 5 | E-mail real (SES): domínio verificado, SPF/DKIM ok, mensagem não cai em spam | Verificação de e-mail e alerta chegam na caixa de entrada (Gmail e Outlook) |
| 6 | Produção no VPS conforme `runbook-vps.md` seção 9 | Checklist de aceite marcado |
| 7 | Cutover do catálogo unificado concluído e estável por 7 dias | `runbook-cutover.md`; sem erro 5xx no log |
| 8 | Backup restaurado com `verify-backup.sh` | Data do teste registrada no runbook |
| 9 | Política de privacidade e termos publicados e revisados | Revisão jurídica ou do contador; landing no ar |
| 10 | Extensão v2.0.0 aprovada na Chrome Web Store (só após o cutover) e app em teste fechado/produção na Google Play | Painéis das lojas |
| 11 | `/security-review` e `/code-review` rodados nos 4 repositórios | Achados críticos resolvidos; tokens do GitHub nas URLs dos remotes `pw` corrigidos |
| 12 | Beta fechado sem erro crítico (seção 4) | Métricas da seção 3 e lista de problemas zerada |

## 2. Smoke test das integrações reais

Rode no seu computador com as credenciais em variáveis de ambiente (nunca em arquivo versionado). Cada resposta registrada deve virar ajuste em `KabumFetcher`, `LomadeeDeeplinkGenerator` ou nos `IOfferSearcher`.

1. **Importar por link**: `POST /api/offers` com um link do ML e outro da Kabum (um deles curto: `meli.la/...`). Esperado: nome, preço, imagem, GTIN quando houver.
2. **Link de afiliado**: abra `POST /api/offers/{id}/click`, siga a URL devolvida e confira, na loja, o parâmetro de afiliado; depois confira o clique no painel da rede (pode demorar horas).
3. **Descoberta**: `POST /api/products/{id}/discover` para um produto com GTIN e outro sem. Esperado: candidatas plausíveis; nenhuma entra em `store_offers` sem voto (RI08).
4. **Monitoramento**: espere um ciclo do `StoreOfferPriceCheckWorker` (1 h) e confira `lastCheckedAt` e a ausência de erros no log.
5. **Alerta**: defina preço-alvo acima do atual; confirme push, e-mail e item na central.

## 3. Métricas do beta (consultas no `mongosh`, somente leitura)

```javascript
// cliques por loja nos últimos 7 dias (base da receita de afiliados)
db.click_logs.aggregate([
  { $match: { createdAt: { $gte: new Date(Date.now() - 7*24*3600*1000) } } },
  { $lookup: { from: 'store_offers', localField: 'offerId', foreignField: '_id', as: 'o' } },
  { $unwind: '$o' },
  { $group: { _id: '$o.source', cliques: { $sum: 1 }, usuarios: { $addToSet: '$userId' } } },
  { $project: { cliques: 1, usuarios: { $size: '$usuarios' } } }
])
// usuários ativos (com pelo menos uma assinatura) e produtos por usuário
db.user_product_subscriptions.aggregate([{ $group: { _id: '$userId', itens: { $sum: 1 } } }, { $group: { _id: null, usuarios: { $sum: 1 }, mediaItens: { $avg: '$itens' } } }])
// qualidade da descoberta: proporção de "é o mesmo produto" por motivo
db.offer_match_feedback.aggregate([{ $group: { _id: '$verdict', n: { $sum: 1 } } }])
// ofertas sem verificação há mais de 3 h (worker travado ou fonte fora do ar)
db.store_offers.countDocuments({ isActive: true, lastCheckedAt: { $lt: new Date(Date.now() - 3*3600*1000) } })
```
Confira os nomes exatos dos campos no `schema.md` antes de rodar (as consultas seguem os nomes dos documentos). A conversão real (compra) só aparece no painel da rede de afiliados; cruze com `click_logs` por dia.

Metas sugeridas para decidir o lançamento aberto (ajuste com os números reais): mais de 60% dos usuários do beta com 2 ou mais produtos; "mesmo produto" confirmado em mais de 70% das candidatas com GTIN; zero alerta perdido; 0 erros 5xx não explicados.

## 4. Beta fechado

- **Quem**: 10 a 20 pessoas conhecidas, avisadas de que é beta (preço pode atrasar, bugs são esperados). Peça a cada uma que acompanhe 3 produtos reais que pretendem comprar.
- **Canais**: SPA e extensão (Web Store como "não listada") e app pelo teste fechado da Google Play (o Google exige, para contas pessoais novas, um período de teste fechado com testadores; confirme a regra vigente no painel).
- **Duração**: 2 a 3 semanas. Duas verificações semanais: métricas da seção 3, `docker compose logs api | grep -i -E "error|warn"` e fila de problemas relatados.
- **Rollout controlado**: ligue as flags na ordem do `runbook-cutover.md`; a comparação multi-loja pode ficar visível só para o beta (SPA/app com a rota de candidatas) até os portões 2 e 3 estarem verdes.
- **Critério de saída**: todos os portões da seção 1 verdes e nenhuma falha crítica aberta.

## 5. Dia do lançamento

1. Confirme o backup da noite anterior (`verify-backup.sh` do mais recente).
2. Publique a extensão (se estava como não listada) e envie o app para produção na Play.
3. `git push` dos repositórios na ordem: API (Watchtower atualiza o VPS), SPA (Pages publica), landing. **O push só acontece com a sua ordem explícita.**
4. Observe 2 horas: uptime, logs, primeiro cadastro real, primeiro clique registrado.

## 6. Plano de rollback

| Problema | Ação |
|---|---|
| Bug grave após deploy da API | Fixar a imagem anterior em `API_IMAGE` (tag da versão boa) e `docker compose up -d api`; Watchtower só segue `latest` |
| Catálogo unificado com dados inconsistentes | Desligar `FEATURE_UNIFIED_CATALOG_ENABLED` (volta ao modelo legado, que o backfill não apagou) e reiniciar a API |
| Descoberta gerando candidatas ruins | Desligar `FEATURE_MATCHING_ENABLED`; o restante segue funcionando |
| Banco corrompido ou perdido | `runbook-vps.md` seção 7 (recuperação de desastre) |
| Extensão com defeito | Reverter para a versão anterior no painel da Web Store e publicar correção |
| VPS fora do ar por longo período | Subir a stack no home server (`docker-compose.prod.yml`) com o último backup e mover o hostname do túnel |

## 7. Depois do lançamento

Acompanhar a receita por loja, medir a taxa de acerto da descoberta sem GTIN e só então decidir sobre embeddings no matching (fora da v1), novas lojas (Awin/Magalu/Shein, cada uma com nova revisão da extensão) e iOS.
