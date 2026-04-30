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

## Requisitos Não-Funcionais

- **RNF01** Rate limiting por IP nos endpoints públicos e por usuário nos endpoints autenticados
- **RNF02** Resiliência no disparo de alertas via Redis Streams (não se perde se o consumer cair)
- **RNF03** Extensibilidade de fontes de preço via interface `IPriceFetcher` (sem alteração de Application/Domain)
- **RNF04** Domain sem dependências externas (Clean Architecture estrita)
- **RNF05** API documentada com Swagger/OpenAPI
- **RNF06** Variáveis sensíveis (JWT secret, SMTP credentials, MongoDB URI) via variáveis de ambiente — nunca hardcoded
- **RNF07** Containers definidos via Docker Compose para ambiente de desenvolvimento (MongoDB, Redis, MailHog)

## Requisitos Implícitos

- **RI01** Um produto pertence a um único usuário (isolamento de dados entre contas)
- **RI02** Alerta enviado a cada ocorrência da condição — sem cooldown no MVP
- **RI03** `nextCheckAt` persiste no produto para que o worker filtre no banco, sem carregar tudo em memória
- **RI04** Remoção de lista remove os produtos vinculados (cascade lógico)
- **RI05** Produto inativo (`isActive = false`) não é processado pelo worker

## Decisões de Produto

| # | Decisão |
|---|---|
| Frequência de verificação | 1 hora (fixo) |
| Fonte MVP | Mercado Livre API oficial |
| Notificação in-app | Polling — SSE descartado (eventos raros não justificam complexidade) |
| Cache de preços | Descartado no MVP — útil apenas para scraping futuro |
| Mensageria | Redis Streams — infra já existente para rate limiting |
| Cooldown de alertas | Sem cooldown — alerta a cada ocorrência |
