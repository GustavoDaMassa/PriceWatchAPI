#!/usr/bin/env bash
# Prova que um backup restaura: sobe um MongoDB descartável, restaura o arquivo e compara as contagens.
# Uso: ./scripts/verify-backup.sh caminho/pricewatch-AAAA-MM-DDTHHMMSSZ.archive.gz [banco]
# Não toca no banco de produção. Rode no VPS ou numa máquina com Docker depois de baixar o arquivo do bucket.
set -euo pipefail

ARCHIVE="${1:?informe o arquivo .archive.gz}"
DB="${2:-pricewatch_db}"
NAME="pw-verify-$$"
cleanup() { docker rm -f "$NAME" >/dev/null 2>&1 || true; }
trap cleanup EXIT

docker run -d --name "$NAME" mongo:7 >/dev/null
for _ in $(seq 1 30); do
  docker exec "$NAME" mongosh --quiet --eval 'db.adminCommand({ping:1}).ok' >/dev/null 2>&1 && break
  sleep 1
done

docker exec -i "$NAME" mongorestore --archive --gzip --nsInclude "${DB}.*" < "$ARCHIVE" >/dev/null 2>&1
echo "Coleções restauradas em '$DB':"
docker exec "$NAME" mongosh --quiet "$DB" --eval '
  db.getCollectionNames().sort().forEach(c => print(c.padEnd(32), db[c].countDocuments({})))'

COUNT="$(docker exec "$NAME" mongosh --quiet "$DB" --eval 'print(db.getCollectionNames().length)')"
if [ "$COUNT" -lt 1 ]; then
  echo "ERRO: o backup restaurou 0 coleções" >&2
  exit 1
fi
echo "OK: backup restaurável ($COUNT coleções)."
