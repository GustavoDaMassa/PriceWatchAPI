# Runbook: produção no VPS (Brasil) e migração do home server (M9)

> Quem executa: o dev, no servidor. O Claude não acessa SSH/Docker/Mongo de produção; este roteiro foi escrito para ser colado passo a passo.
> Arquivos: `deploy/vps/` (`docker-compose.yml`, `nginx.conf`, `.env.example`, `scripts/`). O home server vira staging (continua com `docker-compose.prod.yml`).
> Decisão de entrada: tráfego pelo **Cloudflare Tunnel** (nenhuma porta aberta além do SSH). Alternativa: IP público com proxy laranja + certificado de origem; exige abrir 80/443 e configurar TLS no nginx.

## 1. Contratar e preparar o VPS

Requisito mínimo: 2 vCPU, 4 GB de RAM, 40 GB de disco, datacenter no Brasil, Ubuntu 24.04. Confirme o preço de **renovação** (não só o da promoção) e se há snapshot/backup do provedor.

```bash
ssh root@IP_DO_VPS
adduser gustavo && usermod -aG sudo gustavo
mkdir -p /home/gustavo/.ssh && cp ~/.ssh/authorized_keys /home/gustavo/.ssh/ && chown -R gustavo: /home/gustavo/.ssh
# SSH só por chave, sem root
sed -i 's/^#\?PermitRootLogin.*/PermitRootLogin no/; s/^#\?PasswordAuthentication.*/PasswordAuthentication no/' /etc/ssh/sshd_config
systemctl restart ssh
# firewall: só SSH entra (o tráfego web vem pelo túnel, de dentro para fora)
ufw default deny incoming && ufw allow OpenSSH && ufw --force enable
apt-get update && apt-get install -y unattended-upgrades fail2ban
```
Abra **outra** sessão e confirme `ssh gustavo@IP` antes de fechar a de root.

Docker:
```bash
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker gustavo   # sair e entrar de novo
```

## 2. Cloudflare Tunnel

1. Cloudflare Zero Trust > Networks > Tunnels > Create tunnel (Cloudflared) > nome `pricewatch-prod`.
2. Copie o **token** do túnel (vai para `CLOUDFLARE_TUNNEL_TOKEN` no `.env`; não cole em chats nem em commits).
3. Public hostname: `pricewatch-api.gustavohdev.com.br` > Service `HTTP` > `nginx:80`.
   (Enquanto o home server atende esse hostname, use um hostname temporário, p. ex. `pricewatch-api-new.gustavohdev.com.br`, para testar antes do corte.)

## 3. Subir a stack

```bash
sudo mkdir -p /opt/pricewatch && sudo chown gustavo: /opt/pricewatch
git clone <repo da API> /tmp/pw && cp -r /tmp/pw/deploy/vps/. /opt/pricewatch/ && rm -rf /tmp/pw
cd /opt/pricewatch
cp .env.example .env && chmod 600 .env
# gere segredos novos para a produção do VPS:
openssl rand -base64 48   # JWT_SECRET
openssl rand -base64 24   # MONGO_PASSWORD
nano .env
docker compose pull && docker compose up -d
docker compose ps          # api, mongo, redis devem aparecer "healthy"
docker compose logs --tail=50 api
curl -s https://pricewatch-api-new.gustavohdev.com.br/health/ready   # Healthy
```
Mantenha **todas as flags `FEATURE_*` em false** nesta etapa: o banco ainda está vazio e a migração dos dados vem a seguir.

## 4. Migração dos dados (home server -> VPS)

Janela curta: o corte é feito com a API antiga parada para não perder escritas.

No **home server**:
```bash
cd ~/servidor/pricewatch
docker compose -f docker-compose.prod.yml stop pricewatch-api     # congela as escritas
docker compose -f docker-compose.prod.yml exec -T pricewatch-mongo \
  mongodump -u "$MONGO_USER" -p "$MONGO_PASSWORD" --authenticationDatabase admin \
  --db "$MONGO_DATABASE" --archive --gzip > ~/pricewatch-migracao.archive.gz
scp ~/pricewatch-migracao.archive.gz gustavo@IP_DO_VPS:/opt/pricewatch/
```
No **VPS** (confira o nome do banco: se for diferente do `MONGO_DATABASE` do VPS, use `--nsFrom/--nsTo`):
```bash
cd /opt/pricewatch
docker compose exec -T mongo mongorestore -u "$MONGO_USER" -p "$MONGO_PASSWORD" \
  --authenticationDatabase admin --archive --gzip --drop < pricewatch-migracao.archive.gz
docker compose restart api
curl -s http://localhost/health/ready || docker compose exec api bash -c 'echo ok'
```
Verifique contagens iguais às do home server (`users`, `tracked_products`, `price_snapshots`) com `mongosh`. Os JWT antigos continuam válidos **só se** o `JWT_SECRET` for o mesmo; com segredo novo todos os usuários precisam logar de novo (aceitável e mais seguro, avise antes).

