# ZoomIN – Benutzerhandbuch

## Installation
- **Setup** (`ZoomIN-Setup.exe` aus dem GitHub-Release): Ein Assistent führt in fünf Schritten durch
  die Installation – Sprache, Kanal und Version, Einstellungen, Installation, Fertig. Er lädt die
  gewählte Version herunter, prüft Prüfsumme und Signatur und installiert sie nach
  `C:\Program Files\ZoomIN`. Dafür fragt Windows einmal nach Administratorrechten.
- Setup und ZIP sind **nicht signiert** (kein gekauftes Zertifikat): Windows SmartScreen warnt beim
  ersten Start – „Weitere Informationen“ → „Trotzdem ausführen“. Das Setup signiert ZoomIN dann auf
  Ihrem PC mit einem eigenen Zertifikat und löscht dessen Schlüssel sofort wieder.
- Nur die installierte Fassung darf **über anderen Fenstern** wie dem Startmenü, dem Lautstärke- oder
  Netzwerk-Menü vergrößern, und nur sie läuft auf Anmelde- und Sperrbildschirm.
- Einstellungen im Assistenten (alle vorausgewählt): **Mit Windows starten**, **Auch auf Anmelde- und
  Sperrbildschirm**, Verknüpfung auf dem Desktop, regelmäßig nach Updates suchen.
- Eine ältere Installation (auch der frühere Installer) wird dabei ersetzt; Einstellungen bleiben.
- **ZIP**: Entpacken, `ZoomIN.exe` starten. Nichts wird installiert. Diese Fassung vergrößert nicht
  über Startmenü und Systemmenüs und zeigt dazu einen Hinweis mit Knopf, der das Setup startet.
- Voraussetzung: Windows 10 Version 2004 oder neuer, 64 Bit.
- Beim ersten Start führt ein **Assistent** in fünf Schritten durch das Wichtigste (entfällt, wenn
  das Setup schon alles eingerichtet hat).

## Updates
- **Einstellungen → Allgemein**, Abschnitt **Updates**, zeigt die installierte Version und die verfügbaren Versionen.
- **Kanal:** „Stabil“ zeigt nur fertige Versionen. „Experimentell“ zeigt zusätzlich
  Release-Kandidaten und Entwicklerversionen – ungetestet, sie können Fehler haben.
- **Aktualisieren** oder **Zurückgehen** auf eine ältere Version: ein Klick, dann die Windows-Abfrage
  bestätigen. ZoomIN startet danach neu. Vor dem Zurückgehen werden Einstellungen und Profile gesichert.
- Passt ein Download nicht zur Prüfsumme des Releases oder trägt er eine fremde Signatur, wird er verworfen und nichts geändert.
- Mit „regelmäßig nach Updates suchen“ prüft ZoomIN beim Start und einmal täglich und meldet eine neue
  Version im Infobereich.

## Deinstallation
- **Windows-Einstellungen → Apps → Installierte Apps → ZoomIN → Deinstallieren.**
- ZoomIN fragt, ob Ihre Einstellungen und Profile bleiben sollen. Danach fragt Windows nach
  Administratorrechten.
- Entfernt werden das Programm, Startmenü- und Desktop-Verknüpfung, der automatische Start (für alle
  Benutzerkonten) und die Anmeldung beim Anmelde- und Sperrbildschirm.

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
| Strg+Alt+C | Farbfilter an/aus (wirkt auch ohne Zoom); bei „Invertiert (intelligent)“ siehe unten |
| Strg+Alt+Umschalt+C | nächster Filter |
| Strg+Alt+S | Suchhilfe: ein Ring zieht sich auf den Mauszeiger zusammen |
| Strg+Alt+K | Fadenkreuz an/aus |

Filter: Invertiert · Invertiert mit Farbton · Graustufen · Graustufen invertiert ·
Gelb auf Schwarz · Schwarz auf Gelb · Weiß auf Blau. Kontrast und Helligkeit stellen Sie unter
**Farbfilter** ein, die Glättung unter **Zoom & Modi**.

**Bei „Invertiert (intelligent)“** schaltet Strg+Alt+C nicht den ganzen Bildschirm, sondern nur das
aktive Fenster (sonst das Fenster unter der Maus): erst Originalfarben, beim nächsten Druck wieder
intelligent. ZoomIN merkt sich das für diese App (Seite **Filter pro Fenster**). **Zweimal schnell
drücken** schaltet den Farbfilter des Monitors ganz an oder aus. Das gilt, solange „Filter pro
Fenster“ an ist; sonst schaltet das Kürzel wie bei jedem Filter den ganzen Monitor.

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
lassen sich unter **Einstellungen → Allgemein** anlegen, exportieren und auf einem anderen Rechner
importieren.

## Anmelde- und Sperrbildschirm
- Mit „Auch auf Anmelde- und Sperrbildschirm“ (Einstellungen → Allgemein) vergrößert ZoomIN auch die
  Anmeldung, die Sperre (Win+L), Strg+Alt+Entf und Windows-Abfragen nach Administratorrechten – mit
  Ihrer Zoomstufe und Ihrem Farbfilter.
- Dort fehlen Inseln, Einfrieren und Monitorwechsel. **Strg+Alt+Umschalt+Z** schaltet ZoomIN dort bis
  zum nächsten Bildschirmwechsel aus.
- Den Schalter umzustellen fragt nach Administratorrechten.

## Bildschirmfreigabe
Teilen Sie Ihren Bildschirm in Teams, Zoom, Discord oder im Browser, sehen andere den normalen
Desktop – nur Sie sehen den Zoom. Ausnahme: Geräte, die das Bildsignal direkt abgreifen
(Beamer im Spiegelmodus, Capture-Karte), zeigen den Zoom.

## Wenn etwas nicht klappt
- **Kürzel reagiert nicht:** Ein anderes Programm belegt es – unter „Tastenkürzel“ ändern.
- **ZoomIN ist abgestürzt:** Es startet automatisch neu (höchstens dreimal in fünf Minuten).
- **Log-Dateien:** Einstellungen → Info & Status → Log-Ordner öffnen.
- **Einstellungen zurücksetzen:** ZoomIN beenden, `%AppData%\ZoomIN\settings.json` löschen.
