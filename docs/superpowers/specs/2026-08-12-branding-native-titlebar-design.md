# QuickConvert — branding i natywny ciemny pasek tytułu

## Cel

Nadać aplikacji spójny znak wizualny oraz usunąć biały pasek tytułu widoczny nad ciemnym interfejsem, bez zastępowania natywnych zachowań okna Windows. Ten sam znak ma identyfikować aplikację, instalator, deinstalator, skróty i integrację menu kontekstowego.

## Wybrany znak

Wybrany wariant to „C — Q w ruchu”. Symbol łączy literę Q z okrężnym ruchem sugerującym zmianę formatu. Znak składa się z:

- zaokrąglonego kwadratu;
- gradientu od `#9B8CFF` do `#5145D6`;
- białego symbolu Q z grotem strzałki;
- uproszczonej geometrii zachowującej czytelność przy 16 px.

Źródłem referencyjnym będzie wektorowy plik SVG. Repozytorium będzie zawierało również gotowe zasoby binarne używane podczas kompilacji, bez wymagania dodatkowego programu graficznego:

- wielorozmiarowe ICO zawierające co najmniej 16, 20, 24, 32, 40, 48, 64, 128 i 256 px;
- PNG 256 × 256 do dokumentacji i miejsc niewspierających ICO;
- małą grafikę nagłówka kreatora instalacji.

## Pasek tytułu

Okno zachowuje natywną ramkę WPF i standardowe kontrolki Windows. Po utworzeniu uchwytu okna aplikacja poprosi Desktop Window Manager o wariant ciemny przez `DwmSetWindowAttribute`.

Implementacja:

- najpierw używa aktualnego identyfikatora `DWMWA_USE_IMMERSIVE_DARK_MODE = 20`;
- na zgodnych starszych kompilacjach Windows podejmuje próbę z identyfikatorem 19;
- nie przerywa uruchamiania, jeśli oba wywołania zostaną odrzucone;
- ponawia zastosowanie po zmianie uchwytu lub motywu tylko wtedy, gdy WPF tego wymaga;
- nie zastępuje natywnego hit-testingu, przeciągania, skalowania ani przycisków okna.

Dzięki temu pozostają dostępne Snap Layouts, menu systemowe, skróty klawiaturowe, minimalizacja, maksymalizacja oraz standardowy czerwony stan przycisku zamknięcia.

## Umieszczenie logo

Wielorozmiarowe ICO zostanie ustawione jako ikona aplikacji w projekcie WPF. Windows użyje go automatycznie:

- w lewym górnym rogu natywnego paska tytułu;
- dla pliku `QuickConvert.exe`;
- na pasku zadań;
- w skrótach menu Start;
- w oknie przełączania aplikacji.

Obecny większy kafelek Q w nagłówku głównego widoku zostanie zastąpiony tą samą geometrią „Q w ruchu”, narysowaną jako wektor WPF. Nie będzie używał rozciągniętej bitmapy.

## Instalator i integracja Windows

Inno Setup otrzyma `SetupIconFile` wskazujący ikonę QuickConvert. Ta sama identyfikacja obejmie:

- ikonę pliku instalatora;
- ikonę deinstalatora i wpisu „Zainstalowane aplikacje”;
- skrót QuickConvert;
- klasyczne polecenie menu kontekstowego;
- mały znak w nagłówku kreatora instalacji.

Istniejące zachowanie instalacji per-user i rejestracji Native Messaging pozostaje bez zmian.

## Struktura plików

Planowane zasoby:

- `assets/branding/quickconvert-logo.svg` — źródło wektorowe;
- `assets/branding/quickconvert.ico` — ikona wielorozmiarowa aplikacji i instalatora;
- `assets/branding/quickconvert-256.png` — podgląd i dokumentacja;
- `assets/branding/quickconvert-wizard-small.png` — nagłówek kreatora.

Kod obsługi ciemnego paska zostanie wydzielony do małej klasy odpowiedzialnej wyłącznie za integrację DWM. `MainWindow` wywoła ją po zdarzeniu `SourceInitialized`.

## Obsługa błędów

Brak biblioteki DWM, niewspierany atrybut albo błąd wywołania natywnego nie może zamknąć aplikacji. W takim przypadku Windows wyświetli standardowy pasek tytułu, a pozostałe funkcje będą działały bez zmian.

Uszkodzony lub brakujący zasób ikony jest błędem kompilacji albo instalatora, nie błędem obsługiwanym w czasie działania.

## Weryfikacja

Automatyczne testy sprawdzą:

- obecność wszystkich wymaganych zasobów;
- poprawny nagłówek ICO i zestaw wymaganych rozmiarów;
- konfigurację `ApplicationIcon` w projekcie WPF;
- konfigurację `SetupIconFile` i grafiki kreatora w Inno Setup;
- podłączenie aplikacji ciemnego paska do momentu utworzenia uchwytu;
- zachowanie awaryjne bez wyjątku dla nieobsługiwanego atrybutu DWM.

Weryfikacja ręczna obejmie Windows 10 i 11, skalowanie 100% i 150%, pasek tytułu w stanie zwykłym oraz zmaksymalizowanym, Snap Layouts, pasek zadań, Eksplorator, menu Start i kreator instalacji.

## Poza zakresem

- całkowicie własna ramka okna i własne przyciski minimalizacji/maksymalizacji;
- osobny zestaw ikon dla jasnego motywu;
- animowane logo;
- zmiana nazwy produktu;
- podpis Code Signing;
- przeprojektowanie pozostałych ekranów aplikacji.

