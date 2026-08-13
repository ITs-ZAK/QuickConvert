# QuickConvert v0.2.2 — Zen, praca w tle i zasobnik

## Cel

Naprawić przekazywanie pobrań z Zen/Firefoksa do QuickConvert, zachowując zgodność z Chrome. Dodać ustawienie sterujące zachowaniem aplikacji podczas aktywnych zadań oraz zastąpić systemową ikonę zasobnika właściwym logo QuickConvert. Opublikować całość jako `v0.2.2`.

## Potwierdzona przyczyna problemu Zen

Diagnostyka na zainstalowanym Zen potwierdziła wszystkie granice przepływu:

- dodatek `quickconvert@local` jest aktywny;
- Zen odczytuje standardowy klucz `Software\Mozilla\NativeMessagingHosts`;
- manifest Native Messaging dopuszcza `quickconvert@local`;
- bezpośrednie `sendNativeMessage` zwraca poprawną odpowiedź hosta;
- popup wysyła prawidłowy URL YouTube oraz parametry `mp4` i `best`;
- skrypt tła odbiera wiadomość z `popup.html`;
- żądanie nie dociera do kolejki QuickConvert.

Obecny listener tła zawsze używa wzorca Chrome `sendResponse(...)` oraz `return true`. Dla Zen/Firefoksa zastosujemy preferowany przez tę platformę kontrakt polegający na bezpośrednim zwróceniu `Promise`. Chrome zachowa ścieżkę callbackową, aby nie podnosić minimalnej wymaganej wersji przeglądarki.

## Obsługa wiadomości w rozszerzeniach

Wspólny `extensions/shared/background.js` rozpozna środowisko po obecności natywnego `globalThis.browser`:

- Firefox i Zen: listener jest zwykłą funkcją, która wyłącznie dla `action === "download"` zwraca Promise z `browser.runtime.sendNativeMessage`;
- Chrome: listener wywołuje `chrome.runtime.sendNativeMessage`, przekazuje wynik przez `sendResponse` i zwraca `true`;
- wiadomości innego typu zwracają `false` i nie rezerwują kanału odpowiedzi;
- brak odpowiedzi albo odrzucenie Native Messaging mapuje się na `{ code: "app_unavailable" }` w obu ścieżkach;
- poprawna odpowiedź hosta jest przekazywana do popupu bez zmian.

Popup nie będzie sam się zamykał. Przy włączonej pracy w tle aplikacja nie przejmie fokusu, więc popup pokaże komunikat „Dodano do kolejki QuickConvert”. Przy wyłączonej pracy w tle QuickConvert otworzy okno i systemowe zamknięcie popupu po utracie fokusu pozostaje oczekiwane.

Manifesty Chrome i Firefox otrzymają wersję `0.2.2`. Identyfikatory rozszerzeń, klucz Chrome i `quickconvert@local` pozostają bez zmian.

## Ustawienie pracy w tle

Do `QuickConvertSettings` zostanie dodane pole `RunInBackgroundDuringJobs`, domyślnie `true`. Brak pola w istniejącym `settings.json` ma zostać potraktowany jak wartość domyślna, dzięki czemu aktualizacja nie wymaga migracji ani usuwania ustawień.

W sekcji „Więcej ustawień” pojawi się checkbox:

```text
Pracuj w tle podczas zadań
Po zamknięciu okna aktywne zadania pozostaną w zasobniku.
```

Zmiana zapisuje się natychmiast tak samo jak jakość, folder wyjściowy i otwieranie folderu po ukończeniu.

Ładowanie ustawień otrzyma jawny sygnał zakończenia. Obsługa pierwszego żądania Native Messaging musi poczekać na ustawienia, aby zapisana wartość `false` nie została chwilowo zastąpiona domyślnym `true` podczas startu aplikacji.

## Zachowanie okna i zasobnika

### Gdy „Pracuj w tle podczas zadań” jest włączone

- żądanie pobrania z rozszerzenia uruchamia lub wykorzystuje aplikację bez wymuszania pokazania okna;
- jeśli okno było już widoczne, nie jest automatycznie ukrywane;
- podczas aktywnego zadania ikona QuickConvert jest widoczna w zasobniku;
- kliknięcie `X` podczas aktywnego zadania ukrywa okno, ale nie anuluje zadania;
- dwuklik ikony albo „Otwórz” przywraca okno;
- „Zamknij” w menu zasobnika zachowuje obecne jawne zamknięcie aplikacji;
- po zakończeniu ostatniego zadania ukryta aplikacja pokazuje krótkie powiadomienie, daje systemowi czas na jego opublikowanie, a następnie kończy działanie; ikona nie pozostaje stale w zasobniku.

### Gdy ustawienie jest wyłączone

- każde zadanie otrzymane z rozszerzenia pokazuje lub przywraca okno QuickConvert;
- ikona zasobnika nie jest używana do pracy w tle;
- kliknięcie `X` podczas aktywnego zadania nie zamyka aplikacji, nie ukrywa okna i nie anuluje zadania;
- po zakończeniu zadania użytkownik może normalnie zamknąć aplikację.

