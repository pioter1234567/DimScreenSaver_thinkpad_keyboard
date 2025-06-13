# DimScreenSaver

**DimScreenSaver** to inteligentny ściemniacz ekranu dla systemu Windows, który automatycznie przyciemnia ekran po okresie bezczynności, wyłącza i automatycznie przywraca podświetlenie klawiatury w Lenovo ThinkPad T470 (możliwe, że na innych też). Program działa w tle jako aplikacja w trayu i oferuje pełną kontrolę nad zachowaniem systemu podczas nieaktywności.

Wersja release - gotowa do zainstalowania ze wszystkim co potrzeba - dostępna po prawej stronie w zakładce Releases

Uwaga: Ponieważ jest to projekt hobbystyczny nie posiada płatnych certyfikatów z holywoodu i Windows Defender będzie wyświetlał ostrzeżenie, że plik może być niebezpieczny - należy wtedy kliknąć więcej opcji Uruchm mimo to. 
Avast skanuje przez około minutę i wyświetla brak zagrożeń. Program dostępny open-sourcowo, więc w razie wątpliwości/obaw można przejrzeć kod i skompilować samodzielnie.



## 🔧 Funkcje

- Automatyczne przyciemnianie ekranu po ustawionym interwale bezczynności
- Osobny interwał na całkowite wyłączenie ekranu
- Lewy klik na ikonce w trayu umożliwia ręczne ustawienie jasności ekranu
- Program posiada funkcję wykrywania pracy sterownika audio. Po zaznaczeniu opcji **nie przyciemnia ekranu, jeśli trwa odtwarzanie dźwięku** (np. YouTube, Netflix)
- Jeżeli zadana jasność przygaszenia jest **wyższa niż aktualnie ustawiona robocza jasność ekranu**, to zmiana jasności po zadanym interwale **nie następuje**
- Automatyczne sterowanie podświetleniem klawiatury Lenovo (wyłączenie podświetlenia przy przygaszeniu lub wyłączeniu ekranu oraz ponowne automatyczne włączenie podświetlenia przy wybudzeniu):
	Domyślne progi jasności klawiatury w zależności od podświetlenia ekranu (do zmiany w Opcjach...):
	 - ekran 0-29%  -> klawiatura stopień 1 (ciemniejszy)
	 - ekran 30-59% -> klawuatura stopień 2 (jaśniejszy)
	 - ekran 60+	-> klawiatura stopień 0 (podświetlenie wyłączone - ustawienie ekranu powyżej 50% sugeruje, że jest jasno i podświetlenie klawiatury nie jest potrzebne)
  - Jeżeli użytkownik ręcznie zmieni poziom (np. klawiszem Fn+Space), program **nie nadpisuje go**, dopóki:
    - nie zmieni się jasność ekranu
    - nie nastąpi restart programu/systemu
    - nie wybudzi się system z uśpienia
		Uwaga: (Jeśli nie chcesz, by Lenovo pokazywało ikonkę zmiany jasności klawiatury za każdym razem, gdy następuje zmiana – zobacz: „Jak wyłączyć OSD Lenovo” poniżej)
- Wbudowany **watchdog audio**, który wykrywa odtwarzanie dźwięku i zawiesza wygaszanie.
- Obsługa **programu Panelo** (własne narzędzie autora) – funkcja prywatna, można ją wyłączyć.
- Logi działania programu zapisywane są do pliku:  
  `%TEMP%\scrlog.txt`
- Zapis aktualnej jasności do pliku:  
  `%TEMP%\brightness.txt`
- Ustawienia użytkownika przechowywane są w pliku:  
  `%APPDATA%\DimScreenSaver\settings.cfg`  
  Można edytować go ręcznie, aby ustawić niestandardowe wartości spoza menu traya
- Ewentualne błędy i wyjątki zapisywane są do:  
  `%APPDATA%\DimScreenSaver\crashlog.txt`

## 🧪 Testowane na

- 💻 **Lenovo ThinkPad T470** z systemem Windows 10/11  
  (wymagany działający IMController oraz `Keyboard_Core.dll` do sterowania podświetleniem klawiatury)

## 📁 Zawartość release

| Plik                               | Opis                                                 |
|------------------------------------|------------------------------------------------------|
| `alert.mp4`, `budzik.mp4`          | Wideo odtwarzane przy zdarzeniach                    |
| `AxInterop.WMPLib.dll`             | AxHost interfejs z Windows Media Player ActiveX      |
| `batterysaver.check`               | Pomocniczy exe do detekcji Oszczędziania Baterii     |
| `DimScreenSaver.exe`               | Główna aplikacja                                     |
| `DimScreenSaver.exe.config`        | Konfiguracja aplikacji                               |
| `dim.ico`, `uninstall.ico`         | Ikony programu i deinstalatora                       |
| `Interop.WMPLib.dll`               | Obsługa Windows Media Player                         |
| `Keyboard_Core.dll`                | Obsługa podświetlenia klawiatury Lenovo              |
| `Microsoft.Win32.Registry.dll`     | Obsługa operacji na rejestrze                        |
| `NAudio*.dll`                      | Biblioteka audio                                     |
| `notif.wav`, `error.wav`, `pending.wav` | Dźwięki sygnalizacyjne                          |
| `README.md`                        | Dokumentacja projektu                                |
| `System.Security.AccessControl.dll`| Kontrola dostępu i uprawnień                         |
| `System.Security.Principal.Windows.dll`   | Obsługa kontekstów użytkownika i SID-ów       |


