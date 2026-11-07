param(
    [Parameter(Mandatory = $true)][string]$PythonPath,
    [Parameter(Mandatory = $true)][string]$ModelPath,
    [int]$Port = 8091,
    [string]$ArenaReferencePath
)
$ErrorActionPreference = 'Stop'
$rostWorkspace = Split-Path -Parent $PSScriptRoot
$rostRuntime = Join-Path $rostWorkspace '.tools\rost-speech'
$null = New-Item -ItemType Directory -Path $rostRuntime -Force
$rostPython = (Resolve-Path -LiteralPath $PythonPath).Path
$rostModel = (Resolve-Path -LiteralPath $ModelPath).Path
if ($Port -lt 1024 -or $Port -gt 65535) { throw 'Choose a local port between 1024 and 65535.' }
foreach ($rostFile in @('ve.pt','s3gen.pt','t3_mtl23ls_v2.safetensors','conds.pt','grapheme_mtl_merged_expanded_v1.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $rostModel $rostFile) -PathType Leaf)) { throw "Missing local model file: $rostFile" }
}
if (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue) { throw "Port $Port is already in use." }
$env:ROST_MODEL_PATH = $rostModel
if ($ArenaReferencePath) {
    $rostReference = (Resolve-Path -LiteralPath $ArenaReferencePath).Path
    if ([IO.Path]::GetExtension($rostReference) -ne '.wav') { throw 'Arena reference must be a local WAV recording.' }
    $env:ROST_ARENA_REFERENCE = $rostReference
} else {
    Remove-Item -LiteralPath Env:\ROST_ARENA_REFERENCE -ErrorAction SilentlyContinue
}
$env:HF_HUB_OFFLINE = '1'
$env:TRANSFORMERS_OFFLINE = '1'
$env:PYTHONDONTWRITEBYTECODE = '1'
$env:NUMBA_CACHE_DIR = Join-Path $rostRuntime 'numba'
$env:PYTHONUNBUFFERED = '1'
$env:PYTHONUTF8 = '1'
$env:PYTHONPATH = $PSScriptRoot
$rostProcess = Start-Process -FilePath $rostPython -ArgumentList @('-m','uvicorn','rost_bridge:app','--host','127.0.0.1','--port',"$Port",'--no-access-log') `
    -WorkingDirectory $rostWorkspace -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput (Join-Path $rostRuntime 'stdout.log') -RedirectStandardError (Join-Path $rostRuntime 'stderr.log')
$rostServerId = $null
$rostListenerWait = [Diagnostics.Stopwatch]::StartNew()
while ($rostListenerWait.Elapsed.TotalSeconds -lt 10) {
    $rostListener = Get-NetTCPConnection -LocalAddress '127.0.0.1' -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($rostListener) {
        $rostCandidateId = $rostListener.OwningProcess
        if ($rostCandidateId -eq $rostProcess.Id) { $rostServerId = $rostCandidateId; break }
        $rostCandidate = Get-CimInstance Win32_Process -Filter "ProcessId = $rostCandidateId" -ErrorAction SilentlyContinue
        if ($rostCandidate.ParentProcessId -eq $rostProcess.Id) { $rostServerId = $rostCandidateId; break }
    }
    if ($rostProcess.HasExited) { break }
    Start-Sleep -Milliseconds 200
}
$rostProcess.Id | Set-Content -LiteralPath (Join-Path $rostRuntime 'launcher.id')
if ($rostServerId) {
    $rostServerId | Set-Content -LiteralPath (Join-Path $rostRuntime 'process.id')
    Write-Output "Røst is starting on http://127.0.0.1:$Port (server process $rostServerId)."
} else {
    $rostProcess.Id | Set-Content -LiteralPath (Join-Path $rostRuntime 'process.id')
    Write-Output "Røst launcher $($rostProcess.Id) has started; the server is not listening yet, so process.id temporarily contains the launcher PID."
}
Write-Output "Health: http://127.0.0.1:$Port/health"
