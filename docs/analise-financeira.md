# PriceWatch — Análise financeira (v1 em nuvem)

> Atualizado em 02/10/2026. Premissa de câmbio: **US$ 1 = R$ 5,50** (atualizar). Preços de provedor vêm de pesquisa em 2026; o que não foi pesquisado está marcado como *estimativa*.
> Cenários: **Lançamento** (até ~1 mil usuários) e **Crescimento** (~10 mil). Hospedagem no Brasil, notificações só in-app + push + e-mail, app só no Google Play.

## Custos mensais

| Item | Lançamento | Crescimento | Observação |
|---|---|---|---|
| Servidor (VPS no Brasil) | R$ 28 a 60 | R$ 100 a 250 *(estimativa)* | Hostinger São Paulo: R$ 27,99/mês no plano anual (1 vCPU / 4 GB); renovação pode subir. Preferir 2 vCPU |
| MongoDB | R$ 0 (no VPS) | R$ 0 a 320 | Atlas M10 ~US$ 58/mês só de computação |
| Redis | R$ 0 (no VPS) | R$ 0 | Dados transitórios (rate limit, streams) |
| Backups | R$ 5 a 10 | R$ 10 a 30 | Armazenamento de objetos |
| Front-end (Cloudflare Pages) | R$ 0 | R$ 0 | Uso comercial permitido, banda ilimitada sujeita a uso justo |
| E-mail (Amazon SES) | R$ 0 a 5 | R$ 10 a 60 | US$ 0,10 por mil e-mails; Resend gratuito limita a 100 por dia |
| Push (FCM) | R$ 0 | R$ 0 | |
| Domínio / DNS | ~R$ 4 | ~R$ 4 | DNS na Cloudflare |
| Monitoramento e erros | R$ 0 | R$ 0 a 140 | Faixas gratuitas no início |
| **Infra** | **~R$ 40 a 80** | **~R$ 120 a 800** | |
| MEI (imposto fixo) | ~R$ 80 a 90 | ~R$ 80 a 90 | A confirmar com contador |
| **Total com MEI** | **~R$ 120 a 170** | — | |

## Custos únicos

| Item | Valor |
|---|---|
| Google Play (conta de desenvolvedor) | US$ 25 (~R$ 140); confirmar na hora |
| Chrome Web Store | já pago |
| Apple Developer | não entra (sem app iOS na v1) |

## Ponto de equilíbrio (ilustrativo)

Suposição: comissão média de **3%** sobre ticket de **R$ 800**, ou seja, **R$ 24 por venda**. A Lomadee cita 5% a 15% em geral, mas eletrônicos tendem a pagar menos; 3% é conservador.

| Cenário | Custo mensal | Vendas por mês para empatar |
|---|---|---|
| Lançamento | ~R$ 120 a 170 | ~5 a 7 |
| Crescimento | ~R$ 200 a 900 | ~8 a 38 |

Com conversão de clique em venda de 2% a 3%, 7 vendas exigem em torno de 230 a 350 cliques por mês. A conversão real é desconhecida e é o que mais pesa.

**Fluxo de caixa:** comissões de afiliado são pagas com atraso (validação costuma levar 30 a 60 dias) e podem ser canceladas em caso de devolução. A despesa começa antes da receita.

## Dados que o banco guarda e por quê

