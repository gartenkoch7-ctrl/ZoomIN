# ADR-0031: Lokales Signieren bei der Installation statt gekauftem Zertifikat

- **Status:** Angenommen (ersetzt in ADR-0014 den Punkt „Signatur“)
- **Datum:** 2026-11-17

## Kontext
UIAccess (ADR-0012) verlangt eine `ZoomIN.exe` in `Program Files`, deren Signatur Windows auf diesem
PC vertraut. ADR-0014 sah dafür SignPath (OSS) vor; der Antrag kann Wochen dauern und braucht einen
ersten Release. Ein gekauftes Zertifikat (Certum, OV-Anbieter) kommt für den Nutzer nicht infrage
(Entscheidung 2026-11-17: „das werde ich nicht für Geld machen“). Azure Artifact Signing ist für
Privatpersonen in Deutschland nicht verfügbar.

## Entscheidung (Nutzer, 2026-11-17: „Setup signiert lokal“)
- **Releases sind unsigniert.** Die Pipeline veröffentlicht `ZoomIN-Setup.exe`, das portable ZIP,
  das UIAccess-Paket und `checksums.txt` auch ohne SignPath.
- **Echtheit über die Prüfsumme:** Setup und App laden `checksums.txt` aus demselben GitHub-Release
  (HTTPS) und laden ein Paket nur, wenn sein SHA-256 passt (wie bisher). **Der erhöhte Schritt holt
  `checksums.txt` dann selbst** von der festen Adresse `…/releases/download/v<Version>/checksums.txt`
  (`ReleaseFeeds.Checksums`, `GitHubReleaseChecksums`, Host und Pfad geprüft) und installiert
  **jedes** Paket nur, wenn es dazu passt – auch eines mit schon signierter `ZoomIN.exe`; erst dann
  wird eine unsignierte `ZoomIN.exe` signiert. Die Prüfsumme auf seiner Befehlszeile stammt vom
  unerhöhten Aufrufer und beweist nichts (Sicherheitsreviews 2026-11-17: sonst könnte Schadcode als
  normaler Benutzer `ZoomIN.exe --apply-update` mit eigenem Paket starten und es signieren lassen,
  oder eine schon lokal signierte `ZoomIN.exe` neben eigene DLLs legen). Versionen bestehen nur aus
  SemVer-Zeichen, damit sie weder Adresse noch Ordnerpfad umlenken. Eine **ungültige oder fremde**
  Signatur wird abgelehnt. Fehlt die Prüfsumme (offline, kein Release), endet der Schritt mit
  „fehlgeschlagen“. Folge: Installieren geht nur online und nur veröffentlichte Releases.
  Der Abruf (`MachineTrustHttpClient`) akzeptiert nur Serverzertifikate, deren Wurzel in
  `LocalMachine\Root` oder `LocalMachine\AuthRoot` liegt: Der erhöhte Prozess läuft als derselbe
  Benutzer, der ohne Adminrechte eine eigene Wurzel in `CurrentUser\Root` und einen Proxy setzen
  könnte (Review 2026-11-17).
- **Nur die Installation in `Program Files\ZoomIN`:** Die erhöhten Schritte (Update, Entfernen)
  arbeiten nur, wenn die laufende `ZoomIN.exe` dort liegt (`ProcessRunningInstall`). Eine Kopie in
  einem beschreibbaren Ordner könnte sonst ihr entpacktes `ZoomIN.exe` zwischen Prüfung und
  Signatur austauschen lassen.
- **Signiert wird im erhöhten Installationsschritt, auf dem PC des Nutzers** (`LocalSigner`):
  1. Neues selbst ausgestelltes Zertifikat „CN=ZoomIN Local Signing“, RSA 3072, nur
     Codesignatur (EKU), **kein CA** (BasicConstraints CA=false) – es kann keine anderen
     Zertifikate ausstellen. Der Schlüssel wird als CNG-Maschinenschlüssel **direkt mit eigener
     Zugriffsliste** erzeugt (geschützt, nur SYSTEM und Administratoren, nicht exportierbar), nie
     per PFX importiert: Ein importierter Schlüssel erbt die Standardrechte des Anbieters, die das
     unerhöhte Benutzerkonto einschließen können. Gemessen im Admin-Integrationstest
     `LocalSignerAdminTests` (2026-11-17): `D:P(A;;0xd01f01ff;;;SY)(A;;0xd01f01ff;;;BA)`.
  2. Öffentlicher Teil nach `LocalMachine\Root` und `LocalMachine\TrustedPublisher`.
  3. `ZoomIN.exe` im Staging-Ordner unter `Program Files` signieren (SHA-256).
  4. **Privaten Schlüssel sofort löschen** – auf jedem Weg, auch bei Fehlern; lässt er sich nicht
     löschen, gilt die Installation als fehlgeschlagen und das Zertifikat wird nicht vertraut. Reste eines
     abgebrochenen Schritts löscht der nächste. Danach kann niemand mehr mit diesem Zertifikat
     signieren; die vorhandene Signatur bleibt gültig. Scheitert das Signieren, wird das neue
     Zertifikat wieder aus `Root`/`TrustedPublisher` entfernt.
  PowerShell läuft dabei mit festem `PSModulePath` (nur Windows-Module), damit kein vom Benutzer
  abgelegtes Modul mit Adminrechten lädt.
  Jedes Update erzeugt so ein neues Zertifikat. **Nach** dem Umschalten bleiben nur die Zertifikate,
  mit denen die verbliebenen Versionsordner signiert sind; die Deinstallation entfernt alle.
  „Schon aktiv“ gilt nur, wenn die laufende `ZoomIN.exe` noch gültig signiert ist; sonst (Ordner
  nach Deinstallation liegen geblieben, Zertifikat von Hand entfernt) wird dieselbe Version neu
  installiert und signiert.
