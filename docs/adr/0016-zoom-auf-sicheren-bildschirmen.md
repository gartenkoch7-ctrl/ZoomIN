# ADR-0016: Zoom auf Anmeldebildschirm und sicheren Bildschirmen

- **Status:** Angenommen (Grilling 2026-11-03; technischer Weg wird im Spike von M10 bestätigt)
- **Datum:** 2026-11-03

## Kontext
Anmeldung nach dem Start, Sperrbildschirm, Strg+Alt+Entf (inkl. „Kennwort ändern“) und
UAC-Abfragen laufen auf dem **sicheren Desktop** `winsta0\Winlogon`. Die normale ZoomIN-Instanz
läuft auf dem Desktop `Default` des Benutzers und kann dort weder erfassen (Desktop Duplication
liefert `ACCESS_LOST`) noch etwas anzeigen. Sehbehinderte Nutzer stehen genau an der Stelle ohne
Vergrößerung da, an der sie ein Kennwort eingeben müssen. Bildschirmlupen und Screenreader lösen
das, indem ein Prozess mit SYSTEM-Rechten auf dem sicheren Desktop läuft.

## Entscheidung
- **Weg:** Windows-eigene Registrierung für Hilfsmittel (Erleichterte Bedienung). ZoomIN trägt
  sich unter `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Accessibility\ATs\ZoomIN` ein;
  Windows (`AtBroker.exe`) startet ZoomIN selbst auf jedem sicheren Desktop und beendet es beim
  Wechsel zurück. **Kein eigener Dienst.** Genaue Werte (`StartExe`, `StartParams`,
  `TerminateOnDesktopSwitch`, `Configuration` in HKLM/HKCU …) klärt der Spike und hält sie in
  `docs/kb/sichere-desktops.md` fest.
- **Umfang:** alle vier sicheren Bildschirme – Anmeldung nach Start, Sperre, Strg+Alt+Entf,
  UAC-Abfragen.
- **Sicherer Modus:** dieselbe `current\ZoomIN.exe`, gestartet mit `--secure-desktop`. Volle
  Zoom-Engine (alle Modi, Farbfilter inkl. pro Monitor, Mauszeiger, Fokus/Caret-Tracking,
  Tastenkürzel, Profilwechsel) – aber **ohne WinUI, Tray, Einstellungsfenster, Updates,
  Dateidialoge, Links oder Hilfe**. Bedienung nur über Tastenkürzel. Eigener Einzelinstanz-Name,
  damit er nicht mit der Benutzerinstanz kollidiert.
