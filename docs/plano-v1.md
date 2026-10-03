# PriceWatch — Plano até a v1

> Atualizado em 02/10/2026. Substitui o plano de 10 fases do pivô (Fases 1-6 concluídas na API; Fases 7-10 reorganizadas abaixo).
> Requisitos em `requisitos.md` (RF20-RF26, RNF15-RNF20), arquitetura em `arquitetura.md`, custos em `analise-financeira.md`, tarefas do dev em `proximos-passos.md`.

## Escopo da v1

**Entra:**
- API: import por link, capacidades por loja, descoberta em outras lojas, confirmação do usuário, link de afiliado automático, notificações (in-app, push, e-mail)
- SPA (Angular), extensão Chrome com novas lojas, app Android (Google Play), landing page
- Produção em VPS no Brasil

**Fica fora (v1.1 ou depois):**
- Scraping ou navegador headless no servidor
- Notificação por WhatsApp
- IA/embeddings no matching (camadas deterministas + confirmação do usuário bastam para medir)
- Firefox Android / Safari iOS, app iOS

## Princípios que guiam o escopo

1. **Lojas só entram se tiverem caminho oficial** de dados e de monitoramento: API da loja ou rede de afiliados (Lomadee, Awin). Sem caminho oficial, a loja não entra na v1.
2. **O usuário decide o match** quando não há GTIN. O feedback vira dado rotulado (`OfferMatchFeedback`).
3. **O servidor nunca acessa a página da loja.** Captura no cliente (extensão, WebView) é conveniência; a fonte de verdade do preço é a API ou o feed.
4. **Nada é rastreado automaticamente.** Ofertas descobertas são candidatas e só entram no `PriceCheckWorker` depois da confirmação do usuário.
5. **Migração aditiva continua**: a extensão publicada depende de `POST /api/products`.

## Fases

| Fase | Entrega | Depende de |
|---|---|---|
| **0 — Validação externa** (dev, em paralelo) | Cadastros, aprovações e testes de Lomadee, Awin, ML, Magalu, Shein; decisão das lojas da v1; CNPJ/MEI; conta Google Play; escolha do VPS | — |
| **A — API: import por link e capacidades** | Resolver link curto, identificar loja, registro de capacidades por loja, `ImportFromLinkUseCase`, geração de link de afiliado ao começar o acompanhamento (Lomadee; Awin se aprovada) | Fase 0 para lojas reais; mocks antes |
| **B — API: descoberta e feedback** | `IOfferSearcher` por loja, `DiscoverOffersUseCase`, oferta candidata, `ConfirmOfferMatchUseCase`, `OfferMatchFeedback`, `ClickLog` | A |
| **C — API: notificações** | `DeviceToken` + worker de push (FCM), central in-app, e-mail em produção (SES) | — |
| **D — SPA** | Colar link / seletor de lojas, tela de comparação com "é o mesmo produto?", central de notificações, `openUrl()` pelo endpoint de redirect | A, B (contrato pode ser mockado) |
| **E — Extensão** | Content script genérico + adapters, aba da loja com botão "Acompanhar" e retorno ao site, payload `/api/offers`, nova submissão na Web Store | A |
| **F — App Android** | Expo: login, WebView com botão e interceptação de checkout, Share Sheet, comparação, push, publicação no Google Play | A, B, C estáveis |
| **G — Landing** | Cloudflare Pages: proposta de valor e cadastro | — |
| **H — Produção** | VPS no Brasil, backups, observabilidade, LGPD, feature flag da comparação, beta fechado, métricas de clique | todas |

Fases C e G são independentes das demais e podem rodar em paralelo. A Fase 0 não bloqueia o desenvolvimento com mocks, mas **bloqueia o lançamento**: sem lojas e afiliados reais não há produto.

### Pendências herdadas do pivô, onde entram

| Pendência | Onde entra |
|---|---|
| Smoke test real da Lomadee/Kabum (`KabumFetcher`, `LomadeeDeeplinkGenerator`) | Fase 0 |
| Confirmar se Kabum e Magalu constam na lista oficial de marcas da Lomadee | Fase 0 |
| Resolução do `PendingReview` (antes "trabalho futuro") | Fase B: feedback do usuário |
| **Todo o código das Fases 3 a 6 do pivô está sem commit** no repositório da API (domínio `Product`/`StoreOffer`/`UserProductSubscription`, matching, Lomadee/Kabum, rate limiting, testes, `modelagem.md`, `rastreabilidade.md`), além de `docker-compose.prod.yml`; na SPA, `theme.scss` e `shell.component.scss` | **Antes de qualquer fase nova:** o dev decide o commit (commits atômicos, Conventional Commits, rodar o build completo antes) |
| Rollout gradual e métricas de afiliado | Fase H |
| Teste dedicado de retry/circuit breaker (débito menor, RNF11) | Fase A |

