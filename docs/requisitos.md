# PriceWatch — Requisitos

## Requisitos Funcionais

- **RF01** Multi-user — cada usuário tem sua própria conta (email + senha)
- **RF02** Cadastro com verificação de email obrigatória antes de ativar a conta
- **RF03** Autenticação via JWT
- **RF04** Usuário cola URL de produto e define preço-alvo
- **RF05** Sistema monitora o preço automaticamente a cada 1 hora via Mercado Livre API oficial
- **RF06** Produtos organizados em listas/coleções nomeadas pelo usuário (ex: "Black Friday", "Setup")
- **RF07** Histórico de preços visível por produto (série temporal de snapshots)
- **RF08** Notificação por email quando condição de alerta é atingida
- **RF09** Notificação in-app consultável por polling
- **RF10** Dois critérios de alerta: preço-alvo atingido e novo menor preço histórico
- **RF11** Análise comparativa por lista: produtos ordenados por distância do alvo (qual vale mais comprar agora)
- **RF12** API-only — sem frontend no MVP

## Requisitos Funcionais — Pivô Multi-Loja (Modo Produto)

- **RF13** Produto-mestre (`Product`) agrega múltiplas ofertas (`StoreOffer`) de lojas diferentes referentes ao mesmo produto físico
- **RF14** Sistema casa automaticamente ofertas do mesmo produto entre lojas via GTIN/EAN quando disponível em ambas as ofertas
- **RF15** Quando GTIN não está disponível, sistema tenta casamento por similaridade (fuzzy: título/marca/categoria) e coloca o resultado em fila `PendingReview` — nunca publica match automático sem GTIN
- **RF16** Usuário visualiza comparação lado a lado das ofertas de um produto, com destaque visual para a oferta mais barata no momento
- **RF17** Ação de "ir para a loja" passa por endpoint de redirecionamento (`GET /api/offers/{id}/redirect`) que resolve o link de afiliado e registra o clique antes de encaminhar à loja original — nunca abre a URL original direto no cliente
- **RF18** Catálogo de `Product`/`StoreOffer` é compartilhado entre todos os usuários (não duplicado por usuário) — decisão de arquitetura validada com o dev
- **RF19** Usuário continua definindo preço-alvo e recebendo alertas por produto (herdado de RF04/RF05), agora aplicado à sua subscrição (`UserProductSubscription`) ao produto-mestre, não mais à oferta individual

## Requisitos Funcionais — Fluxo da v1 (descoberta, confirmação e afiliado)

- **RF20** Usuário cola um link de produto; o sistema identifica a loja, segue links curtos até a URL final e importa o produto (nome, preço, imagem e, quando existirem, GTIN, marca, modelo e descrição) pela fonte oficial da loja (API ou rede de afiliados). Se o produto já existe no catálogo (mesma loja e URL), ele é reaproveitado
- **RF21** Captura assistida no cliente: a extensão (aba da loja com botão "Acompanhar") e o app (WebView com botão "Acompanhar") enviam os dados do produto à API; após a ação, o usuário volta ao site ou à tela do app. Convive com RF20 — não é a única forma de entrada
- **RF22** Descoberta em outras lojas: ao começar a acompanhar um produto, o sistema procura o mesmo produto nas demais lojas suportadas — por GTIN quando houver e, sem GTIN, por palavra-chave (marca + modelo) — e apresenta os resultados como **ofertas candidatas**
- **RF23** Confirmação pelo usuário: para cada candidata, o usuário indica "é o mesmo produto" ou "não é" e decide se quer acompanhá-la. O veredito é gravado (`OfferMatchFeedback`). Só candidatas confirmadas passam a ser monitoradas e alertadas
- **RF24** Link de afiliado gerado automaticamente quando o acompanhamento começa (importação ou confirmação de candidata) e guardado em `StoreOffer.AffiliateUrl`. A compra segue por `GET /api/offers/{id}/redirect` (RF17)
- **RF25** Notificação por três canais: central in-app (consultável), push no app Android (FCM) e e-mail
- **RF26** Cada loja declara suas capacidades (buscar por URL, buscar por GTIN/palavra-chave, gerar link de afiliado); a interface e o fluxo se adaptam: loja sem busca não aparece na descoberta. Na v1 só entram lojas com API oficial ou rede de afiliados