| Coleção | Dados | Por que existe |
|---|---|---|
| `users` | Nome, e-mail, hash da senha (BCrypt), status e token de verificação, data de criação | Autenticação e contato para alertas. **Dado pessoal (LGPD)** |
| `product_lists` | Nome e descrição das listas | Organização que o usuário criou |
| `products` | Nome, GTIN, marca, modelo, imagem, status de match | Catálogo compartilhado que viabiliza a comparação entre lojas |
| `store_offers` | Loja, URL, preço atual e mínimo, link de afiliado em cache, próxima checagem | Preço por loja e base da receita de afiliado |
| `user_product_subscriptions` | Usuário, produto, lista, preço-alvo, ativo | O que cada usuário acompanha e quando alertar |
| `price_snapshots` | Oferta, preço, data | Histórico de preço, o diferencial do produto |
| `notifications` | Usuário, produto, tipo, mensagem, lida | Central de notificações in-app |
| `click_logs` (novo) | Oferta, usuário, data | Mede conversão e receita de afiliado |
| `offer_candidates` (novo) | Produto, loja, URL, dados do fetch, pontuação, motivo, validade (TTL) | Ofertas encontradas em outras lojas, aguardando a confirmação do usuário; expiram sozinhas |
| `offer_match_feedback` (novo) | Usuário, oferta, produto, veredito | Confirmação "é o mesmo produto?" e dado rotulado para o matching |
| `device_tokens` (novo) | Usuário, token FCM, plataforma | Push no Android |
| `tracked_products` (legado) | Modelo antigo | Compatibilidade com a extensão já publicada |

**Redis** guarda só dados transitórios: contadores de rate limit e streams de eventos (`price-alerts`, `store-offer-events`).

### Crescimento do `price_snapshots`

**Correção da estimativa anterior:** o intervalo de checagem no código é de **1 hora** (`CheckIntervalHours = 1`, RF05), e não 6 horas.

| Intervalo | Registros por oferta por ano | Com 10 mil ofertas por ano | Espaço aproximado* |
|---|---|---|---|
| 1 hora (atual) | ~8.760 | ~88 milhões | ~10 GB por ano |
| 6 horas | ~1.460 | ~15 milhões | ~1,5 a 2 GB por ano |
| Só quando o preço muda | depende da volatilidade; tipicamente uma ordem de grandeza menor | ordem de ~1 a 10 milhões | < 2 GB por ano |

\* ~120 bytes por registro com índice, estimativa grosseira.

Um VPS de 50 GB aguenta o intervalo atual por poucos anos de histórico com 10 mil ofertas, mas o ganho de **gravar o snapshot só quando o preço muda** é grande e não perde informação (o gráfico continua correto entre mudanças). Fica registrado como decisão a tomar na Fase A/B. O catálogo é compartilhado: mil usuários acompanhando o mesmo produto geram um único conjunto de snapshots.

O intervalo de 1 hora também multiplica chamadas às APIs das lojas e das redes (10 mil ofertas = 240 mil consultas por dia): medir as cotas na Fase 0 (ver `proximos-passos.md`).

### LGPD

Dados pessoais: e-mail, nome e histórico de interesse. Necessário: política de privacidade, exclusão e exportação de conta (`DeleteAccountUseCase` já existe), backups protegidos. Confirmar o enquadramento com contador ou advogado.

## Decisões de custo

1. VPS único com Docker Compose (API, Mongo, Redis, nginx), backup diário para armazenamento de objetos.
2. Migrar o Mongo para o Atlas só quando a receita justificar a alta disponibilidade.
3. Home server fica como ambiente de staging.
4. App mobile só no Google Play; iOS fica fora da v1.

## Fontes

- Hostinger São Paulo, Serverspace, Locaweb: guias de VPS no Brasil 2026 (horadecodar.com.br, guiadohost.com, serverspace.com.br)
- AWS Lightsail: docs.aws.amazon.com/lightsail/latest/userguide/amazon-lightsail-bundles.html
- MongoDB Atlas: comparedge.com/tools/mongodb-atlas/pricing, budgetforge.dev
- Cloudflare Pages: temps.sh/blog/cloudflare-pages-free-tier-limits-2026, devtoolreviews.com
- AWS Amplify: toolradar.com/tools/aws-amplify/pricing
- Amazon SES e Resend: mailblast.io, nuntly.com/resend-pricing
- Vercel: costbench.com/software/developer-tools/vercel (uso comercial exige Pro; alternativa descartada)