Konwersje uruchomione z menu pliku nadal pokazują okno niezależnie od ustawienia. Aktywacja już uruchomionej aplikacji również nadal pokazuje okno.

## Logo zasobnika

`NotifyIcon` nie będzie używać `SystemIcons.Application`. Ikona zostanie wczytana z osadzonego `Assets/quickconvert.ico`, tego samego źródła co ikona okna i pliku wykonywalnego.

Strumień zasobu i obiekt `Icon` będą miały jednoznacznie zarządzany czas życia. `NotifyIcon`, menu kontekstowe i ikona zostaną zwolnione podczas rzeczywistego zamykania aplikacji, dopiero po krótkim okresie potrzebnym na publikację końcowego powiadomienia. Logo ma być widoczne przy każdym stanie zasobnika: aktywne zadanie, praca w ukryciu i krótkie powiadomienie końcowe.

## Podział odpowiedzialności

- `extensions/shared/background.js` — wyłącznie adaptacja Firefox/Zen kontra Chrome i mapowanie błędu transportu;
- `QuickConvertSettings` oraz `JsonSettingsStore` — trwała wartość `RunInBackgroundDuringJobs` i kompatybilne wstecz ładowanie;
- nowa czysta polityka okna/zasobnika — decyzje `show`, `hide`, `block close` i `show tray` na podstawie rodzaju wywołania, ustawienia oraz aktywnych zadań;
- `MainViewModel` — udostępnienie ustawienia i sygnału zakończenia jego ładowania;
- `App` — decyzja, czy żądanie IPC ma pokazać okno;
- `MainWindow` — wykonanie decyzji zamykania oraz obsługa właściwej ikony zasobnika.

Czysta polityka nie będzie zależeć od WPF ani WinForms, aby wszystkie warianty zachowania dało się sprawdzić testami jednostkowymi.

## Obsługa błędów

- odrzucenie `sendNativeMessage`, brak hosta lub brak odpowiedzi daje popupowi `app_unavailable`;
- kod odpowiedzi Native Hosta nie jest maskowany;
- błąd odczytu osadzonego logo zatrzymuje tworzenie okna z jednoznacznym błędem deweloperskim zamiast cichego użycia obcej ikony systemowej;
- błąd zapisu ustawienia nie przerywa aktywnego zadania i zachowuje dotychczasowy lokalny model best-effort;
- ustawienie nigdy nie anuluje zadań ani nie zmienia plików źródłowych.

## Testy i kryteria odbioru

### Rozszerzenia

- test Zen/Firefox potwierdza, że listener zwraca Promise i przekazuje `accepted`;
- test Chrome potwierdza `return true` oraz odpowiedź przez callback;
- oba środowiska mapują odrzucenie Native Hosta na `app_unavailable`;
- wiadomość inna niż `download` nie otwiera kanału;
- popup nadal generuje prawidłowy payload dla filmu, Shorts i linku playlisty z bieżącym filmem;
- ręczny test w Zen kończy się wpisem `download` w kolejce i plikiem w `Pobrane\QuickConvert`.

### Aplikacja

- stare ustawienia bez nowego pola ładują `RunInBackgroundDuringJobs = true`;
- zapis i ponowne wczytanie zachowują wybraną wartość;
- polityka tła pokrywa wszystkie kombinacje: aktywne/brak zadań, okno widoczne/ukryte, ustawienie włączone/wyłączone;
- przy `false` kliknięcie `X` podczas zadania pozostawia widoczne okno i aktywne zadanie;
- przy `true` kliknięcie `X` ukrywa okno, a zadanie trwa;
- żądanie rozszerzenia pokazuje okno tylko przy `false`;
- test kontraktu kodu odrzuca `SystemIcons.Application` i wymaga `Assets/quickconvert.ico` dla `NotifyIcon`;
- pełne 49 dotychczasowych testów oraz nowe testy przechodzą;
- build kończy się bez ostrzeżeń.

### Wydanie

- wersja aplikacji, instalatora i obu manifestów wynosi `0.2.2`;
- Git tag i GitHub Release to `v0.2.2` / `QuickConvert v0.2.2`;
- publiczny instalator nazywa się `QuickConvert-0.2.2-win-x64-setup.exe`;
- release zawiera instalator, XPI i `SHA256SUMS.txt`;
- po instalacji publicznego artefaktu Zen rozpoczyna pobranie, QuickConvert respektuje ustawienie tła, a zasobnik pokazuje logo produktu.

## Poza zakresem

- automatyczne publikowanie w Chrome Web Store lub AMO;
- podpisywanie XPI, Authenticode i zmiana istniejących identyfikatorów dodatku;
- obsługa playlist, DRM, logowania, cookies lub innych serwisów;
- stałe działanie aplikacji bez aktywnych zadań;
- anulowanie zadania przy kliknięciu `X`;
- zmiany formatów, presetów albo silników FFmpeg/yt-dlp.
