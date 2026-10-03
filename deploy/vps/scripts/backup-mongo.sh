#!/usr/bin/env bash
# Backup diário do MongoDB para um bucket S3-compatível (Backblaze B2, Cloudflare R2, Wasabi, S3...).
# Roda no VPS, no diretório do compose, via cron. Variáveis vêm do .env do compose (nunca no script).
#   0 3 * * * cd /opt/pricewatch && ./scripts/backup-mongo.sh >> /var/log/pricewatch-backup.log 2>&1
set -euo pipefail

cd "$(dirname "$0")/.."
set -a; . ./.env; set +a

: "${MONGO_USER:?}" "${MONGO_PASSWORD:?}" "${MONGO_DATABASE:?}"
: "${BACKUP_S3_ENDPOINT:?}" "${BACKUP_S3_BUCKET:?}" "${BACKUP_S3_ACCESS_KEY:?}" "${BACKUP_S3_SECRET_KEY:?}"
RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-14}"
MIN_BYTES="${BACKUP_MIN_BYTES:-1024}"

STAMP="$(date -u +%Y-%m-%dT%H%M%SZ)"
FILE="pricewatch-${STAMP}.archive.gz"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

echo "[$(date -u +%FT%TZ)] dump -> $FILE"
docker compose exec -T mongo mongodump \
  -u "$MONGO_USER" -p "$MONGO_PASSWORD" --authenticationDatabase admin \
  --db "$MONGO_DATABASE" --archive --gzip > "$TMP/$FILE"

SIZE="$(stat -c %s "$TMP/$FILE")"
if [ "$SIZE" -lt "$MIN_BYTES" ]; then
  echo "ERRO: dump com apenas $SIZE bytes (mínimo $MIN_BYTES); abortando sem enviar" >&2
  exit 1
fi

# AWS CLI em contêiner: nada para instalar no host. As credenciais passam por variável de ambiente.
aws_s3() {
  docker run --rm -v "$TMP:/data:ro" \
    -e AWS_ACCESS_KEY_ID="$BACKUP_S3_ACCESS_KEY" -e AWS_SECRET_ACCESS_KEY="$BACKUP_S3_SECRET_KEY" \
    -e AWS_DEFAULT_REGION="${BACKUP_S3_REGION:-auto}" \
    amazon/aws-cli --endpoint-url "$BACKUP_S3_ENDPOINT" "$@"
}

aws_s3 s3 cp "/data/$FILE" "s3://$BACKUP_S3_BUCKET/$FILE" --only-show-errors
echo "[$(date -u +%FT%TZ)] enviado ($SIZE bytes)"

# retenção: apaga do bucket o que passou do prazo
CUTOFF="$(date -u -d "-${RETENTION_DAYS} days" +%Y-%m-%d)"
aws_s3 s3 ls "s3://$BACKUP_S3_BUCKET/" | awk '{print $4}' | grep -E '^pricewatch-.*\.archive\.gz$' | while read -r name; do
  day="${name#pricewatch-}"; day="${day%%T*}"
  if [[ "$day" < "$CUTOFF" ]]; then
    echo "removendo $name (anterior a $CUTOFF)"
    aws_s3 s3 rm "s3://$BACKUP_S3_BUCKET/$name" --only-show-errors
  fi
done
echo "[$(date -u +%FT%TZ)] backup concluído"
