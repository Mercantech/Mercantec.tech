param(
    [string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent),
    [ValidateSet('join-tables', 'sort-rows', 'find-row')]
    [string[]]$Name = @('join-tables', 'sort-rows', 'find-row')
)

$ErrorActionPreference = 'Stop'
$ProjectRoot = [System.IO.Path]::GetFullPath($ProjectRoot)
$ffmpeg = (Get-Command ffmpeg -ErrorAction Stop).Source
$ffprobe = (Get-Command ffprobe -ErrorAction Stop).Source
$destination = Join-Path $ProjectRoot 'web/wwwroot/images/characters/raymond/animations'
New-Item -ItemType Directory -Force -Path $destination | Out-Null

foreach ($animationName in $Name) {
    $sheet = Join-Path $ProjectRoot "assets/characters/raymond/animations/$animationName/sheet.png"
    if (-not (Test-Path -LiteralPath $sheet)) { throw "Missing source sheet: $sheet" }
    $metadata = (& $ffprobe -v error -select_streams v:0 -show_entries stream=width,height -of json $sheet | ConvertFrom-Json).streams[0]
    if ($LASTEXITCODE -ne 0 -or $metadata.width % 3 -ne 0 -or $metadata.height % 2 -ne 0) {
        throw "The source must be an evenly divisible 3 by 2 frame grid: $sheet"
    }
    $frameWidth = [int]($metadata.width / 3)
    $frameHeight = [int]($metadata.height / 2)
    $scratch = Join-Path $ProjectRoot ('.tools/raymond-animation-' + $animationName + '-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $scratch | Out-Null

    # Extract the six native illustrated cells; do not repaint or interpolate poses.
    for ($frame = 0; $frame -lt 6; $frame++) {
        $left = ($frame % 3) * $frameWidth
        $top = [int][Math]::Floor($frame / 3) * $frameHeight
        $crop = "crop=${frameWidth}:${frameHeight}:${left}:${top}"
        $framePath = Join-Path $scratch ('pose-{0:00}.png' -f $frame)
        & $ffmpeg -hide_banner -loglevel error -y -i $sheet -vf $crop -frames:v 1 $framePath
        if ($LASTEXITCODE -ne 0) { throw "Frame extraction failed: $animationName / $frame" }
    }

    # A short hold before and after the action makes the loop easier to read.
    $timeline = @(0, 0, 1, 2, 3, 4, 4, 5, 5)
    for ($index = 0; $index -lt $timeline.Count; $index++) {
        Copy-Item -LiteralPath (Join-Path $scratch ('pose-{0:00}.png' -f $timeline[$index])) -Destination (Join-Path $scratch ('frame-{0:00}.png' -f $index))
    }
    $gif = Join-Path $destination "$animationName.gif"
    & $ffmpeg -hide_banner -loglevel error -y -framerate 4 -i (Join-Path $scratch 'frame-%02d.png') -filter_complex '[0:v]split[frames][colors];[colors]palettegen=stats_mode=full:reserve_transparent=0[palette];[frames][palette]paletteuse=dither=bayer:bayer_scale=3' -loop 0 $gif
    if ($LASTEXITCODE -ne 0) { throw "GIF encoding failed: $animationName" }
    Copy-Item -LiteralPath (Join-Path $scratch 'pose-00.png') -Destination (Join-Path $destination "$animationName.png") -Force

    [pscustomobject]@{
        Name = $animationName
        Width = $frameWidth
        Height = $frameHeight
        DurationSeconds = $timeline.Count / 4
        GifBytes = (Get-Item -LiteralPath $gif).Length
        Gif = $gif
    }
}
