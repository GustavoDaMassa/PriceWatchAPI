#!/bin/bash
set -e

if [ -f local.env ]; then
  set -a
  source local.env
  set +a
fi

echo "Subindo infraestrutura..."
docker compose up -d

echo "Aguardando MongoDB ficar pronto..."
until docker exec pricewatch-mongo mongosh --quiet --eval "db.runCommand({ ping: 1 })" &>/dev/null; do
  sleep 1
done

echo ""
echo "Swagger:  http://localhost:5283/swagger"
echo "MailHog:  http://localhost:8025"
echo ""
echo "Compilando..."
dotnet build src/PriceWatch.API --no-restore -q

echo "Iniciando API..."
dotnet run --project src/PriceWatch.API --no-build
