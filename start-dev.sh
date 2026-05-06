#!/bin/bash
set -e

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
echo "Iniciando API..."
dotnet run --project src/PriceWatch.API
