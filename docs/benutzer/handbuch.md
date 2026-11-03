# ZoomIN – Benutzerhandbuch

## Installation
- **Installer** (`ZoomIN-…-setup.exe`): installiert für Ihr Benutzerkonto, ohne Admin-Rechte,
  mit Startmenü-Eintrag. Optional „Mit Windows starten“.
- **ZIP**: Entpacken, `ZoomIN.exe` starten. Nichts wird installiert.
- Voraussetzung: Windows 10 Version 2004 oder neuer, 64 Bit.
- Beim ersten Start führt ein **Assistent** in fünf Schritten durch das Wichtigste.

ZoomIN läuft danach im **Infobereich** (neben der Uhr, ggf. unter „^“):
- **Klick** auf das Lupen-Symbol: Einstellungen
- **Rechtsklick**: Zoom an/aus, Modus, nächstes Profil, **ZoomIN beenden**
- Das X des Einstellungsfensters beendet ZoomIN nicht, es läuft im Infobereich weiter.

## Zoomen
| Kürzel | Aktion |
|---|---|
| Strg+Alt+Z | Zoom an/aus (im zuletzt benutzten Modus) |
| Strg+Mausrad | stufenlos vergrößern/verkleinern; bis 1× zurück = aus |
| Strg+Alt+Bild↑ / Bild↓ | in Schritten vergrößern/verkleinern |

**Hinweis:** Strg+Mausrad gehört ZoomIN. Browser und Office zoomen damit nicht mehr selbst –
unter „Tastenkürzel“ können Sie das ändern.

## Modi
| Kürzel | Modus |
|---|---|
| Strg+Alt+F | **Vollbild** – der ganze Monitor ist vergrößert |
| Strg+Alt+L | **Lupe** – ein Rechteck um den Mauszeiger; Größe mit Strg+Alt+Umschalt+Pfeiltasten |
| Strg+Alt+D | **Angedockt** – ein Streifen (Standard: oben, 40 %) zeigt die Umgebung des Zeigers; andere Fenster rücken in den Rest |

Dasselbe Kürzel noch einmal schaltet den Zoom aus. Jeder Modus merkt sich seine eigene Stufe,
und standardmäßig hat jeder Monitor seine eigene Stufe.

**Klicken im Zoom:** Im Vollbild und in der Lupe klicken Sie einfach auf das, was Sie sehen.
Im angedockten Streifen kann man nicht klicken – er ist nur Anzeige.

## Farben und Mauszeiger
| Kürzel | Aktion |
|---|---|
| Strg+Alt+C | Farbfilter an/aus (wirkt auch ohne Zoom) |
| Strg+Alt+Umschalt+C | nächster Filter |
| Strg+Alt+S | Suchhilfe: ein Ring zieht sich auf den Mauszeiger zusammen |
| Strg+Alt+K | Fadenkreuz an/aus |

Filter: Invertiert · Invertiert mit Farbton · Graustufen · Graustufen invertiert ·
Gelb auf Schwarz · Schwarz auf Gelb · Weiß auf Blau. Kontrast und Helligkeit stellen Sie unter
**Farbfilter** ein, die Glättung unter **Zoom & Modi**.

Mit „Eigener Farbfilter je Monitor“ (standardmäßig an) merkt sich jeder Monitor, ob sein Filter
an ist und welcher; die Kürzel wirken dann auf den Monitor unter der Maus. Auf der Seite
**Farbfilter** wählen Sie den Monitor in einer Liste aus; „Standard“ gilt für alle Monitore ohne
eigenen Filter. Kontrast und Helligkeit gelten immer für alle Monitore.

Auf der Seite **Mauszeiger** (mit Live-Vorschau) stellen Sie ein: „Windows-Zeigereinstellungen
überschreiben“ (ZoomIN zeichnet dann immer den Windows-Standardzeiger in eigener Größe und Farbe,
Zeigergröße und -farbe aus den Windows-Einstellungen gelten nicht), Größe ohne Zoom (1–4×),
„Größe relativ zum Zoom“ (an: Größe × Zoomfaktor bis zur maximalen Größe; aus: der Zeiger bleibt
gleich groß) und die maximale Größe beim Zoomen, Farbe (Original, Einfärben, Invertiert), Kontrastrand mit Farbe und
Breite, die Suchhilfe (Farbe, Größe, Dauer; auslösen per Kürzel, Schütteln der Maus, nach
3 Sekunden Pause oder mit Strg allein), das Fadenkreuz (nur beim Zoomen oder immer) und
„Zeiger beim Tippen ausblenden“. Die Startseite **Übersicht** bündelt Zoom an/aus, Modus,
Zoomstufe, Schnellschalter, Profil und Status.

## Tastaturfokus und Textcursor
Drücken Sie Tab, springt der Zoom zum fokussierten Element (magenta umrahmt). Beim Tippen folgt
er dem Textcursor, sobald dieser in die Nähe des Rands kommt. Bewegen Sie die Maus, gleitet der
Zoom zurück zur Maus. **Strg+Alt+T** schaltet das Folgen an und aus.

## Profile
Ein Profil speichert die Darstellung (Modus, Stufen, Filter, Zeiger, Fokus-Verfolgung).
Tastenkürzel gelten für alle Profile. **Strg+Alt+P** wechselt zum nächsten Profil. Profile
lassen sich exportieren und auf einem anderen Rechner importieren.

## Bildschirmfreigabe
Teilen Sie Ihren Bildschirm in Teams, Zoom, Discord oder im Browser, sehen andere den normalen
Desktop – nur Sie sehen den Zoom. Ausnahme: Geräte, die das Bildsignal direkt abgreifen
(Beamer im Spiegelmodus, Capture-Karte), zeigen den Zoom.

## Wenn etwas nicht klappt
- **Kürzel reagiert nicht:** Ein anderes Programm belegt es – unter „Tastenkürzel“ ändern.
- **ZoomIN ist abgestürzt:** Es startet automatisch neu (höchstens dreimal in fünf Minuten).
- **Log-Dateien:** Einstellungen → Info & Status → Log-Ordner öffnen.
- **Einstellungen zurücksetzen:** ZoomIN beenden, `%AppData%\ZoomIN\settings.json` löschen.