- **Einstellungen (Hybrid):**
  - In einer angemeldeten Sitzung (Sperre, Strg+Alt+Entf, UAC) liest der sichere Modus Einstellungen
    und Profile **des Sitzungsbenutzers** – nur lesen, Größenlimit, keine Reparse-Points folgen,
    jeder Wert wird auf seinen gültigen Bereich begrenzt.
  - Davon legt er eine Kopie in SYSTEMs eigenem Profil ab
    (`%SystemRoot%\System32\config\systemprofile\AppData\Local\ZoomIN\secure\`, Schreibrecht nur
    SYSTEM und Administratoren). Diese Kopie gilt auf dem Anmeldebildschirm nach dem Start, wenn noch niemand
    angemeldet ist. Fehlt sie: Standardwerte.
- **Live-Zustand, Einbahn:** Meldet Windows Sperre bzw. Desktopwechsel, speichert die
  Benutzerinstanz ihren aktuellen Zustand (Zoom an/aus, Stufen, Modus, Filter) sofort; der sichere
  Modus startet damit. Änderungen auf dem sicheren Desktop werden **nicht** zurückgeschrieben –
  SYSTEM schreibt nie in Benutzerordner.
- **Ein/Aus:** Standard „an“. Kontrollkästchen im Setup (vorausgewählt), ein gemeinsamer Schalter
  in der App (Seite „Allgemein“, Admin-Abfrage, weil HKLM). Die Wahl wird maschinenweit gemerkt;
  Updates (`--apply-update`, ohnehin erhöht) schalten bestehende Installationen ein, außer der
  Nutzer hat ausgeschaltet. Deinstallation entfernt die Registrierung und den ZoomIN-Ordner in SYSTEMs Profil.
  Das portable ZIP unterstützt die Funktion nicht.
- **Sicherheitsregeln:**
  1. Der Tastatur-Hook liefert im sicheren Modus (wie überall) nur zwei Ereignisse nach außen:
     „Zeichentaste gedrückt“ und „Strg allein“. Kein Tastencode verlässt den Hook, nichts wird
     geloggt; ein Test sichert die Schnittstelle ab.
  2. UI Automation liefert nur Rechtecke (Fokus, Caret), nie Text oder Werte.
  3. Keine Netzwerkzugriffe, keine Prozessstarts außer dem eigenen Wächter.
  4. Logs in SYSTEMs Profil (`…\systemprofile\AppData\Local\ZoomIN\logs`, nur SYSTEM/Admins),
     ohne Eingabedaten.
  5. **Nicht `%ProgramData%`** (Nachtrag 2026-11-03, vor dem Merge): Dort dürfen Benutzer Ordner
     anlegen und könnten per Verzeichnis-Link SYSTEMs Schreibzugriffe umlenken.
  6. Gelesene Benutzerdateien: Der Pfad, den Windows tatsächlich geöffnet hat, muss dem erwarteten
     entsprechen (keine Junctions/Symlinks, auch nicht nachträglich getauscht), genau ein
     Dateiname (kein Hardlink), höchstens 256 KB.
- **Notbremse:** Strg+Alt+Z schaltet den Zoom aus; eigenes Kürzel zum Beenden nur im sicheren
  Modus: **Strg+Alt+Umschalt+Z** (Q schied aus, AltGr+Q ergibt @ – ADR-0008); Wächter startet höchstens 3×
  neu, danach bleibt der sichere Modus bis zum nächsten Systemstart aus; liefert die Erfassung
  kein Bild, zeigt das Overlay **nichts** statt Schwarz.
- **Startzeit:** vergrößertes Bild spätestens **1 s** nach Win+L bzw. Strg+Alt+Entf (gemessen und
  geloggt).
- **Plan B:** Reicht der Windows-Weg nicht (Z-Reihenfolge über LogonUI/UAC, Desktop Duplication
  dort nicht möglich), folgt ein **minimaler Startdienst** unter SYSTEM, der nur Desktopwechsel
  beobachtet und den sicheren Modus startet – mit eigenem ADR und Rücksprache vor dem Bau.

## Verworfen
- **Eigener SYSTEM-Dienst als Erstweg:** mehr Code, dauerhaft laufender Systemprozess, größere
  Angriffsfläche (ADR-0014 hat einen Dienst schon verworfen). Bleibt nur Plan B.
- **Einstellungsfenster/Mini-Menü auf dem sicheren Desktop:** Datei-Dialoge, Links und Hilfe in
  einem SYSTEM-Prozess sind bekannte Wege zur Rechteausweitung.
- **Nur explizite Kopie der Einstellungen (mit Admin-Abfrage):** sicherer, aber Änderungen kämen
  erst nach erneutem Klick an. **Eigenes Anmelde-Profil:** mehr UI, mehr Pflege.
- **Rückschreiben von Änderungen auf dem sicheren Desktop:** Datenweg SYSTEM → Benutzer.
- **Tastatur-Hook im sicheren Modus abschalten:** kostet „Zeiger beim Tippen ausblenden“ und den
  Strg-Zeigerfinder; die Zwei-Ereignis-Schnittstelle schützt Kennwörter ausreichend.

## Konsequenzen
- Neues Meilensteindokument [M10](../plan/m10-sichere-bildschirme.md), beginnend mit einem Spike.
- Die Zusammenstellung der Engine muss ohne WinUI laufen (gemeinsamer Teil von `AppShell` und
  sicherem Modus).
- Code, der als SYSTEM läuft, bekommt eine Sicherheitsprüfung (`/security-review`) vor dem Merge.
- Clean-Room: Verhalten anderer Lupen nur aus öffentlichen Quellen; Open-Source-Screenreader
  (z. B. NVDA, GPL) dürfen als Hinweis auf Windows-Mechanismen dienen, Code wird nicht übernommen.
- `docs/kb/rendering-pipeline.md` („Secure Desktop … nicht im MVP“) wird mit M10 aktualisiert.