🔧 Uwaga: DimScreenSaver korzysta z zewnętrznego programu exe batterysaver.check. Kod źródłowy znajduje się w folderze helper/.

## ✅ Wymagania

- Windows 10 / 11 (64-bit)
- .NET Framework 4.8
- Sterownik `Keyboard_Core.dll` (opcjonalnie do sterowania jasnością klawiatury Lenovo ThinkPad) 
- Brak potrzeby instalacji – wystarczy rozpakować i uruchomić

## ▶️ Jak używać

1. Rozpakuj i uruchom `DimScreenSaver.exe` lub zainstaluj przez Instalator, który pozwala dodać aplikację do autostartu
2. Aplikacja pojawi się jako ikonka w zasobniku systemowym (tray)
3. Kliknij **lewym przyciskiem myszy**, aby ręcznie ustawić jasność ekranu
4. Kliknij **prawym przyciskiem**, aby otworzyć menu z ustawieniami trybów, timerów i funkcji
5. Gotowe! Program działa w tle, automatycznie reagując na bezczynność i aktywność systemu

## 📦 Instalacja (opcjonalna)

Aplikacja może jest dostarczana z instalatorem (zbudowanym przez Inno Setup). Instalator przenosi wszystkie pliki do katalogu `{app}` i rejestruje autostart.

## 💡 Jak wyłączyć OSD Lenovo (TPOSD.exe)

Jeśli używasz laptopa Lenovo (np. ThinkPad T470), to przy każdej zmianie poziomu podświetlenia klawiatury może pojawiać się ekranie irytujące okienko OSD (ikonka z symbolem podświetlenia klawiatury).

Można to całkowicie wyłączyć bez wpływu na funkcję klawiszy Fn oraz działanie programu DimScreenSaver
🔧 Jak to zrobić?

    Wciśnij Win + R, wpisz services.msc, naciśnij Enter

    Znajdź usługę o nazwie Lenovo Hotkey Client Loader (lub coś podobnego)

    Kliknij prawym, wybierz Właściwości

    Kliknij Zatrzymaj, a potem ustaw Typ uruchomienia na Wyłączony

    Zatwierdź

Po tej operacji OSD nie będzie się już wyświetlać, a wszystkie funkcje programu DimScreenSaver będą nadal działać prawidłowo.

---


## ⚠️ Zastrzeżenie (czyli „jak coś się zespuje, to nie moja wina”)

Ten program działa u mnie™ i generalnie nie próbuje spalić Ci laptopa, wszystko jest oparte o oficjalne API i legalne, wspierane biblioteki.
Nie pisze do rejestru (poza autostartem), nie zbiera danych, nie łączy się z netem, nie modyfikuje sterowników, nie ingeruje w firmware.

Ale:

- Jak ekran się nie włączy – to wiedz, że coś się dzieje. 😈
- Jak nagle klawiatura świeci jak choinka, a potem gaśnie – to nie duchy, to funkcja świąteczna.
- Jak coś innego nie działa to... cóż 🤷‍

Program jest udostępniony **tak jak jest**. Bez gwarancji. Bez wsparcia technicznego z call center w Indiach.  
Używasz – **na własne ryzyko**. 

Zanim odpalisz to na sprzęcie do respiratora – **lepiej przemyśl sprawę.**

Miłego przygaszania! 😎

---



## ENGLISH

# DimScreenSaver

**DimScreenSaver** is an intelligent screen dimmer for Windows that automatically dims the display after a period of inactivity and controls the keyboard backlight for Levovo ThinkPad T470 (other models possibly too). The app runs silently in the system tray and offers full control over screen behavior when the user is idle.

Release version that includes everything you need for the program to work is available in Releases section on the right hand side.

Note: Since this is a hobby project, it doesn't include any paid Hollywood-grade certificates. As a result, Windows Defender may show a warning that the file could be unsafe — in that case, click "More info" → "Run anyway."
Avast scans it for about a minute and then confirms no threats.
The program is available as open source, so if you have any doubts or concerns, you can review the code and build it yourself.

## 🔧 Features

