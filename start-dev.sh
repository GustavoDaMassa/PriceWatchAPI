#!/bin/bash
set -e

if [ -f local.env ]; then
  set -a
  source local.env
  set +a
fi

# o fluxo novo (colar link, comparar lojas, descobrir ofertas) só funciona com o catálogo unificado;
# para testar o modelo legado: Features__UnifiedCatalogEnabled=false ./start-dev.sh
export Features__UnifiedCatalogEnabled="${Features__UnifiedCatalogEnabled:-true}"

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
dotnet run --project src/PriceWatch.API/PriceWatch.API.csproj