## Requisitos Não-Funcionais

- **RNF01** Rate limiting por IP nos endpoints públicos e por usuário nos endpoints autenticados
- **RNF02** Resiliência no disparo de alertas via Redis Streams (não se perde se o consumer cair)
- **RNF03** Extensibilidade de fontes de preço via interface `IPriceFetcher` (sem alteração de Application/Domain)
- **RNF04** Domain sem dependências externas (Clean Architecture estrita)
- **RNF05** API documentada com Swagger/OpenAPI
- **RNF06** Variáveis sensíveis (JWT secret, SMTP credentials, MongoDB URI) via variáveis de ambiente — nunca hardcoded
- **RNF07** Containers definidos via Docker Compose para ambiente de desenvolvimento (MongoDB, Redis, MailHog)

## Requisitos Não-Funcionais — Pivô Multi-Loja (Modo Produto)

- **RNF08** Fontes de preço/catálogo priorizam feeds ou APIs oficiais com GTIN (agregador de afiliados ou API própria da loja) em vez de web scraping
- **RNF09** Rate limiting por IP (endpoints públicos) e por usuário (endpoints autenticados) efetivamente implementado — fecha o gap do RNF01, que estava documentado sem implementação correspondente no código
- **RNF10** Logging estruturado com correlação, necessário para depurar o motor de matching assíncrono e múltiplos fetchers concorrentes
- **RNF11** Retry com backoff e circuit breaker (Polly) em toda chamada HTTP a fonte externa (fetchers de preço, geradores de link de afiliado)
- **RNF12** Falha rápida (fail-fast) no startup se o segredo de JWT não estiver configurado via variável de ambiente em produção — sem fallback default silencioso
- **RNF13** Motor de matching roda de forma assíncrona (Redis Streams), sem bloquear a resposta HTTP de criação de oferta
- **RNF14** `ProductSource` (enum persistido) só cresce por adição de valores no final — nunca reordenar ou remover valores já gravados em produção

## Requisitos Não-Funcionais — Produção da v1

- **RNF15** Produção em VPS no Brasil com Docker Compose (API, MongoDB, Redis, nginx); front-end estático na Cloudflare Pages; home server vira ambiente de staging
- **RNF16** Backup diário do MongoDB para armazenamento de objetos, com restauração testada antes do lançamento
- **RNF17** Conformidade com a LGPD: política de privacidade, exclusão de conta (`DeleteAccountUseCase`), exportação dos dados do usuário e backups protegidos
- **RNF18** Transparência de frescor: a interface mostra há quanto tempo o preço foi atualizado e indica que preços vindos de feed ou rede de afiliados podem ter atraso em relação ao site da loja
- **RNF19** Buscas em APIs externas (descoberta, importação) só acontecem por ação do usuário, com rate limiting por usuário e cache por GTIN/URL, para respeitar cotas das redes e das lojas
- **RNF20** Nenhum cliente (extensão ou app) altera o conteúdo das páginas das lojas além de injetar o botão "Acompanhar"; no app, a compra é impedida **interceptando a navegação** para carrinho, checkout e login (abrem fora do WebView), nunca escondendo elementos da loja

## Requisitos Implícitos

