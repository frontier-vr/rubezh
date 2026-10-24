# YG-13: голос инструктора синтезом речи Windows (голос Microsoft Irina, ru-RU).
# Читает Assets/_Rubezh/Data/Hints/script.ru.json и пишет WAV в Assets/_Rubezh/Audio/Voice/:
#   команды — по ID озвучки (cmd_*), подсказки — по ID (hint_*, только действие после «кнопка:»),
#   причины стопа — fail_<Violation> («заголовок. пояснение»).
# Запуск: powershell -ExecutionPolicy Bypass -File tools/audio/voice_tts.ps1
# Это временная озвучка: запись живым голосом заменит файлы с теми же именами (YG-14).
param([string]$Root = (Resolve-Path "$PSScriptRoot\..\.."))

Add-Type -AssemblyName System.Speech
$json = Get-Content -Raw -Encoding UTF8 (Join-Path $Root "Assets\_Rubezh\Data\Hints\script.ru.json") | ConvertFrom-Json
$out = Join-Path $Root "Assets\_Rubezh\Audio\Voice"
New-Item -ItemType Directory -Force $out | Out-Null

$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
$synth.SelectVoice("Microsoft Irina Desktop")
$format = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(22050, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)

function Say([string]$id, [string]$text, [int]$rate) {
    $path = Join-Path $out ($id + ".wav")
    $synth.Rate = $rate
    $synth.SetOutputToWaveFile($path, $format)
    $synth.Speak($text)
    $synth.SetOutputToNull()
    Write-Output ("{0}`t{1}" -f $id, $text)
}

foreach ($c in $json.commands) {
    # Короткие строевые команды — чуть быстрее и твёрже
    $rate = if ($c.text.Length -le 10) { 1 } else { 0 }
    Say $c.voice $c.text $rate
}
foreach ($h in $json.hints) {
    $spoken = $h.text
    $i = $spoken.IndexOf(": ")
    if ($i -ge 0) { $spoken = $spoken.Substring($i + 2) }
    $spoken = $spoken.Substring(0, 1).ToUpper() + $spoken.Substring(1)
    Say $h.id $spoken 0
}
foreach ($f in $json.failReasons) {
    Say ("fail_" + $f.violation) ($f.title + ". " + $f.text) 0
}
$synth.Dispose()
