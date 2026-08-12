# QuickConvert — Dark Fluent UI

## Cel

Odświeżyć główne okno QuickConvert tak, aby było czytelne, spójne z estetyką Windows 10/11 i proste w obsłudze. Zmiana dotyczy wyłącznie warstwy prezentacji. Istniejące polecenia, modele danych, kolejka, historia i zachowanie aplikacji pozostają bez zmian.

## Kierunek wizualny

Interfejs używa ciemnego motywu Fluent z grafitowym tłem `#10131A`, jaśniejszymi kartami, jasną typografią i fioletowym kolorem akcentu. Kontrast wszystkich podstawowych treści musi pozostać czytelny również wtedy, gdy systemowy styl WPF nie zostanie odziedziczony, dlatego główne powierzchnie otrzymają jawne kolory tła i tekstu.

Elementy korzystają z zaokrągleń 8–16 px, subtelnych obramowań zamiast ciężkich cieni oraz siatki odstępów opartej na 4 px. Domyślną czcionką pozostaje Segoe UI.

## Układ okna

Domyślny rozmiar okna wynosi około 800 × 640 px, z zachowaniem obecnej minimalnej wielkości. Górna część zawiera znak produktu, nazwę QuickConvert, krótki opis oraz status „Lokalnie • Bez chmury”.

Poniżej znajduje się zaokrąglony przełącznik trzech istniejących sekcji:

- Konwertuj;
- Historia;
- Informacje.

Przełącznik zastępuje wygląd standardowego `TabControl`, ale nadal korzysta z tego samego mechanizmu zakładek i nawigacji klawiaturą.

## Sekcja „Konwertuj”

Pierwsza karta ma czytelną hierarchię:

1. tytuł wyboru i krótka instrukcja;
2. dominujący przycisk „Wybierz pliki”;
3. dostępne formaty przedstawione jako kompaktowe kafelki;
4. ostrzeżenie o pierwszej klatce animacji, jeśli jest wymagane;
5. zwijane informacje o presecie i miejscu zapisu.

Pod kartą znajduje się kolejka. Każde zadanie jest osobną kartą z opisem, tekstowym stanem postępu, błędem oraz uporządkowanymi akcjami. Akcja podstawowa i neutralna nie może wyglądać tak samo jak anulowanie. Istniejące warunki dostępności poleceń pozostają źródłem stanów disabled.

## Historia i informacje

Historia używa kart o tej samej geometrii co kolejka. Wpis eksponuje opis, a status i datę pokazuje jako dane drugorzędne.

Sekcja informacji dzieli treści na osobne karty:

- folder pobierania i zasady prywatności;
- aktualizacje oraz lokalny log;
- strefę niebezpieczną z funkcją usuwania historii i logów.

Przycisk czyszczenia danych używa czerwonego wariantu i nie sąsiaduje bezpośrednio z normalnymi akcjami.

## Style i dostępność

W `App.xaml` powstaną współdzielone zasoby kolorów i style dla przycisków, kart, tekstu pomocniczego, zakładek, elementów list i paska postępu. Przyciski mają stany normal, hover, pressed, disabled i focus. Tekst podstawowy oraz pomocniczy zachowuje wysoki kontrast na każdej używanej powierzchni.

Zmiana nie wprowadza zewnętrznego frameworka UI. Nawigacja klawiaturą, polecenia MVVM oraz przewijanie pozostają aktywne. Teksty interfejsu otrzymają poprawne kodowanie UTF-8 i polskie znaki.

## Zakres techniczny

Zmiany obejmują przede wszystkim `App.xaml` i `MainWindow.xaml`. Kod widoku lub ViewModel zostanie zmieniony tylko wtedy, gdy będzie to konieczne do prezentacji już istniejącego stanu. Nie powstają nowe funkcje konwersji ani downloadera.

## Weryfikacja

- kompilacja całego rozwiązania w konfiguracji Release;
- istniejący zestaw testów jednostkowych i integracyjnych;
- testy rozszerzeń pozostają bez zmian;
- ręczne uruchomienie aplikacji i kontrola widoków Konwertuj, Historia oraz Informacje przy domyślnym i minimalnym rozmiarze;
- kontrola kontrastu, hover, focus i disabled;
- potwierdzenie, że polecenia wyboru plików, konwersji, anulowania, ponowienia, otwierania wyniku, logu, aktualizacji i czyszczenia danych nadal są podłączone.

## Poza zakresem

- automatyczne przełączanie jasnego i ciemnego motywu;
- nowa nawigacja boczna;
- przeciąganie i upuszczanie plików;
- animacje przejść;
- dodatkowe biblioteki ikon lub kontrolek;
- zmiany działania silnika i integracji systemowych.
