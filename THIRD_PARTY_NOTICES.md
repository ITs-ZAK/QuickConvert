# Informacje o komponentach zewnętrznych

QuickConvert uruchamia poniższe narzędzia jako osobne procesy i nie modyfikuje ich kodu.

## FFmpeg / FFprobe

- Projekt: https://ffmpeg.org/
- Źródła: https://ffmpeg.org/download.html
- Build Windows używany w wydaniach: https://www.gyan.dev/ffmpeg/builds/
- Licencja konkretnego buildu zależy od konfiguracji; pełne buildy zawierające libx264 są rozpowszechniane na warunkach GPL.

Informację ffmpeg -version należy dołączać do opisu każdego wydania.

## yt-dlp

- Projekt i źródła: https://github.com/yt-dlp/yt-dlp
- Licencja: Unlicense

Skrypt pobiera samodzielne yt-dlp.exe z oficjalnego GitHub Release i porównuje je z opublikowanym SHA2-256SUMS.

## .NET

- Projekt: https://github.com/dotnet/runtime
- Licencja: MIT

Wydanie win-x64 jest self-contained i zawiera wymagane składniki środowiska .NET.
