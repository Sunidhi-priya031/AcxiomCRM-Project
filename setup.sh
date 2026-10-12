#!/usr/bin/env bash
# AcxiomCRM - one-time local setup (macOS / Linux)
#   ./setup.sh                 # uses the default local demo password
#   ./setup.sh 'Your#Pass1'
set -euo pipefail
cd "$(dirname "$0")"
PROJECT=src/AcxiomCRM
DEMO_PASSWORD="${1:-Demo@12345}"

echo "1/4 Restoring packages and the dotnet-ef tool..."
dotnet tool restore
dotnet restore "$PROJECT/AcxiomCRM.csproj"

echo "2/4 Storing the local demo password in user-secrets (outside source control)..."
dotnet user-secrets set "Seed:DemoPassword" "$DEMO_PASSWORD" --project "$PROJECT" > /dev/null

echo "3/4 Creating the EF Core migration (first run only)..."
if [ ! -d "$PROJECT/Migrations" ]; then
  rm -f "$PROJECT"/App_Data/acxiomcrm.db*
  dotnet ef migrations add InitialCreate --project "$PROJECT" --output-dir Migrations
fi

echo "4/4 Applying migrations to App_Data/acxiomcrm.db..."
dotnet ef database update --project "$PROJECT"

echo
echo "Setup complete. Start the app with:  dotnet run --project $PROJECT"
echo "Then open http://localhost:5173 and sign in as admin@acxiomcrm.local with your demo password."
