# QuickConvert — ustawienia konwersji i dopracowanie pustego stanu

## Cel

Zamienić statyczną sekcję „Więcej ustawień” w trzy rzeczywiste, proste ustawienia konwersji oraz usunąć dwa niespójne elementy widoku: systemowe białe kółko ekspandera i puste miejsce przed wyborem plików.

## Zakres interfejsu

Sekcja „Więcej ustawień” zawiera:

1. wybór jakości: `Oszczędna`, `Zbalansowana`, `Najwyższa`;
2. wybór miejsca zapisu: `Obok oryginału` albo `Pobrane\QuickConvert`;
3. przełącznik `Otwórz folder po zakończeniu`, domyślnie wyłączony.

Domyślne ustawienia to `Zbalansowana`, `Obok oryginału` i wyłączone automatyczne otwieranie folderu. Ustawienia są zapisywane automatycznie w `%LocalAppData%\QuickConvert\settings.json` i obowiązują dla kolejnych uruchomień.

Sekcja pozostaje zwinięta przy starcie. Systemowy znacznik ekspandera zostanie zastąpiony niewielkim fioletowym chevronem obracającym się logicznie przez zmianę kierunku znaku, bez animacji. Cały nagłówek pozostaje klikalny i dostępny z klawiatury.

## Presety jakości

Preset wpływa tylko na kodowanie stratne. Rozdzielczość, FPS i proporcje pozostają zgodne ze źródłem.

| Format | Oszczędna | Zbalansowana | Najwyższa |
|---|---:|---:|---:|
| MP4/MKV H.264 | CRF 28, audio 128 kb/s | CRF 23, audio 192 kb/s | CRF 18, audio 256 kb/s |
| WebM VP9 | CRF 38, Opus 96 kb/s | CRF 33, Opus 128 kb/s | CRF 28, Opus 192 kb/s |
| MP3/M4A | 128 kb/s | 192 kb/s | 320 kb/s |
| Opus | 96 kb/s | 128 kb/s | 192 kb/s |
| JPG | FFmpeg q:v 5 | q:v 3 | q:v 2 |
| WebP | jakość 75 | jakość 85 | jakość 95 |

FLAC, WAV, PNG i GIF nie zmieniają parametrów w zależności od presetu. Interfejs pokazuje stałą notę: „FLAC, WAV, PNG i GIF pozostają bezstratne lub używają ustawień formatu”.

Dotychczasowa wartość `ConversionPreset.Default` zostaje zastąpiona albo zmapowana na `Balanced`, aby istniejące wywołania IPC i testy zachowały bezpieczną wartość domyślną.

## Folder docelowy

`Obok oryginału` zachowuje dotychczasowe działanie.

`Pobrane\QuickConvert` zapisuje wszystkie wyniki do tego samego katalogu, używanego już przez downloader. Katalog powstaje przed rozpoczęciem zapisu. Nazwa wyniku bazuje na nazwie źródła, a kolizje nadal otrzymują ` (1)`, ` (2)` itd. Źródło nigdy nie jest nadpisywane ani usuwane.

Silnik otrzyma jawny katalog pobierania w konstruktorze lub żądaniu, dzięki czemu testy nie zapisują do prawdziwego profilu użytkownika.

## Automatyczne otwieranie folderu

Po pomyślnym zakończeniu zadania konwersji, przy włączonej opcji aplikacja otwiera folder zawierający pierwszy wynik. Nie zaznacza pojedynczego pliku i nie otwiera wielu okien dla konwersji wsadowej.

Opcja nie działa dla:

- zadań anulowanych lub zakończonych błędem;
- zadań downloadera przekazanych przez rozszerzenie;
- ponowionych starych zadań, jeśli użytkownik wyłączył opcję przed ich zakończeniem.

Błąd uruchomienia Eksploratora nie zmienia statusu ukończonej konwersji.

## Pusty stan formatów

Przed wybraniem plików, pod nagłówkiem „Wybierz format wyniku”, widoczny jest komunikat „Najpierw wybierz pliki”. Po pojawieniu się co najmniej jednego zgodnego formatu komunikat znika, a jego miejsce zajmują kafelki formatów.

Jeżeli pliki zostały wybrane, ale nie mają wspólnego formatu docelowego, komunikat brzmi „Brak wspólnego formatu dla tego zestawu plików”.

ViewModel udostępnia jawny stan, dzięki czemu zachowanie jest testowalne i nie zależy od wizualnego drzewa WPF.

## Model i trwałość ustawień

Powstaje mały model ustawień zawierający:

- `ConversionPreset QualityPreset`;
- `OutputDirectoryMode OutputDirectoryMode`;
- `bool OpenFolderOnCompletion`.

Magazyn JSON używa zapisu przez plik tymczasowy i atomową zamianę. Brak pliku, uszkodzony JSON albo nieznana wartość enum przywraca bezpieczne wartości domyślne. Plik nie zawiera ścieżek źródłowych ani historii.

Zmiana kontrolki zapisuje ustawienia w tle. Błąd zapisu nie blokuje konwersji.

## Obsługa błędów

Brak dostępu do `Pobrane\QuickConvert` jest raportowany jak błąd zapisu zadania i nie powoduje pozostawienia gotowego pliku. Częściowe pliki nadal używają sufiksu `.quickconvert.partial`.

Jeżeli ustawienia nie mogą zostać odczytane lub zapisane, aplikacja działa z wartościami domyślnymi. Błędy otwierania Eksploratora są ignorowane po pomyślnym zakończeniu zadania.

## Testy

Testy jednostkowe obejmą:

- dokładną macierz argumentów FFmpeg dla trzech presetów;
- brak wpływu presetu na formaty bezstratne;
- rozwiązywanie ścieżki obok źródła i w katalogu pobierania;
- kolizje nazw w obu trybach;
- tworzenie katalogu docelowego;
- zapis, odczyt, wartości domyślne i uszkodzony JSON ustawień;
- komunikaty pustego stanu;
- automatyczne otwieranie tylko po udanej konwersji;
- obecność własnego stylu ekspandera i brak systemowego znacznika w głównym widoku.

Pełna weryfikacja zachowuje dotychczasowe testy integracyjne FFmpeg, kompilację WPF i testy rozszerzeń.

## Poza zakresem

- wybór dowolnego folderu przez dialog;
- ręczny wybór kodeka, bitrate, CRF, FPS lub rozdzielczości;
- osobne ustawienia dla każdego pliku w zadaniu wsadowym;
- automatyczne usuwanie źródła;
- profile użytkownika i synchronizacja chmurowa;
- ustawienia jakości downloadera, które pozostają w popupie rozszerzenia.

