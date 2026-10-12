# AcxiomCRM - one-time local setup (Windows PowerShell)
#   .\setup.ps1                       # uses the default local demo password
#   .\setup.ps1 -DemoPassword "Your#Pass1"
param(
    [string]$DemoPassword = "Demo@12345"
)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
$project = "src/AcxiomCRM"

Write-Host "1/4 Restoring packages and the dotnet-ef tool..." -ForegroundColor Cyan
dotnet tool restore
dotnet restore "$project/AcxiomCRM.csproj"

Write-Host "2/4 Storing the local demo password in user-secrets (outside source control)..." -ForegroundColor Cyan
dotnet user-secrets set "Seed:DemoPassword" $DemoPassword --project $project | Out-Null

Write-Host "3/4 Creating the EF Core migration (first run only)..." -ForegroundColor Cyan
if (-not (Test-Path "$project/Migrations")) {
    # A database created before migrations existed would conflict with the first migration.
    Get-ChildItem "$project/App_Data" -Filter "acxiomcrm.db*" -ErrorAction SilentlyContinue | Remove-Item -Force
    dotnet ef migrations add InitialCreate --project $project --output-dir Migrations
}

Write-Host "4/4 Applying migrations to App_Data/acxiomcrm.db..." -ForegroundColor Cyan
dotnet ef database update --project $project

Write-Host ""
Write-Host "Setup complete. Start the app with:  dotnet run --project $project" -ForegroundColor Green
Write-Host "Then open http://localhost:5173 and sign in as admin@acxiomcrm.local with your demo password."
