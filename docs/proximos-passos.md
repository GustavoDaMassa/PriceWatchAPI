# PriceWatch — Próximos passos do dev

> Atualizado em 02/10/2026. Tarefas que só o dev consegue fazer (cadastros, contas, decisões) e o roteiro de testes que o Claude acompanha assim que as credenciais saírem.
> Ordem sugerida: 1 → 2 → 3 em paralelo com 4 a 7. Marque cada item ao concluir.

## 1. Verificar o programa de afiliados de cada loja

Para cada loja, preencher uma linha da tabela abaixo (cópia para seguir preenchendo; o que não for encontrado fica "?"). Onde olhar: painel do afiliado, documentação para desenvolvedores, termos do programa e suporte da rede.

**Ordem:** Lomadee → Awin → Mercado Livre → Magalu → Shein → Casas Bahia / Americanas / Ponto → Amazon.

| Campo | O que responder |
|---|---|
| Rede e programa | Lomadee, Awin, programa próprio? Link do programa |
| Aprovada? | Pedido enviado, aprovada, recusada, em análise (data) |
| API de produto ou feed | Sim/não; formato (JSON, CSV, XML); frequência de atualização |
| Busca por GTIN | Existe filtro por EAN/GTIN? Ou só palavra-chave? |
| Consulta por URL ou id | Dá para consultar um produto direto (URL, id da oferta, SKU)? |
| Dados retornados | GTIN, marca, modelo, descrição, preço, imagem, estoque: quais vêm preenchidos? |
| Frescor do preço | Atraso do preço em relação ao site (horas, dia) |
| Link de afiliado por API | Existe geração programática? Limite por chamada? |
| Remuneração | CPA (% por venda), CPC (valor por clique), faixa de comissão por categoria |
| Prazo de pagamento | Validação e repasse (dias) |
| Limites de uso | Requisições por minuto/dia da API |
| Restrições | Comparadores de preço, links gerados automaticamente, extensões que alteram links, cupons |

### Resultado a preencher

| Loja | Rede/programa | Aprovada | API/feed | Busca GTIN | Consulta por URL/id | Retorna GTIN/marca/descrição | Frescor | Link por API | CPA/CPC | Restrições |
|---|---|---|---|---|---|---|---|---|---|---|
| Mercado Livre | próprio | ? | API oficial (já em uso) | ? | catálogo por id | GTIN/marca/modelo sim (spike Fase 3) | ? | **sem API oficial** | ? | ? |
| Kabum | Lomadee? Awin? | ? | ? | ? | ? | ? | ? | ? | ? | ? |
| Magalu | Parceiro Magalu | ? | ? | ? | ? | ? | ? | ? | ? | ? |
| Shein | Lomadee? próprio? | ? | ? | ? | ? | ? | ? | ? | ? | ? |
| Casas Bahia / Ponto | Lomadee? Awin? | ? | ? | ? | ? | ? | ? | ? | ? | ? |
| Americanas / Submarino | Lomadee? | ? | ? | ? | ? | ? | ? | ? | ? | ? |
| Amazon | Associados Amazon | ? | ? | ? | ? | ? | ? | ? | ? | ? |

### Perguntas que decidem a arquitetura

1. Kabum está na Lomadee, na Awin ou nas duas? (A pesquisa da sessão achou Kabum na Awin; na Lomadee não foi confirmado.) O `KabumFetcher` e o `LomadeeDeeplinkGenerator` foram escritos sobre a Lomadee.
2. Magalu tem API ou feed para afiliados, ou só painel manual?
3. **Mercado Livre:** existe no painel do programa um formato de link que se possa montar por regra (parâmetros fixos), ou só o gerador manual? (O ML não tem API pública de geração de link.)
4. Qual rede paga **CPC**? (A Afilio foi citada como a única com CPC explícito.)
5. Os termos de cada programa permitem comparador de preços e links gerados automaticamente a partir de uma ação do usuário?

## 2. Roteiro de teste das APIs (com o Claude, quando as credenciais saírem)

Credenciais ficam em variáveis de ambiente, nunca coladas em arquivo ou comando salvo. Para cada teste, guardar a resposta bruta (em arquivo fora do repositório) e anotar na tabela do item 1.

