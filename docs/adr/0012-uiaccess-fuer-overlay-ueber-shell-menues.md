# ADR-0012: UIAccess-Build, damit der Zoom über Shell-Menüs liegt

- **Status:** Angenommen (ergänzt ADR-0009)
- **Datum:** 2026-10-30

## Kontext
Fehlerbericht: Öffnet man das Ton-/Netzwerk-Menü (Schnelleinstellungen), erscheint das
**Original unvergrößert und nicht invertiert** an seiner normalen Stelle; beim Zoomen wird nur
eine Kopie vergrößert, das Original bleibt stehen. Ursache (siehe `docs/kb/capture-ausschluss.md`,
„Z-Ebenen“): Windows legt Shell-Fenster wie Schnelleinstellungen, Info-Center und Startmenü in
eine eigene **Z-Ebene (Band)** über allen normalen Topmost-Fenstern. Unser Overlay kommt dort mit
keinem Fensterstil hin. Desktop Duplication nimmt das Menü aber mit auf → Kopie im Zoom, Original
darüber.

## Entscheidung
- Ein **zweites Manifest** `src/ZoomIN.App/app.uiaccess.manifest` mit
  `requestedExecutionLevel level="asInvoker" uiAccess="true"`. Ein Topmost-Fenster eines
  UIAccess-Prozesses landet in der UIAccess-Ebene über diesen Shell-Menüs (wie bei der
  Windows-Bildschirmlupe und der Bildschirmtastatur). Am Overlay-Code ändert sich nichts.
- Aktiv nur mit der Build-Eigenschaft `ZoomInUiAccess=true` (`publish.ps1 -UiAccess`), weil
  Windows eine UIAccess-EXE **nur** startet, wenn sie
  1. mit einem Zertifikat signiert ist, dem der PC vertraut, und
  2. in einem geschützten Ordner liegt (`C:\Program Files\…`).
  Entwickler-Builds (`dotnet run`) und das portable ZIP bleiben ohne UIAccess.
- **Signatur vorerst selbst ausgestellt:** `scripts/setup-signing.ps1` (einmalig als Administrator)
  erzeugt „CN=ZoomIN Local Code Signing“ und vertraut ihm auf diesem PC. Für die Verteilung an
  andere bleibt ein echtes Zertifikat (z. B. SignPath für OSS) das Ziel.
- **Installer** mit `/DUiAccess`: `PrivilegesRequired=admin`, Installation nach `Program Files`,
  Start nach der Installation per `shellexec runasoriginaluser` (nie erhöht).
- **Prozessstarts über die Shell:** Wächter-Start und Neustart nach Absturz nutzen
  `UseShellExecute = true`, denn `CreateProcess` verweigert UIAccess-EXEs
  (`ERROR_ELEVATION_REQUIRED`). Für den normalen Build ändert das nichts.

## Konsequenzen
- Der UIAccess-Installer braucht einmal Admin-Rechte (Abweichung von ADR-0009 nur für diese Variante).
- Ohne vertrautes Zertifikat startet der UIAccess-Build nicht („Vom Server wurde eine Verweisung
  zurückgegeben“) – darum bricht `publish.ps1 -UiAccess` ab, wenn die Signatur nicht `Valid` ist.
- Build-Falle: Das Windows App SDK führt Manifeste in `obj\…\Manifests\app.manifest` nur neu
  zusammen, wenn die Ausgabe älter ist als die Eingaben. Beim Wechsel zwischen den Varianten blieb
  daher das alte Manifest stehen. Das Target `ZoomInRefreshMergedManifest` merkt sich die Quelle
  und erzwingt beim Wechsel eine neue Zusammenführung.
- Spätere Input-Transform-Funktionen (`MagSetInputTransform`, eingabe-und-tracking.md) setzen
  ebenfalls UIAccess voraus und können auf diesem Build aufbauen.
