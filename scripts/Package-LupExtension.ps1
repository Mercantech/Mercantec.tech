param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$lupRepository = Split-Path -Parent $PSScriptRoot
$lupExtension = Join-Path $lupRepository 'extensions/lup'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $lupRepository '.tools/lup-extension' }
$lupManifest = Get-Content -LiteralPath (Join-Path $lupExtension 'manifest.json') -Raw | ConvertFrom-Json
$lupPackageFiles = @('manifest.json', 'worker.mjs', 'core.mjs', 'content.js', 'prepare.html', 'prepare.mjs', 'popup.html', 'style.css')
$lupPackagePaths = foreach ($lupPackageFile in $lupPackageFiles) {
    $lupPackagePath = Join-Path $lupExtension $lupPackageFile
    if (-not (Test-Path -LiteralPath $lupPackagePath -PathType Leaf)) { throw "Missing extension file: $lupPackageFile" }
    $lupPackagePath
}
$null = New-Item -ItemType Directory -Force -Path $OutputDirectory
$lupArchive = Join-Path $OutputDirectory "LUP-$($lupManifest.version).zip"
Compress-Archive -LiteralPath $lupPackagePaths -DestinationPath $lupArchive -Force
Write-Output (Get-Item -LiteralPath $lupArchive).FullName