- **Setup-Knopf in der ZIP-Fassung:** startet das heruntergeladene Setup, wenn die Prüfsumme passt,
  auch ohne Signatur.
- **SmartScreen** warnt beim ersten Start des unsignierten Setups und des ZIP („Weitere
  Informationen“ → „Trotzdem ausführen“) – steht in Anleitung, README und Release-Notes.
- Ein späteres öffentliches Zertifikat (SignPath o. Ä.) bleibt möglich: Ein gültig von
  `TrustedPublishers` signiertes Paket wird dann ohne lokales Signieren installiert.

## Verworfen
- **Gekauftes Zertifikat:** Kosten (Nutzerentscheidung).
- **Skript statt Setup:** kein Assistent, keine Updates aus der App, für sehbehinderte Nutzer umständlich.
- **Nur ZIP ohne UIAccess:** kein Zoom über Startmenü, Schnelleinstellungen und Sperrbildschirm.
- **Dauerhafter lokaler Signierschlüssel:** Ein Schlüssel, dessen Zertifikat in `Root` liegt, wäre ein
  Angriffsziel; gelöscht nach dem Signieren gibt es nichts zu stehlen.

## Konsequenzen
- ✅ Kostenlos; UIAccess, Sperrbildschirm und Updates funktionieren wie geplant.
- ⚠️ Vertrauen hängt an GitHub (HTTPS + Prüfsumme im selben Release), nicht an einer Signatur des
  Herausgebers. Wer das Repo oder den Release übernimmt, kann ein Paket samt Prüfsumme tauschen.
- ⚠️ ZoomIN legt bei jeder Installation ein Zertifikat in den Stammzertifizierungsstellen ab
  (ohne privaten Schlüssel, ohne CA-Recht). Sicherheitsprüfer und manche Virenscanner fragen danach.
- ⚠️ SmartScreen-Warnung beim Download, bis ein öffentliches Zertifikat da ist. Die UAC-Abfrage des
  Setups zeigt „Unbekannter Herausgeber“; die des installierten ZoomIN zeigt „ZoomIN Local Signing“.
- ⚠️ Das Setup-Programm selbst prüft nur der Nutzerkontext (Prüfsumme vor dem Start); zwischen
  Prüfung und Start könnte es getauscht werden. Das erhöhte Paket schützt die eigene Prüfsumme.
- ⚠️ Kein Installieren ohne Internet; lokale Testbuilds laufen über `install-uiaccess.ps1` mit dem
  Entwicklerzertifikat.
- ⚠️ Restrisiken des erhöhten Schritts (Sicherheitsreview 2026-11-17, bewusst offen):
  - Er erbt die Umgebung des Benutzers. Startup-Hooks sind im Release abgeschaltet
    (`StartupHookSupport=false`), CoreCLR-Profiler-Variablen (`CORECLR_ENABLE_PROFILING`,
    `CORECLR_PROFILER_PATH_64`) lassen sich so nicht abschalten – Schadcode mit Benutzerrechten könnte
    darüber eine DLL in den Schritt laden, wenn der Nutzer die UAC-Abfrage bestätigt. Abhilfe später:
    Start über einen Weg mit sauberer Umgebung (z. B. geplante Aufgabe oder nativer Starter).
  - Das Single-File-Setup entpackt sich (`IncludeAllContentForSelfExtract`) in einen vom Benutzer
    beschreibbaren Ordner; ein vorab abgelegter Ordner würde wiederverwendet.
  - Zurückgehen auf jede ältere **veröffentlichte** Version ist möglich (gewollt, ADR-0014), auch auf
    eine mit später behobenem Fehler.
