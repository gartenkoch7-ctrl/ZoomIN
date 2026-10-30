# M7 – App, Einstellungen & Profile

**Ziel:** Alles, was bisher per JSON eingestellt wird, über eine schöne, selbst barrierefreie
Oberfläche bedienbar machen.
**Demo:** Tray-Icon → Einstellungen → Zoom, Modus, Filter live ändern; Profil per
`Strg+Alt+P` wechseln; Erststart-Assistent fragt nach Autostart.
**Voraussetzungen:** M4, M5, M6.

## Entscheidungen (Grilling 2026-10-30)
- Einstellungen wirken **sofort live** und werden gespeichert; „Auf Standard zurücksetzen“ je Seite
- **Autostart** wird im **Erststart-Assistenten** abgefragt (Vorauswahl: ja)
- **Profile manuell** wechseln (Hotkey/Tray); automatisch je Programm erst nach dem MVP
- **Update-Prüfung opt-in:** nur Abfrage der neuesten GitHub-Release-Version, nur Hinweis,
  kein Auto-Install, keine weiteren Daten
- Sprachen: Deutsch und Englisch

## Aufgaben

### 1. Einstellungen (`Core/Settings/`, `Platform/Settings/`)
- [ ] `AppSettings` vollständig, `schemaVersion`, Migrationen (ADR-0004)
- [ ] `JsonSettingsStore`: atomar schreiben, Backup bei defekter Datei
- [ ] Profile: anlegen, umbenennen, duplizieren, wechseln, Export/Import
- [ ] Änderungen wirken live über `RenderSnapshot`; Schreiben entprellt (z. B. 500 ms)

### 2. Einstellungsfenster (`App/Views/`, `App/ViewModels/`)
- [ ] `NavigationView`: Allgemein · Zoom · Modi · Farben & Zeiger · Tracking ·
      Tastenkürzel · Profile · Info
- [ ] Hotkey-Editor mit Warnung bei AltGr- und Systemkollisionen (ADR-0008)
- [ ] Filtervorschau, Mica, Hell/Dunkel/System
- [ ] Jede Seite = eigene View + ViewModel (300-Zeilen-Regel)

### 3. Tray & Lebenszyklus
- [ ] Tray-Menü: Zoom an/aus, Modus, Profil, Einstellungen, Beenden
- [ ] Single Instance (zweiter Start öffnet Einstellungen)
- [ ] Autostart über Run-Key `HKCU` (startet ZoomIN + Watchdog)
- [ ] Erststart-Assistent: Zoom ausprobieren · Tastenkürzel (inkl. Hinweis
      „Strg+Mausrad gehört jetzt ZoomIN“) · Farben · Autostart · Update-Prüfung

### 4. Update-Hinweis (`Platform/Updates/`)
- [ ] Nur wenn erlaubt: beim Start GitHub-API „latest release“ abfragen, Version vergleichen,
      Hinweis im Tray; Fehler still ignorieren

### 5. Barrierefreiheit der eigenen UI
- [ ] Vollständig per Tastatur, sichtbarer Fokus, Narrator-Test, Kontrastdesigns
- [ ] Einstellungsfenster wird **nicht** aus Aufnahmen ausgeschlossen

### 6. Lokalisierung
- [ ] Alle Texte in `.resw`, Deutsch und Englisch

## Akzeptanzkriterien
- [ ] Jede Einstellung aus M2–M6 in der UI änderbar und sofort wirksam
- [ ] Defekte `settings.json` → Start mit Standardwerten + Meldung
- [ ] Narrator- und Tastaturtest bestanden
- [ ] Ohne Zustimmung keinerlei Netzwerkzugriff (prüfen mit Netzwerkmonitor)

## Offene Fragen / Risiken
- Tray-Bibliothek für WinUI 3 (z. B. H.NotifyIcon): Lizenz MIT-kompatibel prüfen.
- Startzeit unpackaged messen; Ziel: Tray < 1 s nach Login.