**2.1 Lomadee** (`https://api.lomadee.com/v2`, `APP_TOKEN` e `SOURCE_ID` do painel; ver documentação em developer.lomadee.com)

```bash
# Busca de ofertas por palavra-chave
curl -s "https://api.lomadee.com/v2/$LOMADEE_APP_TOKEN/offer/_search?sourceId=$LOMADEE_SOURCE_ID&keyword=notebook+vivobook&format=json"

# Geração de deeplink para uma URL de loja parceira
curl -s "https://api.lomadee.com/v2/$LOMADEE_APP_TOKEN/deeplink/_create?sourceId=$LOMADEE_SOURCE_ID&url=<URL_ENCODED_DA_LOJA>"
```

Anotar: lojas que aparecem nos resultados, campos presentes (`barcode`, `description`, `prices`, `link`, `shortUrl`), se existe id de oferta consultável direto, se há filtro por `barcode`, limites de requisição.

**2.2 Awin** (token de API do painel; ver help.awin.com/apidocs)

- Geração de link: `POST https://api.awin.com/publishers/{publisherId}/linkbuilder/generate` com o token no cabeçalho `Authorization: Bearer`, corpo com `advertiserId` e `destinationUrl` (campos exatos a conferir na documentação).
- Formato manual do link, para comparar: `https://www.awin1.com/cread.php?awinmid=<ADVERTISER>&awinaffid=<PUBLISHER>&ued=<URL_ENCODED>`.
- Feed de produtos: baixar o feed do anunciante aprovado (Product Feed pelo painel) e anotar colunas (GTIN/EAN, marca, preço, URL), tamanho do arquivo e frequência de atualização.

**2.3 Mercado Livre**

- Catálogo por id: o que o `MercadoLivreFetcher` já faz; confirmar GTIN, marca, modelo.
- Busca por GTIN no catálogo: testar se a pesquisa de produtos aceita identificador de produto (EAN) e o que devolve. Se funcionar, a descoberta partindo de outra loja para o ML é por GTIN.
- Link de afiliado: abrir o painel do programa, gerar dois links para produtos diferentes e comparar os parâmetros. Se o formato for estável e montável por regra, documentar; se for opaco, o ML segue sem afiliado na v1.

**2.4 Smoke test ponta a ponta (Kabum)**

Com `Lomadee__AppToken` e `Lomadee__SourceId` configurados num ambiente local, enviar uma URL real da Kabum em `POST /api/offers` e conferir: oferta criada, preço, GTIN preenchido e link de afiliado gerado. Se o formato divergir do documentado, só `KabumFetcher.cs` e `LomadeeDeeplinkGenerator.cs` precisam de ajuste.

**2.5 Amostra de qualidade dos dados**

Pegar 30 produtos reais (mix de eletrônicos e moda) e medir, por loja, quantos retornam GTIN, marca, descrição e preço. É esse número que decide se o matching precisa de camadas extras.

## 3. Conferir lojas oficiais

- Lista oficial de marcas da Lomadee: `https://www.lomadee.com.br/pt-br/marcas`. Conferir Kabum, Magalu, Shein, Casas Bahia, Americanas.
- Programa Kabum: `https://www.kabum.com.br/hotsite/afiliados/`.
- Awin Brasil: anunciantes disponíveis e requisitos de aprovação.

## 4. CNPJ/MEI e contador

- Abrir CNPJ ou MEI; as redes de afiliados costumam pedir CPF ou CNPJ no cadastro.
- Perguntar ao contador: enquadramento da receita de afiliado (a atividade cabe no MEI?), limite de faturamento, emissão de nota, imposto devido.
- Custo informativo: imposto fixo do MEI na faixa de R$ 80 a 90 por mês (confirmar o valor vigente).

## 5. Google Play

- Criar conta de desenvolvedor (taxa única de US$ 25; confirmar o valor na hora).
- Verificar os requisitos atuais de publicação para contas novas (testes fechados antes da produção, se exigidos).

## 6. Servidor

