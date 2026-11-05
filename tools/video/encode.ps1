# Склейка кадров сквозного прогона в MP4 (NG-20), docs/release.md.
# 1. В Unity: Rubezh → Видео прогона → Записывать кадры в Temp/video.
# 2. Test Runner → PlayMode → FullRunTests: кадры 1280×720, 30 в секунду игрового времени.
# 3. Rubezh → Видео прогона → Не записывать.
# 4. .\tools\video\encode.ps1 -Frames Temp\video -Out Builds\rubezh-gameplay.mp4
param(
    [Parameter(Mandatory = $true)][string]$Frames,
    [string]$Out = "Builds/rubezh-gameplay.mp4",
    [string]$Ffmpeg = "ffmpeg"
)
& $Ffmpeg -y -loglevel error -framerate 30 -i (Join-Path $Frames "frame_%05d.png") `
    -c:v libx264 -pix_fmt yuv420p -crf 20 -preset slow -movflags +faststart $Out
Write-Host "готово: $Out"