- **RI01** Um produto pertence a um único usuário (isolamento de dados entre contas)
- **RI02** Alerta enviado a cada ocorrência da condição — sem cooldown no MVP
- **RI03** `nextCheckAt` persiste no produto para que o worker filtre no banco, sem carregar tudo em memória
- **RI04** Remoção de lista remove os produtos vinculados (cascade lógico)
- **RI05** Produto inativo (`isActive = false`) não é processado pelo worker
- **RI06** Oferta sem match (`MatchStatus = Unmatched`) continua funcionando como hoje — rastreamento solo, nunca forçada a aparecer numa comparação incorreta
- **RI07** Oferta em `PendingReview` é rastreada normalmente para o usuário que a adicionou (preço, alerta), mas não entra na tela de comparação até confirmação do match
- **RI08** Oferta candidata (descoberta, ainda não confirmada) **não** entra no `PriceCheckWorker` e não gera alerta; é só exibida na tela de confirmação
- **RI09** A confirmação do usuário (RF23) é o mecanismo de resolução do `PendingReview` — o fuzzy continua só sugerindo, nunca funde sozinho (RF15)

## Decisões de Produto

| # | Decisão |
|---|---|
| Frequência de verificação | 1 hora (fixo) |
| Fonte MVP | Mercado Livre API oficial |
| Notificação in-app | Polling — SSE descartado (eventos raros não justificam complexidade) |
| Cache de preços | Descartado no MVP — útil apenas para scraping futuro |
| Mensageria | Redis Streams — infra já existente para rate limiting |
| Cooldown de alertas | Sem cooldown — alerta a cada ocorrência |

## Decisões de Produto — Pivô Multi-Loja (Modo Produto)

| # | Decisão |
|---|---|
| Catálogo de produto/oferta | Compartilhado entre todos os usuários, não duplicado por usuário |
| Estratégia de matching v1 | GTIN/EAN-first; sem GTIN, fuzzy em fila `PendingReview` — nunca match automático sem GTIN |
| Lojas do v1 | O máximo possível de lojas brasileiras, via agregador de afiliados (Lomadee/Awin/Rakuten — escolha específica na Fase 6) + Mercado Livre via API/afiliados próprios (fora do agregador). **Refinada em 02/10/2026**: só entram lojas com API oficial ou rede de afiliados aprovada — ver "Decisões de Produto — v1" abaixo |
| Migração do modelo legado (`TrackedProduct`) | Aditiva/incremental via job de backfill — sem big-bang, há extensão publicada em produção |
| Stack do app mobile | React Native + Expo |
| Gaps de maturidade (RNF09–RNF12) | Pré-requisito bloqueante da Fase 3, antes de qualquer fetcher/matching novo |

## Decisões de Produto — v1 (02/10/2026)

| # | Decisão |
|---|---|
| Entrada do produto | Colar link (ou escolher a loja e navegar); extensão e app capturam os dados no cliente, mas a fonte de verdade do preço é a API ou o feed da loja |
| Lojas da v1 | Só lojas com API oficial ou rede de afiliados (Mercado Livre, Kabum e as aprovadas na Lomadee/Awin). Sem scraping nem navegador no servidor |
| Descoberta em outras lojas | GTIN primeiro; sem GTIN, busca por palavra-chave; sempre com confirmação do usuário ("é o mesmo produto?") |
| Matching sem GTIN | Camadas deterministas (marca, atributos numéricos, Levenshtein) + confirmação do usuário. Embeddings/IA ficam fora da v1; só serão avaliados depois de medir a taxa de acerto |
| Receita | Afiliado (Lomadee, Awin, ML) com CPC onde existir. Link gerado por API na Lomadee e na Awin; no ML, depende da verificação do painel do programa (`proximos-passos.md`) |
| Notificações | In-app + push (FCM) + e-mail; sem WhatsApp na v1 |
| Clientes | SPA, extensão Chrome (novas lojas), app Android (Google Play), landing page. Sem iOS, Firefox mobile |
| Hospedagem | VPS no Brasil, front-end na Cloudflare Pages, e-mail por SES; ver `analise-financeira.md` |
| Compra | Nunca dentro do WebView do app; sempre pelo redirect de afiliado, fora do WebView |
