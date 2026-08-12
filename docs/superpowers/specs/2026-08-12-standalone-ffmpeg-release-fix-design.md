# Standalone FFmpeg Release Fix Design

## Cel

Naprawić proces budowania QuickConvert tak, aby instalator zawsze zawierał prawdziwe, samodzielne pliki `ffmpeg.exe` i `ffprobe.exe`, a nigdy shimy Chocolatey. Opublikować poprawkę jako `v0.2.1` bez zmiany zachowania konwertera i bez modyfikowania plików użytkownika.

## Potwierdzona przyczyna

Wydanie `v0.2.0` skopiowało wyniki `Get-Command ffmpeg.exe` i `Get-Command ffprobe.exe`. Na runnerze GitHub Actions były to pliki ShimGen Chocolatey po 392704 bajty, których docelowe binaria znajdowały się poza katalogiem instalatora.

Po instalacji shim próbował uruchomić nieistniejącą ścieżkę:

```text
..\lib\ffmpeg-full\tools\ffmpeg\bin\ffmpeg.exe
```

W rezultacie każda konwersja zwracała `tool_failed`. Wcześniejsze konwersje wykonane z prawdziwym FFmpeg działały, co wyklucza uszkodzenie plików wejściowych jako wspólną przyczynę.

## Architektura poprawki

Logika wyboru narzędzi zostanie wydzielona z `tools/prepare-tools.ps1` do małego modułu PowerShell `tools/ffmpeg-tools.ps1`. Moduł udostępni funkcje do wykrywania shimów, wyboru kompletnej pary FFmpeg/FFprobe i walidacji skopiowanych plików.

`prepare-tools.ps1` wykona następujący przepływ:

1. Zbiera kandydatów z `Get-Command -All`.
2. Jeśli Chocolatey jest dostępne, przeszukuje tylko `ChocolateyInstall\lib\ffmpeg*\tools` w poszukiwaniu prawdziwych binariów.
3. Jeśli WinGet jest dostępny w profilu użytkownika, uwzględnia rzeczywiste pliki zwrócone przez `Get-Command -All`.
4. Odrzuca pliki, których metadane `ProductName` zawierają `ShimGen generated shim`.
5. Akceptuje wyłącznie `ffmpeg.exe` i `ffprobe.exe` pochodzące z tego samego katalogu.
6. Uruchamia oba pliki ze znacznikiem `-version` przed kopiowaniem.
7. Kopiuje je do `artifacts\publish\tools`.
8. Uruchamia skopiowane pliki z `-version`. Każdy kod wyjścia różny od zera przerywa build.

Resolver nie będzie polegał wyłącznie na rozmiarze pliku. Rozmiar może służyć jako informacja diagnostyczna, lecz podstawową ochroną są metadane ShimGen, wspólny katalog pary i rzeczywisty smoke test po skopiowaniu.

## Interfejs modułu

`tools/ffmpeg-tools.ps1` udostępni:

```powershell
Test-QuickConvertShim -Path <string> -> bool
Select-QuickConvertFfmpegPair -Candidates <object[]> -> object
Find-QuickConvertFfmpegPair -AdditionalRoots <string[]> -> object
Test-QuickConvertExecutable -Path <string> -ExpectedName <string> [-ProcessRunner <scriptblock>] -> void
Copy-QuickConvertFfmpegTools -OutputDirectory <string> -AdditionalRoots <string[]> [-ProcessRunner <scriptblock>] -> void
```

Każdy rekord kandydata zawiera `Path`, `ToolName` i `ProductName`. Pozwala to oddzielić pobieranie metadanych PE od czystej logiki wyboru pary. Obiekt pary zawiera pełne ścieżki `FfmpegPath` i `FfprobePath`. Funkcje zgłaszają zakończone błędem wyjątki z nazwą brakującego lub wadliwego narzędzia; nie wykonują cichego fallbacku do shima.

## Test regresji

Powstanie `tools/tests/ffmpeg-tools.tests.ps1`, uruchamiany bez Pester, aby nie dodawać zależności. Test nie będzie tworzył sztucznych plików PE ani zależał od FFmpeg zainstalowanego na komputerze. Zamiast tego poda do czystej funkcji selekcji syntetyczne rekordy kandydatów z kontrolowanymi ścieżkami i `ProductName`, a do walidatora procesu wstrzyknie kontrolowany `ProcessRunner`.

Domyślny `ProcessRunner` używany w produkcji zawsze uruchamia prawdziwy plik z `-version`, bez powłoki. Parametr testowy zmienia wyłącznie sposób wykonania procesu; nie omija selekcji, kopiowania ani ponownej walidacji. Test musi udowodnić, że:

- shim nigdy nie zostaje wybrany;
- niekompletna para zostaje odrzucona;
- wybierane są dwa pliki z jednego katalogu;
- niedziałający plik przerywa przygotowanie;
- narzędzia są ponownie sprawdzane w katalogu docelowym;
- ścieżki przekazane do walidatora po kopiowaniu wskazują na katalog wyjściowy.

Workflow GitHub Actions uruchomi test PowerShell przed pełnym buildem. Lokalny `build-release.ps1` nadal wywołuje `prepare-tools.ps1`, więc korzysta z tej samej ochrony.

## Wersja i publikacja

- Wersja produktu: `0.2.1`.
- Tag: `v0.2.1`.
- Release: `QuickConvert v0.2.1`.
- Instalator: `QuickConvert-0.2.1-win-x64-setup.exe`.
- `v0.2.0` pozostaje dostępne jako historyczne wydanie, ale notatki `v0.2.1` wyraźnie opiszą naprawę niedziałającego FFmpeg.

Tag powstanie dopiero po pełnych testach, lokalnym buildzie instalatora oraz sprawdzeniu, że pliki z `artifacts\publish\tools` nie są ShimGen i oba zwracają kod zero dla `-version`.

## Obsługa błędów

- Brak kompletnej działającej pary przerywa build z jasnym komunikatem.
- Wykrycie shima nigdy nie powoduje skopiowania go jako fallback.
- Błąd smoke testu przed lub po kopiowaniu przerywa build.
- Brak prawdziwego FFmpeg na maszynie lokalnej nie modyfikuje istniejących artefaktów; skrypt budujący i tak tworzy katalog `artifacts` od nowa.
- Niepowodzenie GitHub Actions nie będzie obchodzone ręcznym uploadem instalatora.

## Kryteria odbioru

- Test regresji PowerShell przechodzi i został wcześniej zaobserwowany jako czerwony wobec starego zachowania.
- Wszystkie testy .NET, w tym prawdziwy FFmpeg, przechodzą.
- Testy Chrome/Firefox przechodzą.
- Lokalny build tworzy instalator `0.2.1`.
- `artifacts\publish\tools\ffmpeg.exe` i `ffprobe.exe` nie mają `ProductName` zawierającego `ShimGen`.
- Oba opublikowane narzędzia wykonują `-version` z kodem zero już z katalogu `artifacts`.
- GitHub Actions kończy się sukcesem.
- Publiczne wydanie zawiera instalator `QuickConvert-0.2.1-win-x64-setup.exe` i `SHA256SUMS.txt`.
- Pobrany instalator zawiera prawdziwe FFmpeg; po instalacji `tools\ffmpeg.exe -version` i `tools\ffprobe.exe -version` kończą się kodem zero.

## Poza zakresem

- Zmiany kodeków, presetów lub interfejsu konwersji.
- Automatyczne czyszczenie historii nieudanych zadań użytkownika.
- Authenticode i podpisanie XPI Firefoksa.
- Usuwanie albo podmienianie assetów `v0.2.0`.