## Especificação da extensão (v1)

Estado atual (`manifest.json` v1.1.0): `host_permissions` só para `*.mercadolivre.com.br` e a API; um content script (`content-ml.js`) fixo no ML; popup com login próprio.

Alvo:
- **Content script genérico** (`content-generic.js`) lê JSON-LD `schema.org/Product` quando existir (nome, marca, GTIN, preço, descrição, imagem) e delega a **adapters por domínio** (`adapters/mercadolivre.js`, `adapters/kabum.js`, ...) para o que o JSON-LD não cobre.
- **Fluxo "abrir loja":** o site abre a URL da loja em aba nova com marcador `#pw=<id>`; a extensão detecta o marcador, mostra o botão "Acompanhar com PriceWatch" em destaque, envia os dados para `POST /api/offers`, foca a aba do site (`chrome.tabs.update`) e fecha a aba da loja.
- **Detecção da extensão pelo site:** mensagem de ping (`externally_connectable`); sem a extensão, o site oferece instalar ou usa o caminho de import por link.
- **Cada loja nova** exige adicionar `matches` e `host_permissions` e uma nova submissão na Chrome Web Store (limitação do Manifest V3). Agrupar lojas num único envio para reduzir revisões.
- **Não alterar a página da loja** além de injetar o botão (nada de esconder elementos nem reescrever links).

## Especificação do app Android (v1)

- **Stack:** React Native + Expo. Publicação só no Google Play (taxa única de US$ 25).
- **Entrada por link:** o usuário cola o link no app ou usa o Share Sheet (`expo-share-intent` ou equivalente). A API segue links curtos até a URL final.
- **WebView de captura:** abre a URL (ou a página inicial da loja escolhida), injeta o botão "Acompanhar", extrai os dados (mesmos adapters da extensão, empacotados como script injetado) e posta a mensagem ao app (`window.ReactNativeWebView.postMessage`); ao tocar, envia à API e fecha o WebView.
- **Interceptação de navegação** (`onShouldStartLoadWithRequest`): URLs de carrinho, checkout e login abrem fora do WebView (navegador do sistema ou app da loja), com aviso ao usuário. A página da loja não é modificada além do botão.
- **Notificações:** push via FCM + central in-app.
- **Compra:** sempre fora do WebView, pelo `GET /api/offers/{id}/redirect` (link de afiliado, clique registrado).

## Riscos

| Risco | Impacto | Mitigação |
|---|---|---|
| Lomadee/Awin não aprovam a conta ou não cobrem as lojas esperadas | Sem receita e sem lojas | Fase 0 antes de investir em clientes; plano B: Afilio e programas próprios |
| ML sem API de link de afiliado | ML sem comissão | Dev verifica o painel (`proximos-passos.md`); v1 pode lançar com ML sem afiliado |
| Intervalo de checagem de 1 hora multiplica chamadas às APIs | Estourar limite de requisições das redes e do ML | Medir cotas na Fase 0; aumentar o intervalo por oferta; usar feeds em lote onde existirem |
| `PriceSnapshot` cresce rápido com checagem horária | Disco do VPS | Gravar snapshot só quando o preço muda; retenção (ver `analise-financeira.md`) |
| Preço de feed com atraso | Alerta tardio ou divergente do site | Mostrar "preço atualizado há X" na UI (RNF18) |
| Regras dos programas proíbem comparadores, links automáticos ou extensões que alteram links | Conta banida | Ler os termos antes de cada integração; fluxo sempre iniciado pelo usuário |
| Extensão: nova revisão na Web Store a cada loja | Atraso | Agrupar lojas em lotes |
| Matching errado destrói a confiança | Usuário abandona | Sem GTIN nunca funde sozinho; confirmação do usuário |
| Seu tempo de manutenção dos adapters | Quebra quando a loja muda o HTML | JSON-LD primeiro; adapters só para o resto; testes com HTML salvo |

## Gates

Cada fase de código tem gate de aprovação do dev (regra do `Dev.md`). Lançamento da v1 exige: Fase 0 concluída para as lojas da v1, smoke test real de todas as integrações, beta fechado sem erro crítico e política de privacidade publicada.