- Escolher o provedor do VPS no Brasil (2 vCPU / 4 GB). Candidatos citados na pesquisa: Hostinger (São Paulo, R$ 27,99 no plano anual de 1 vCPU / 4 GB), Serverspace, AWS Lightsail.
- Perguntar o preço de **renovação** (o plano anual costuma ser promocional) e se há plano de 2 vCPU.
- Decidir o destino de backup (armazenamento de objetos) e a região.

## 7. Jurídico e produto

- Política de privacidade e termos de uso (LGPD): dados guardados são e-mail, nome, listas e histórico de interesse (ver `analise-financeira.md`). O `DeleteAccountUseCase` já cobre a exclusão de conta.
- Ler os termos de cada programa de afiliados sobre comparadores, links automáticos e extensões **antes** de integrar.
- Decidir o push FCM na v1 (recomendado: sim; custo zero e é o que avisa o usuário com o app fechado).

## 8. O que o Claude faz com você

- Prepara scripts de teste a partir do item 2 quando as credenciais saírem e roda junto com você, sem exibir segredos.
- Preenche a tabela do item 1 com as respostas reais e atualiza `arquitetura.md` e `requisitos.md` com o que cada rede suporta.
- Ajusta `KabumFetcher.cs`/`LomadeeDeeplinkGenerator.cs` se o formato real divergir, e cria o `AwinLinkGenerator` se a Awin for aprovada.

## 9. Firebase (push) e Amazon SES (e-mail): guia passo a passo

O código já está pronto e testado com simulações; falta só você criar as contas e me passar o resultado **sem colar chaves aqui** (guarde em variáveis de ambiente).

### 9.1 Firebase Cloud Messaging (push no Android)

1. Acesse `console.firebase.google.com`, **Adicionar projeto** (ex.: "PriceWatch"). Pode desativar o Google Analytics.
2. Em **Configurações do projeto → Contas de serviço → Gerar nova chave privada**: baixa um arquivo `.json`. **É um segredo**: não commite, não envie por chat.
3. Anote o **ID do projeto** (`project_id` dentro do JSON, também em Configurações do projeto).
4. Converta o JSON em uma linha base64 para o `.env` do servidor:
   ```bash
   base64 -w0 caminho/para/chave-de-servico.json
   ```
5. No `.env` do servidor (`~/servidor/pricewatch/.env`):
   ```
   FCM_PROJECT_ID=<id do projeto>
   FCM_SERVICE_ACCOUNT_JSON_BASE64=<saída do comando acima>
   ```
   e recrie a API: `docker compose -f docker-compose.prod.yml up -d pricewatch-api`.
6. Para o app Android (marco M7) você também vai registrar o app Android no mesmo projeto e baixar o `google-services.json`; isso será guiado quando chegarmos lá.

### 9.2 Amazon SES (e-mail)

1. Na AWS, abra o **SES** na região `sa-east-1` (São Paulo) e, em **Identidades verificadas**, crie uma identidade de **domínio** (ex.: `gustavohdev.com.br`).
2. O SES mostra registros **DKIM** (3 CNAMEs). Cadastre-os no DNS da Cloudflare (modo "somente DNS", nuvem cinza). Adicione também um registro **SPF** (`v=spf1 include:amazonses.com ~all`) e, se ainda não tiver, um **DMARC** simples.
3. Aguarde a identidade ficar "Verificada". Em **Configurações da conta → Acesso ao sandbox**, peça a **saída do sandbox** (sem isso o SES só envia para endereços verificados).
4. Em **SMTP settings → Criar credenciais SMTP**: guarda usuário e senha SMTP (diferentes das chaves de acesso da AWS).
5. No `.env` do servidor:
   ```
   SMTP_HOST=email-smtp.sa-east-1.amazonaws.com
   SMTP_PORT=587
   SMTP_USER=<usuário SMTP>
   SMTP_PASSWORD=<senha SMTP>
   SMTP_FROM=PriceWatch <alertas@gustavohdev.com.br>
   ```
   (`SMTP_FRONTEND_BASE_URL` continua apontando para o endereço da SPA.)
6. Teste: crie uma conta nova pela SPA e confira se o e-mail de verificação chega; veja `docker compose logs pricewatch-api` se não chegar.
