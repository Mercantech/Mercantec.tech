param(
    [string]$ProjectRoot = (Join-Path $PSScriptRoot '../../../..')
)

$ErrorActionPreference = 'Stop'
$ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
$ffmpeg = (Get-Command ffmpeg -ErrorAction Stop).Source
$ffprobe = (Get-Command ffprobe -ErrorAction Stop).Source
$destination = Join-Path $ProjectRoot 'web/wwwroot/images/characters/bat/animations'
$scratch = Join-Path ([System.IO.Path]::GetTempPath()) ('naesebladet-animation-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $destination, $scratch -Force | Out-Null

try {
    foreach ($name in 'pulsen-og-ekkoet', 'naar-ekkoet-udebliver', 'maalingens-vej-gennem-mqtt') {
        $sheet = Join-Path $PSScriptRoot "$name-sheet.png"
        $metadata = (& $ffprobe -v error -select_streams v:0 -show_entries stream=width,height -of json $sheet | ConvertFrom-Json).streams[0]
        if ($LASTEXITCODE -ne 0 -or $metadata.width % 2 -ne 0 -or $metadata.height % 2 -ne 0) {
            throw "Billedarket skal have fire lige store felter: $sheet"
        }

        $cellWidth = [int]($metadata.width / 2)
        $cellHeight = [int]($metadata.height / 2)
        $inset = 9
        $cropWidth = $cellWidth - (2 * $inset)
        $cropHeight = $cellHeight - (2 * $inset)

        for ($frame = 0; $frame -lt 4; $frame++) {
            $left = (($frame % 2) * $cellWidth) + $inset
            $top = ([int][Math]::Floor($frame / 2) * $cellHeight) + $inset
            $filter = "crop=${cropWidth}:${cropHeight}:${left}:${top}:exact=1,scale=512:512:force_original_aspect_ratio=decrease:flags=lanczos,pad=512:512:(ow-iw)/2:(oh-ih)/2:color=0x43313A,setsar=1"
            $pose = Join-Path $scratch ('pose-{0}.png' -f $frame)
            & $ffmpeg -hide_banner -loglevel error -y -i $sheet -vf $filter -frames:v 1 $pose
            if ($LASTEXITCODE -ne 0) { throw "Kunne ikke udtrække billede $frame fra $sheet" }
        }

        # Lad hver tegnet tilstand stå længe nok til, at den kan aflæses.
        $timeline = @(0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 3)
        for ($frame = 0; $frame -lt $timeline.Count; $frame++) {
            $pose = Join-Path $scratch ('pose-{0}.png' -f $timeline[$frame])
            $target = Join-Path $scratch ('frame-{0:00}.png' -f $frame)
            Copy-Item -LiteralPath $pose -Destination $target -Force
        }

        $gif = Join-Path $destination "$name.gif"
        & $ffmpeg -hide_banner -loglevel error -y -filter_complex_threads 1 -framerate 4 -i (Join-Path $scratch 'frame-%02d.png') -filter_complex '[0:v]split[frames][colors];[colors]palettegen=stats_mode=full:reserve_transparent=0[palette];[frames][palette]paletteuse=dither=sierra2_4a' -loop 0 $gif
        if ($LASTEXITCODE -ne 0) { throw "Kunne ikke gemme GIF: $name" }

        $poster = Join-Path $destination "$name.webp"
        & $ffmpeg -hide_banner -loglevel error -y -i (Join-Path $scratch 'pose-0.png') -c:v libwebp -quality 87 -frames:v 1 $poster
        if ($LASTEXITCODE -ne 0) { throw "Kunne ikke gemme stillbillede: $name" }

        Write-Output $gif
    }
}
finally {
    Get-ChildItem -LiteralPath $scratch -File | Remove-Item
    Remove-Item -LiteralPath $scratch
}
