# Publishes self-contained single-file builds for win-x64 and linux-x64.
# Output: artifacts/<rid>/aula-cli, aula-app
#
# Usage: pwsh packaging/publish.ps1 [-NoLinux]
param(
    [switch]$NoLinux
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "artifacts"

function Publish-Project {
    param(
        [string]$Project,
        [string]$Rid
    )

    $dir = Join-Path $out $Rid
    Write-Host "Publishing $Project ($Rid) -> $dir" -ForegroundColor Cyan
    & dotnet publish (Join-Path $root "src/$Project") `
        -c Release `
        -r $Rid `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:PublishTrimmed=false `
        -o $dir

    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed for $Project ($Rid)"
    }
}

Publish-Project "Aula.Cli" "win-x64"
Publish-Project "Aula.App" "win-x64"

if (-not $NoLinux) {
    Publish-Project "Aula.Cli" "linux-x64"
    Publish-Project "Aula.App" "linux-x64"
}

Write-Host "Done. Artifacts in $out" -ForegroundColor Green
