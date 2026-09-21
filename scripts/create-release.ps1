# Build CartTracking and package Thunderstore + GitHub release artifacts.
#
# Usage:
#   .\scripts\create-release.ps1
#   .\scripts\create-release.ps1 -GitHubRelease

param(
  [switch]$GitHubRelease
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path $PSScriptRoot -Parent
$Project = Join-Path $RepoRoot "CartTracking\CartTracking.csproj"
$PluginCs = Join-Path $RepoRoot "CartTracking\CartTracking.cs"
$OutDll = Join-Path $RepoRoot "CartTracking\bin\Release\net4.8\CartTracking.dll"
$Icon = Join-Path $RepoRoot "resources\icon.png"
$PackageReadme = Join-Path $RepoRoot "publish\README.md"
$Changelog = Join-Path $RepoRoot "CHANGELOG.md"

if (-not (Test-Path $Icon)) { throw "Missing Thunderstore icon: $Icon (256x256 PNG)" }
if (-not (Test-Path $PackageReadme)) { throw "Missing package README: $PackageReadme" }
if (-not (Test-Path $Changelog)) { throw "Missing changelog: $Changelog" }

$versionLine = Select-String -Path $PluginCs -Pattern 'public const string VERSION = "([^"]+)"' | Select-Object -First 1
if (-not $versionLine) { throw "Could not parse VERSION from CartTracking.cs" }
$Version = $versionLine.Matches[0].Groups[1].Value

$ReleaseDir = Join-Path $RepoRoot "release\$Version"
$TempDir = Join-Path $RepoRoot "release\temp"

Write-Host "Building CartTracking $Version ..."
dotnet build $Project -c Release
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
if (-not (Test-Path $OutDll)) { throw "Missing build output: $OutDll" }

Remove-Item -Recurse -Force $ReleaseDir, $TempDir -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $ReleaseDir | Out-Null

Copy-Item $OutDll (Join-Path $ReleaseDir "CartTracking.dll") -Force

$Ts = Join-Path $TempDir "Thunderstore"
$TsPlugins = Join-Path $Ts "BepInEx\plugins"
New-Item -ItemType Directory -Force -Path $TsPlugins | Out-Null
Copy-Item $Icon (Join-Path $Ts "icon.png") -Force
Copy-Item $PackageReadme (Join-Path $Ts "README.md") -Force
Copy-Item $Changelog (Join-Path $Ts "CHANGELOG.md") -Force
Copy-Item $OutDll (Join-Path $TsPlugins "CartTracking.dll") -Force

$manifest = @{
  name            = "CartTracking"
  version_number  = $Version
  website_url     = "https://github.com/MattHB1/valheim-cart-tracking"
  description     = "See all cart positions on the map. Single player or multiplayer."
  dependencies    = @("denikson-BepInExPack_Valheim-5.4.2350")
} | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText((Join-Path $Ts "manifest.json"), $manifest + "`n")

$TsZip = Join-Path $ReleaseDir "Thunderstore.zip"
if (Test-Path $TsZip) { Remove-Item $TsZip -Force }
Compress-Archive -Path (Join-Path $Ts "*") -DestinationPath $TsZip -Force

Remove-Item -Recurse -Force $TempDir -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Release artifacts in $ReleaseDir"
Get-ChildItem $ReleaseDir | Format-Table Name, Length
Write-Host "Upload Thunderstore.zip at https://thunderstore.io/c/valheim/create/package/"
Write-Host "(or drag the zip into the Thunderstore / r2modman upload UI)"

if ($GitHubRelease) {
  $tag = "v$Version"
  $dllAsset = Join-Path $ReleaseDir "CartTracking.dll"
  $notes = @"
CartTracking $Version - cart map pins.

See every cart on the map. For multiplayer, install on the dedicated server AND every client (same version).

See the Thunderstore package README for full instructions.
"@
  $ErrorActionPreference = "Continue"
  gh release view $tag -R MattHB1/valheim-cart-tracking 2>&1 | Out-Null
  $exists = ($LASTEXITCODE -eq 0)
  $ErrorActionPreference = "Stop"
  if ($exists) {
    Write-Host "GitHub release $tag already exists; uploading assets..."
    gh release upload $tag $dllAsset $TsZip -R MattHB1/valheim-cart-tracking --clobber
  } else {
    gh release create $tag $dllAsset $TsZip -R MattHB1/valheim-cart-tracking --title "CartTracking $Version" --notes $notes
  }
  Write-Host "GitHub release: https://github.com/MattHB1/valheim-cart-tracking/releases/tag/$tag"
}
