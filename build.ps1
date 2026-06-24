param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

dotnet publish .\src\LocationSharer\LocationSharer.csproj -c $Configuration -r win-x64 `
  /p:PublishSingleFile=true `
  /p:SelfContained=true

Write-Host "Publish output:"
Write-Host "src\LocationSharer\bin\$Configuration\net8.0-windows10.0.22621.0\win-x64\publish\"
