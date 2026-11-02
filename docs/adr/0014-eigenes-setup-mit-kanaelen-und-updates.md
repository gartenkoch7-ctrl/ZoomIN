# ADR-0014: Eigenes Setup-Programm mit Kanälen und Ein-Klick-Updates

- **Status:** Angenommen (ersetzt ADR-0009, ergänzt ADR-0012)
- **Datum:** 2026-11-02

## Kontext
Das Ton-/Netzwerk-/Bluetooth-Menü liegt nur dann unter dem Zoom, wenn ZoomIN mit UIAccess läuft
(ADR-0012): signierte EXE in `Program Files`. Das selbst ausgestellte Zertifikat gilt nur auf dem
PC, auf dem es angelegt wurde – auf jedem anderen Rechner fehlte UIAccess wieder. Gleichzeitig
fehlten ein barrierefreier Installer, Updates aus der App und ein Kanal für develop-Stände.

## Entscheidung
- **Eigenes Setup-Programm** `ZoomIN.Setup` (C#/WinUI 3) als kleines **Online-Setup**: holt die
  Versionsliste von GitHub Releases und lädt die gewählte Version. Inno Setup entfällt.
- **Barrierefrei:** Tastatur, Screenreader (NVDA, JAWS, Sprachausgabe), Kontrastdesigns,
  Windows-Textgröße und eine eigene stufenlose Vergrößerung des Setups.
- **Immer `Program Files` + UIAccess.** Die Oberfläche läuft unerhöht; nur ein kurzer
  Hilfsprozess kopiert mit Admin-Rechten. Einstellungen landen so beim angemeldeten Benutzer.
- **Kanäle:** *Stabil* = Releases ohne Vorabkennung; *Experimentell* = RCs und
  `x.y.z-dev.N` (jeder Push auf `develop` erzeugt eine Vorabversion). Downgrade erlaubt,
  Einstellungen und Profile werden vorher gesichert.
- **Updates:** Hinweis in der App, ein Klick lädt, prüft und installiert – mit einer
  Admin-Abfrage pro Update. Kein Dienst, keine geplante Aufgabe. Update-Prüfung beim Start und
  täglich, im Setup vorausgewählt und abwählbar (bisher Opt-in, M7).
- **Signatur:** SignPath für OSS signiert Setup, Releases und develop-Builds in CI. Jeder
  Download wird vor der Installation geprüft (Authenticode `Valid` + SHA-256 aus dem Release).
- **Portables ZIP bleibt** als Variante ohne Admin-Rechte und ohne UIAccess.

## Verworfen
- **Inno Setup erweitern:** Oberfläche zu begrenzt, Downloads/Versionslisten mühsam.
- **MSIX/WinGet:** UIAccess kaum möglich, keine freie Versionsauswahl.
- **Update-Dienst mit Systemrechten:** bequemer, aber viel mehr Code und Angriffsfläche.
- **Eigenes Zertifikat per Setup als vertrauenswürdig importieren:** Sicherheitsrisiko für Nutzer.

## Konsequenzen
- M9 kommt **vor** dem ersten Release; 1.0 erscheint direkt mit dem neuen Setup.
- Bis SignPath genehmigt ist, wird mit dem lokalen Zertifikat gebaut und getestet;
  öffentliche Releases erst mit SignPath.
- Alte Inno-Installationen (`%LocalAppData%\Programs\ZoomIN`) werden vom Setup erkannt und entfernt.
