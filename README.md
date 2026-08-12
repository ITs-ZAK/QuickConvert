# QuickConvert

QuickConvert to lokalny konwerter audio, wideo i obrazów dla Windows 10/11 oraz pomocnicze rozszerzenie Chrome/Firefox do przekazywania pojedynczych materiałów YouTube do aplikacji.

Program nie wysyła plików do chmury, nie zawiera telemetrii i nie obchodzi DRM. Downloader jest przeznaczony wyłącznie do treści własnych, public-domain albo takich, na których pobranie użytkownik ma zgodę.

## Funkcje v0.1

- prawy klik na pliku → **Więcej opcji** → **Konwertuj…**;
- wspólne formaty wyjściowe dla wielu zaznaczonych plików;
- audio: MP3, M4A, Opus, FLAC i WAV;
- wideo: MP4, MKV, WebM oraz ekstrakcja audio;
- obrazy: JPG, PNG, WebP i GIF;
- pojedyncze filmy i Shorts z YouTube do MP3 albo MP4;
- kolejka, anulowanie, historia 50 zadań i bezkolizyjne nazwy wyników.

## Uruchomienie ze źródeł

Wymagania: Windows 10/11 x64, .NET SDK 8+, Node.js 20+, FFmpeg/FFprobe w PATH.

    dotnet restore QuickConvert.slnx
    dotnet build QuickConvert.slnx --no-restore
    dotnet run --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj
    npm run test:extensions
    npm run check:extensions
    .\tools\build-extensions.ps1

Build aplikacji znajduje się w src\QuickConvert.App\bin. Podczas pracy ze źródeł ffmpeg.exe i yt-dlp.exe mogą być dostępne w PATH; instalator umieszcza je w katalogu tools.

## Rozszerzenia

Chrome:

1. Uruchom .\tools\build-extensions.ps1.
2. Otwórz chrome://extensions.
3. Włącz tryb deweloperski i wybierz **Załaduj rozpakowane**.
4. Wskaż dist\extensions\chrome.

Firefox wymaga podpisanego XPI dla trwałej instalacji w wydaniu stabilnym. Workflow release podpisuje wariant unlisted przez AMO przy ustawionych sekretach WEB_EXT_API_KEY i WEB_EXT_API_SECRET. Niepodpisane XPI służy tylko do testów deweloperskich.

## Wydanie

Skrypt .\tools\build-release.ps1 publikuje samodzielne binaria win-x64, kopiuje FFmpeg z PATH, pobiera oficjalne yt-dlp.exe, weryfikuje jego SHA-256, buduje rozszerzenia i uruchamia Inno Setup 6. Opcja -SkipInstaller pomija wyłącznie ostatni etap.

Pierwsze wydania nie są podpisane certyfikatem Code Signing, dlatego Windows SmartScreen może wyświetlić ostrzeżenie.

## Prywatność

Historia i stan aktualizacji są lokalne w %LocalAppData%\QuickConvert. Rozszerzenie korzysta tylko z activeTab, storage i nativeMessaging. Nie ma serwera QuickConvert, kont użytkowników, reklam ani analityki.

## Licencja

Kod QuickConvert jest dostępny na warunkach GPL-3.0-or-later. Zależności zachowują własne licencje; szczegóły zawiera THIRD_PARTY_NOTICES.md.