- Automatically dims the screen after a configurable idle interval
- Separate interval for turning off the screen completely
- Left-clicking the tray icon opens a brightness control pop-up
- Includes audio driver monitoring: **when enabled, the screen will not dim while audio is playing** (e.g. YouTube, Netflix)
- If the configured dimm brightness is **higher than the current screen brightness**, the brightness **will not be changed**
- Automatic control of Lenovo keyboard backlight:
  - Backlight level is determined based on screen brightness, default levels (can be chaged in Settings):
    - screen 0–29% → backlight level 1 (dimmer)
    - screen 30–59% → backlight level 2 (brighter)
    - screen 60%+ → backlight off (assumes ambient light is sufficient)
  - If the user manually changes the backlight (e.g. with Fn+Space), the app **will not override it** unless:
    - screen brightness changes (e.g. due to dimming interval)
    - the app/system restarts
    - the system wakes from sleep
		Note: If you don’t want Lenovo to show a keyboard backlight icon every time the brightness level changes, see: “How to disable Lenovo OSD” below.
- Built-in **audio watchdog** that detects sound playback and pauses dimming
- Optional support for **Panelo** (author’s private tool) – can be disabled
- Logs are saved to:  
  `%TEMP%\scrlog.txt`
- Last known brightness is saved to:  
  `%TEMP%\brightness.txt`
- User settings are saved in:  
  `%APPDATA%\DimScreenSaver\settings.cfg`  
  You can edit this file manually to define custom values beyond what's in the tray menu
- Crash reports are saved to:  
  `%APPDATA%\DimScreenSaver\crashlog.txt`

## 🧪 Tested on

- 💻 **Lenovo ThinkPad T470** running Windows 10/11  
  (Requires IMController and `Keyboard_Core.dll` for controlling the keyboard backlight)

## 📁 File contents for release version

| File                                | Description                                           |
|-------------------------------------|-------------------------------------------------------|
| `alert.mp4`, `budzik.mp4`           | Video files triggered by various events               |
| `AxInterop.WMPLib.dll`              | AxHost interface for Windows Media Player ActiveX     |
| `batterysaver.check`                | Helper tool for detecting Battery Saver state         |
| `DimScreenSaver.exe`                | Main application executable                           |
| `DimScreenSaver.exe.config`         | Application config file                               |
| `dim.ico`, `uninstall.ico`          | Tray and uninstaller icons                            |
| `Interop.WMPLib.dll`                | Interop with Windows Media Player                     |
| `Keyboard_Core.dll`                 | Lenovo keyboard backlight control                     |
| `Microsoft.Win32.Registry.dll`      | Registry operations support                           |
| `NAudio*.dll`                       | NAudio audio library                                  |
| `notif.wav`, `error.wav`, `pending.wav` | Notification sounds                               |
| `README.md`                         | Project documentation                                 |
| `System.Security.AccessControl.dll` | Access control and permissions management             |
| `System.Security.Principal.Windows.dll` | User identity and SID management                  |

🔧 Note: DimScreenSaver uses an external exe application batterysaver.check. Source code for that can be found in helper/.



## ✅ Requirements

- Windows 10 / 11 (64-bit)
- .NET Framework 4.8
- `Keyboard_Core.dll` (optional, for Lenovo ThinkPad keyboard backlight)
- No installation required – just unpack and run

## ▶️ How to use

1. Unpack and run `DimScreenSaver.exe`, or install via the provided installer to enable autostart
2. The app will appear as an icon in the system tray
3. **Left-click** the icon to adjust screen brightness manually
4. **Right-click** to open the settings menu and configure intervals, modes, and behavior
5. That’s it! The program now runs in the background, managing screen and keyboard behavior based on user activity

## 📦 Installation (optional)

The app may be delivered with an installer (created with Inno Setup). It copies all files to the `{app}` folder and registers the app in autostart.


## 💡 How to disable Lenovo OSD (TPOSD.exe)

If you're using a Lenovo laptop (e.g. ThinkPad T470), you may see an annoying on-screen popup (OSD) every time the keyboard backlight level changes — usually a small icon with a lightbulb or brightness bar.

This is not required for any keyboard functions to work, and can be completely disabled without affecting Fn keys or the DimScreenSaver program.
🔧 How to disable it:

    Press Win + R, type services.msc, and hit Enter

    Find a service named Lenovo Hotkey Client Loader (or something similar)

    Right-click it and choose Properties

    Click Stop, then set Startup type to Disabled

    Click OK

After that, the OSD popup will no longer appear, and all DimScreenSaver features will continue to work as intended.

---

## ⚠️ Disclaimer (aka “if it breaks, not my fault”)

This program works on my machine™ and generally doesn't try to fry your laptop. It’s based entirely on official Windows APIs and supported libraries.  
It doesn't write to the registry (except for autostart), doesn't collect data, doesn’t connect to the Internet, doesn’t modify drivers, and doesn’t touch your firmware.

However:

- If your screen won’t turn back on — this is fine ☕
- If your keyboard lights up like a Christmas tree and then goes dark — that’s not ghosts, that’s a feature.
- If something else breaks… well, 🤷‍

The program is provided **as is**, with no guarantees and no support hotline in India.  
You use it **at your own risk**.

If you’re thinking of running it on life-support equipment — **maybe don’t.**

Happy dimming! 😎

---
