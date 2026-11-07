// Native clipboard guard shared by loopback-only browser QA; originals stay in RAM.
import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
export async function preserveClipboard(runId, until) {
  let guard, guardClosed;
  assert.equal(process.platform, 'win32', 'Use a platform clipboard guard before running native clipboard acceptance');
  const code = String.raw`
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$original = [System.Windows.Forms.Clipboard]::GetDataObject()
$snapshot = New-Object System.Windows.Forms.DataObject
$formats = @()
try {
  if ($null -ne $original) {
    foreach ($format in $original.GetFormats($false)) {
      $value = $original.GetData($format, $false)
      if ($null -eq $value) { continue }
      if ($value -is [System.IO.Stream]) {
        $copy = New-Object System.IO.MemoryStream
        if ($value.CanSeek) { $value.Position = 0 }
        $value.CopyTo($copy); $copy.Position = 0; $value = $copy
      } elseif ($value -is [System.Drawing.Image]) { $value = $value.Clone() }
      elseif ($value -is [System.Collections.Specialized.StringCollection]) { $copy = New-Object System.Collections.Specialized.StringCollection; foreach ($item in $value) { [void]$copy.Add($item) }; $value = $copy }
      elseif ($value -is [System.ICloneable]) { $value = $value.Clone() }
      elseif ($value -isnot [string] -and -not $value.GetType().IsValueType) { throw 'Unsupported clipboard format: preservation aborted before any write.' }
      $snapshot.SetData($format, $false, $value); $formats += $format
    }
  }
} catch { [Console]::WriteLine('GUARD_ERROR'); exit 2 }
[Console]::WriteLine('GUARD_READY'); [Console]::Out.Flush()
$sentinels = New-Object System.Collections.Generic.List[string]
$images = New-Object System.Collections.Generic.List[string]
function PixelHash($image) {
  $bitmap = [System.Drawing.Bitmap]::new($image.Width, $image.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
  try { $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy; $graphics.DrawImageUnscaled($image, 0, 0) } finally { $graphics.Dispose() }
  $bounds = [System.Drawing.Rectangle]::new(0, 0, $bitmap.Width, $bitmap.Height)
  $bits = $bitmap.LockBits($bounds, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  try { $bytes = [byte[]]::new([Math]::Abs($bits.Stride) * $bitmap.Height); [System.Runtime.InteropServices.Marshal]::Copy($bits.Scan0, $bytes, 0, $bytes.Length) }
  finally { $bitmap.UnlockBits($bits); $bitmap.Dispose() }
  $sha = [System.Security.Cryptography.SHA256]::Create()
  try { return [Convert]::ToBase64String($sha.ComputeHash($bytes)) } finally { $sha.Dispose() }
}
try { while ($null -ne ($command = [Console]::ReadLine())) {
  if ($command.StartsWith('sentinel:')) { $sentinels.Add($command.Substring(9)) }
  elseif ($command.StartsWith('png:')) {
    $stream = [System.IO.MemoryStream]::new([Convert]::FromBase64String($command.Substring(4)))
    try { $image = [System.Drawing.Image]::FromStream($stream); try { $images.Add((PixelHash $image)) } finally { $image.Dispose() } } finally { $stream.Dispose() }
  } else { break }
} }
finally {
  $ours = $false
  try {
    $current = [System.Windows.Forms.Clipboard]::GetDataObject()
    if ($null -ne $current) { foreach ($format in $current.GetFormats($false)) { $value = $current.GetData($format, $false); if ($value -is [string]) { foreach ($sentinel in $sentinels) { if ($sentinel.Length -gt 10 -and $value.Contains($sentinel)) { $ours = $true } } } } }
    if ($images.Count -gt 0 -and [System.Windows.Forms.Clipboard]::ContainsImage()) { $image = [System.Windows.Forms.Clipboard]::GetImage(); try { if ($images.Contains((PixelHash $image))) { $ours = $true } } finally { $image.Dispose() } }
  } catch { $ours = $false }
  if (-not $ours) { [Console]::WriteLine('GUARD_SKIPPED_UNCHANGED_OR_USER_COPY'); exit 0 }
  for ($attempt = 0; $attempt -lt 10; $attempt++) {
    try {
      if ($formats.Count -eq 0) { [System.Windows.Forms.Clipboard]::Clear() } else { [System.Windows.Forms.Clipboard]::SetDataObject($snapshot, $true) }
      [Console]::WriteLine('GUARD_RESTORED'); break
    } catch { if ($attempt -eq 9) { [Console]::WriteLine('GUARD_RESTORE_FAILED'); exit 3 }; Start-Sleep -Milliseconds 100 }
  }
}
`;
  guard = spawn('powershell.exe', ['-NoProfile', '-Sta', '-EncodedCommand', Buffer.from(code, 'utf16le').toString('base64')], { windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
  let log = '';
  guard.stdout.on('data', chunk => { log += chunk.toString(); });
  guardClosed = new Promise(accept => guard.once('exit', code => accept(code)));
  await until(() => { if (log.includes('GUARD_ERROR')) throw new Error('OS clipboard cannot be preserved without losing a format'); return log.includes('GUARD_READY'); }, 'clipboard preservation guard');
  guard.stdin.write('sentinel:' + runId + '\n');
  const restore = async () => {
    guard.stdin.end('restore\n'); const code = await guardClosed;
    assert.ok(code === 0 && (log.includes('GUARD_RESTORED') || log.includes('GUARD_SKIPPED')), 'OS clipboard preserved or conditionally restored');
    process.stdout.write('PASS OS clipboard ' + (log.includes('GUARD_RESTORED') ? 'conditionally restored' : 'untouched or user-copy preserved') + '; no original values logged or written to disk\n');
  };
  restore.sentinel = value => { if (typeof value === 'string' && !/[\r\n]/.test(value)) guard.stdin.write('sentinel:' + value + '\n'); };
  restore.png = base64 => { if (!/^[A-Za-z0-9+/=]+$/.test(base64)) throw new Error('Invalid PNG preservation fingerprint input'); guard.stdin.write('png:' + base64 + '\n'); };
  return restore;
}