## 5. Virar o DNS

1. No túnel do VPS, troque o hostname temporário por `pricewatch-api.gustavohdev.com.br` (e remova esse hostname do túnel do home server para não haver dois donos).
2. Teste: `curl -s https://pricewatch-api.gustavohdev.com.br/health/ready` e um login real pelo site.
3. Home server: deixe a stack parada (não apague) por 7 dias como plano de rollback. Rollback = `docker compose -f docker-compose.prod.yml start` lá e devolver o hostname ao túnel antigo.
4. Só depois execute `runbook-cutover.md` (ligar as flags do catálogo unificado) **já no VPS**.

## 6. Front-end na Cloudflare Pages

SPA: projeto Pages ligado ao repositório, build `npm run build`, saída `dist/PriceWatchSPA/browser`, o arquivo `public/_redirects` já traz o fallback de SPA. `environment.prod.ts` deve apontar para `https://pricewatch-api.gustavohdev.com.br/api`. Landing: ver `PriceWatchLanding/README.md`. Domínio da SPA: `ALLOWED_ORIGIN_SITE` e `SMTP_FRONTEND_BASE_URL` no `.env` precisam bater com ele.

## 7. Backup diário e teste de restauração

Bucket S3-compatível (Backblaze B2, Cloudflare R2, Wasabi...) com chave de acesso **só para esse bucket**, preenchida em `BACKUP_S3_*` no `.env`.
```bash
cd /opt/pricewatch && ./scripts/backup-mongo.sh        # teste manual; confira o arquivo no bucket
crontab -e
# 0 3 * * * cd /opt/pricewatch && ./scripts/backup-mongo.sh >> /var/log/pricewatch-backup.log 2>&1
```
O script recusa enviar dumps suspeitos (menores que `BACKUP_MIN_BYTES`) e apaga do bucket o que passou de `BACKUP_RETENTION_DAYS` (padrão 14).

**Restauração testada é requisito de lançamento.** Baixe um backup do bucket e rode:
```bash
./scripts/verify-backup.sh pricewatch-2026-...archive.gz pricewatch_db
```
Ele sobe um Mongo descartável, restaura e lista as coleções com contagem; não toca na produção. Repita a cada mudança relevante de infraestrutura e registre a data aqui.

Recuperação de desastre (VPS perdido): novo VPS, seções 1-3, baixar o backup mais recente, `mongorestore --drop` (seção 4), apontar o túnel. Meta: restaurar em menos de 1 hora; perda máxima: 24 h de dados.

## 8. Observabilidade

- **Uptime**: monitor externo gratuito (UptimeRobot ou Better Stack) em `https://pricewatch-api.gustavohdev.com.br/health/ready`, a cada 5 min, alerta por e-mail. `/health/live` só diz que o processo responde; `/health/ready` confere Mongo e Redis.
- **Logs**: `docker compose logs -f api` (Serilog, com `CorrelationId` por requisição). Os logs do Docker rodam em disco: configure rotação em `/etc/docker/daemon.json` (`{"log-driver":"json-file","log-opts":{"max-size":"20m","max-file":"5"}}`) e reinicie o Docker.
- **Disco**: `df -h` e `docker system df` no cron semanal ou alerta do provedor. O crescimento esperado está em `analise-financeira.md`.
- **Atualizações**: o Watchtower atualiza só contêineres com o rótulo `com.centurylinklabs.watchtower.enable` (a API). Mongo, Redis e nginx sobem com versão fixa de major; atualize manualmente.

## 9. Checklist de aceite do VPS

- [ ] `docker compose ps`: api, mongo, redis `healthy`; nada publicado além de SSH (`ss -tlnp`).
- [ ] `/health/ready` responde `Healthy` pelo domínio público.
- [ ] Login real, adicionar produto por link e abrir comparação funcionando.
- [ ] Rate limit por IP separa visitantes (duas redes diferentes não compartilham o limite).
- [ ] Backup do bucket restaurado com `verify-backup.sh` (data registrada).
- [ ] Monitor de uptime ativo e alerta testado.
- [ ] `.env` com permissão 600 e fora de qualquer repositório.
