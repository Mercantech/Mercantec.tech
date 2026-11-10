# Render the exact five Danish lines locally. Prefer a Danish male voice if installed.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Runtime.WindowsRuntime
[Windows.Media.SpeechSynthesis.SpeechSynthesizer, Windows.Media.SpeechSynthesis, ContentType=WindowsRuntime] > $null
[Windows.Media.SpeechSynthesis.SpeechSynthesisStream, Windows.Media.SpeechSynthesis, ContentType=WindowsRuntime] > $null
[Windows.Storage.Streams.DataReader, Windows.Storage.Streams, ContentType=WindowsRuntime] > $null
$asTask = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.IsGenericMethodDefinition -and $_.GetGenericArguments().Length -eq 1 -and $_.GetParameters().Length -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' } | Select-Object -First 1
function Await-WinRT($Operation, [Type]$ResultType) {
    $task = $asTask.MakeGenericMethod($ResultType).Invoke($null, @($Operation))
    $task.Wait()
    return $task.Result
}
$audioRoot = [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot, '..', '..'))
$raw = [IO.Path]::Combine($PSScriptRoot, 'raw')
[IO.Directory]::CreateDirectory($raw) > $null
$phrases = [IO.File]::ReadAllText([IO.Path]::Combine($audioRoot, 'audio', 'phrases.json'), [Text.Encoding]::UTF8) | ConvertFrom-Json
$synth = New-Object Windows.Media.SpeechSynthesis.SpeechSynthesizer
$voices = @([Windows.Media.SpeechSynthesis.SpeechSynthesizer]::AllVoices | Where-Object Language -eq 'da-DK')
$voice = $voices | Where-Object { $_.Gender.ToString() -eq 'Male' } | Select-Object -First 1
if ($null -eq $voice) { $voice = $voices | Select-Object -First 1 }
if ($null -eq $voice) { throw 'No installed local da-DK voice available.' }
$synth.Voice = $voice
$rate = 1.0
$pitch = 1.03
$synth.Options.SpeakingRate = $rate
$synth.Options.AudioPitch = $pitch
$synth.Options.AudioVolume = 1.0
Write-Output ('Voice: ' + $voice.DisplayName + '; gender=' + $voice.Gender + '; rate=' + $rate + '; pitch=' + $pitch)
foreach ($phrase in $phrases) {
    $stream = Await-WinRT ($synth.SynthesizeTextToStreamAsync($phrase.text)) ([Windows.Media.SpeechSynthesis.SpeechSynthesisStream])
    $reader = New-Object Windows.Storage.Streams.DataReader($stream)
    $count = Await-WinRT ($reader.LoadAsync([uint32]$stream.Size)) ([uint32])
    $bytes = New-Object byte[] ([int]$stream.Size)
    $reader.ReadBytes($bytes)
    [IO.File]::WriteAllBytes([IO.Path]::Combine($raw, ($phrase.id + '.wav')), $bytes)
    $reader.Dispose()
    $stream.Dispose()
    Write-Output ('Rendered: ' + $phrase.id)
}
$installed = @($voices | ForEach-Object { @{ name = $_.DisplayName; language = $_.Language; gender = $_.Gender.ToString() } })
$voiceInfo = @{ voice = $voice.DisplayName; language = $voice.Language; gender = $voice.Gender.ToString(); installedDanishVoices = $installed; speakingRate = $rate; audioPitch = $pitch; robotProcessing = $false; note = 'Preferred an installed Danish male voice; otherwise the available Danish voice is openly documented with slightly lively LUPP character pitch. No voice cloning.' }
[IO.File]::WriteAllText([IO.Path]::Combine($raw, 'voice.json'), ($voiceInfo | ConvertTo-Json -Depth 5), [Text.Encoding]::UTF8)
$synth.Dispose()
