[CmdletBinding()]
param(
    [uri] $ServerUrl = 'https://lup.dk/api/mcp',
    [ValidateRange(1024, 65535)] [int] $CallbackPort = 58217,
    [ValidatePattern('^[a-zA-Z0-9_-]{2,64}$')] [string] $ClientId = 'lup-codex',
    [string] $ConfigPath,
    [switch] $Configure
)

$ErrorActionPreference = 'Stop'
if (-not $ServerUrl.IsAbsoluteUri -or $ServerUrl.Scheme -ne 'https') {
    throw 'Brug MCP-serverens offentlige HTTPS-adresse.'
}
if (-not (Get-Command codex -ErrorAction SilentlyContinue)) {
    throw 'Codex CLI skal være installeret, før forbindelsen sættes op.'
}
if (-not $ConfigPath) {
    $lupCodexRoot = [Environment]::GetEnvironmentVariable('CODEX_HOME')
    if (-not $lupCodexRoot) { $lupCodexRoot = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.codex' }
    $ConfigPath = Join-Path $lupCodexRoot 'config.toml'
}
$lupConfigBlock = @"
[mcp_servers.lup]
url = "$($ServerUrl.AbsoluteUri)"
scopes = ["openid", "profile", "offline_access"]
tool_timeout_sec = 120

[mcp_servers.lup.oauth]
client_id = "$ClientId"
callback_port = $CallbackPort

[mcp_servers.lup.tools.read_page]
output_token_limit = 100000
"@

if (-not $Configure) {
    Write-Output $lupConfigBlock
    Write-Output "`nForhåndsvisning. Brug -Configure, når MCP-endpointet er udrullet."
    return
}

# Refuse to install a connection to a missing endpoint or an anonymous service.
$lupChallenge = $null
try {
    # Stateless Streamable HTTP may reject GET with 405 before auth. Probe initialize instead.
    $lupInitialize = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"lup-mcp-setup","version":"1.0.0"}}}'
    $null = Invoke-WebRequest -Uri $ServerUrl -Method Post -ContentType 'application/json' -Body $lupInitialize -Headers @{ Accept = 'application/json, text/event-stream' }
}
catch {
    $lupResponse = $_.Exception.Response
    if ($lupResponse -and [int] $lupResponse.StatusCode -eq 401) {
        $lupChallenge = [string] $lupResponse.Headers.WwwAuthenticate
        if (-not $lupChallenge) { $lupChallenge = [string] $lupResponse.Headers['WWW-Authenticate'] }
    }
    else { throw 'MCP-endpointet er ikke tilgængeligt. Udrul og verificér serveren først.' }
}
if ($lupChallenge -notmatch 'resource_metadata=') {
    throw 'Serveren annoncerer ikke beskyttet MCP-adgang. Konfigurationen blev ikke ændret.'
}

$lupConfigDirectory = Split-Path -Parent ([IO.Path]::GetFullPath($ConfigPath))
$null = [IO.Directory]::CreateDirectory($lupConfigDirectory)
$lupFile = [IO.File]::Open($ConfigPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try {
    $lupReader = [IO.StreamReader]::new($lupFile, [Text.Encoding]::UTF8, $true, 4096, $true)
    try { $lupCurrent = $lupReader.ReadToEnd() } finally { $lupReader.Dispose() }
    if ($lupCurrent -match '(?m)^\s*\[mcp_servers\.lup(?:\.|\])') {
        throw 'Der findes allerede en lup-forbindelse. Ingen eksisterende indstillinger blev overskrevet.'
    }
    $null = $lupFile.Seek(0, [IO.SeekOrigin]::End)
    $lupWriter = [IO.StreamWriter]::new($lupFile, [Text.UTF8Encoding]::new($false), 4096, $true)
    try { $lupWriter.Write("`n`n$lupConfigBlock`n"); $lupWriter.Flush(); $lupFile.Flush($true) }
    finally { $lupWriter.Dispose() }
}
finally { $lupFile.Dispose() }

Write-Output "Tilføjede kun MCP-serveren lup i $ConfigPath."
Write-Output 'Næste trin: codex mcp login lup'
Write-Output 'Registrér den præcise callback-adresse fra Codex på den offentlige OAuth-klient før første login.'
