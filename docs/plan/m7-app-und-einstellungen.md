# M7 – App, Einstellungen & Profile

**Ziel:** Alles, was bisher per JSON eingestellt wird, über eine schöne, selbst barrierefreie
Oberfläche bedienbar machen.
**Demo:** Tray-Icon → Einstellungen öffnen → Zoomstufe, Modus, Filter live ändern;
Profil „Büro“ / „Abends“ wechseln; Autostart aktivieren.
**Voraussetzungen:** M4, M5, M6.

## Aufgaben

### 1. Einstellungen (`Core/Settings/`, `Platform/Settings/`)
- [ ] `AppSettings` vollständig, `schemaVersion`, Migrationen (ADR-0004)
- [ ] `JsonSettingsStore`: atomar schreiben, Backup bei defekter Datei
- [ ] Profile: anlegen, umbenennen, duplizieren, wechseln (Hotkey), Export/Import
- [ ] Änderungen wirken live über `RenderSnapshot`

### 2. Einstellungsfenster (`App/Views/`, `App/ViewModels/`)
- [ ] `NavigationView` mit Seiten: Allgemein · Zoom · Modi · Farben & Zeiger · Tracking ·
      Tastenkürzel · Profile · Info
- [ ] Hotkey-Editor mit Kollisionsprüfung
- [ ] Vorschau für Farbfilter
- [ ] Mica-Hintergrund, Hell/Dunkel/System, große Schrift möglich
- [ ] Jede Seite = eigene View + ViewModel (300-Zeilen-Regel)

### 3. Tray & Lebenszyklus
- [ ] Tray-Icon mit Menü: Zoom an/aus, Modus, Profil, Einstellungen, Beenden
- [ ] Single Instance (zweiter Start öffnet Einstellungen)
- [ ] Autostart (Run-Key `HKCU`) – Option, standardmäßig aus
- [ ] Erststart-Assistent: 3 Schritte (Zoom ausprobieren, Hotkeys, Farben)

### 4. Barrierefreiheit der eigenen UI
- [ ] Vollständig per Tastatur bedienbar, sichtbarer Fokus
- [ ] Mit Windows-Sprachausgabe (Narrator) getestet: alle Controls benannt
- [ ] Windows-Kontrastdesigns unterstützt
- [ ] Einstellungsfenster wird **nicht** aus Aufnahmen ausgeschlossen (normale App)

### 5. Lokalisierung
- [ ] Alle Texte in `.resw`, Deutsch und Englisch

## Akzeptanzkriterien
- [ ] Jede Einstellung aus M2–M6 in der UI änderbar und sofort wirksam
- [ ] Defekte `settings.json` → App startet mit Standardwerten und meldet es
- [ ] Narrator-Test bestanden, Tastaturtest bestanden
- [ ] Profilwechsel per Hotkey < 1 Frame sichtbar

## Offene Fragen / Risiken
- Tray-Bibliothek für WinUI 3 (z. B. H.NotifyIcon) – Lizenz prüfen (MIT-kompatibel).
- WinUI-3-Startzeit unpackaged messen; Ziel: Tray in < 1 s nach Login.
