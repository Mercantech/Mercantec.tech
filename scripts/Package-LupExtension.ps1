param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$lupRepository = Split-Path -Parent $PSScriptRoot
$lupExtension = Join-Path $lupRepository 'extensions/lup'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $lupRepository '.tools/lup-extension' }
$lupManifest = Get-Content -LiteralPath (Join-Path $lupExtension 'manifest.json') -Raw | ConvertFrom-Json
$lupImageProject = Join-Path $lupRepository 'image-tools'
if (-not (Test-Path -LiteralPath (Join-Path $lupImageProject 'node_modules/onnxruntime-web/package.json'))) {
    & npm --prefix $lupImageProject ci
    if ($LASTEXITCODE -ne 0) { throw 'Image-tool dependencies could not be installed.' }
}
& npm --prefix $lupImageProject run build
if ($LASTEXITCODE -ne 0) { throw 'Image-tool assets could not be built.' }
$lupImageSource = Join-Path $lupRepository 'web/wwwroot/image-tools'
$lupImageTarget = Join-Path $lupExtension 'image-tools'
$null = New-Item -ItemType Directory -Force -Path $lupImageTarget
foreach ($lupImageFile in @('ort.wasm.min.mjs', 'ort-wasm-simd-threaded.mjs', 'ort-wasm-simd-threaded.wasm', 'u2netp.onnx', 'background-core.mjs', 'assets.json', 'U2NET-LICENSE.txt', 'REMBG-LICENSE.txt', 'ONNXRUNTIME-LICENSE.txt', 'ONNXRUNTIME-THIRD-PARTY-NOTICES.txt', 'THIRD-PARTY-NOTICES.txt')) {
    Copy-Item -LiteralPath (Join-Path $lupImageSource $lupImageFile) -Destination (Join-Path $lupImageTarget $lupImageFile) -Force
}
foreach ($lupRuntimeFile in @('emoji-background.js', 'emoji-background-worker.js')) {
    Copy-Item -LiteralPath (Join-Path $lupRepository "web/wwwroot/js/$lupRuntimeFile") -Destination (Join-Path $lupExtension $lupRuntimeFile) -Force
}
$lupPackageFiles = @('manifest.json', 'worker.mjs', 'core.mjs', 'content.js', 'prepare.html', 'prepare.mjs', 'popup.html', 'popup.mjs', 'style.css', 'crop.mjs', 'crop.css', 'emoji-background.js', 'emoji-background-worker.js', 'image-search.mjs', 'search.html', 'search.mjs', 'search.css')
$lupPackagePaths = foreach ($lupPackageFile in $lupPackageFiles) {
    $lupPackagePath = Join-Path $lupExtension $lupPackageFile
    if (-not (Test-Path -LiteralPath $lupPackagePath -PathType Leaf)) { throw "Missing extension file: $lupPackageFile" }
    $lupPackagePath
}
$lupPackagePaths += $lupImageTarget
$lupPackagePaths += Join-Path $lupExtension 'assets'
$null = New-Item -ItemType Directory -Force -Path $OutputDirectory
$lupArchive = Join-Path $OutputDirectory "LUP-$($lupManifest.version).zip"
Compress-Archive -LiteralPath $lupPackagePaths -DestinationPath $lupArchive -Force
Write-Output (Get-Item -LiteralPath $lupArchive).FullName
