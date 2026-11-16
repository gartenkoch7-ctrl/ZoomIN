# ADR-0017: Hinweis mit Setup-Knopf statt Selbstsignierung, wenn UIAccess fehlt

- **Status:** Angenommen (ergänzt ADR-0012 und ADR-0014)
- **Datum:** 2026-11-03

## Kontext
Auf einem zweiten PC lief ein selbst gebauter develop-Stand (dev.104). Schnelleinstellungen (Ton,
Netzwerk, Bluetooth) blieben unvergrößert über dem Zoom – derselbe Fehler wie in ADR-0012. Ursache:
Ohne UIAccess liegt das Overlay unter diesen Menüs. UIAccess gibt Windows nur einer EXE, die mit
einem auf dem PC vertrauten Zertifikat signiert ist und in `Program Files` liegt. Ein selbst
gebauter Stand, `dotnet run` oder das portable ZIP erfüllen das nie. Wunsch des Nutzers: Niemand
soll dafür Skripte ausführen müssen; die App soll das beim Start erkennen und beheben.

Ganz still beheben geht nicht: Für Program Files und Zertifikate braucht es immer eine
Admin-Abfrage (UAC).

## Entscheidung
- Beim Start liest ZoomIN `TokenUIAccess` des eigenen Prozesses (`Win32UiAccessProbe`). Fehlt es:
  - einmal pro Start eine Tray-Meldung,
  - ein Banner oben auf der Übersicht, solange es fehlt (nicht wegklickbar),
  - Knopf „Signierte Version installieren“: lädt `ZoomIN-Setup.exe` des neuesten Releases im
    eingestellten Kanal, prüft es gegen `checksums.txt` und den angehefteten ZoomIN-Signierer
    (`TrustedPublishers`) und startet es. Das Setup fragt selbst nach Admin-Rechten.
- Gilt auch für Entwickler-Builds: Wer das Setup installiert, hat danach die signierte Version.
- **Kein** eigenes Zertifikat auf fremden PCs.

## Verworfen
- **Selbstreparatur per Klick** (Zertifikat auf dem PC erzeugen, in `LocalMachine\Root`
  eintragen, sich selbst signieren, installieren): verändert den Zertifikatsspeicher fremder PCs.
  Ein Stammzertifikat, dessen Schlüssel je auf dem PC lag, ist ein Angriffsweg; Virenscanner und
  die SignPath-Regeln („keine versteckten Systemänderungen“) stoßen sich daran.
- **Nur Release-Seite öffnen**: der Nutzer müsste selbst laden und prüfen.

## Folgen
- Bis SignPath genehmigt ist, gibt es kein Release mit einem Setup, dem fremde PCs vertrauen. Der
  Knopf meldet dann „noch keine Version mit Setup“ bzw. „noch nicht öffentlich signiert“. Er
  funktioniert ohne Codeänderung, sobald CI signierte Releases mit `ZoomIN-Setup.exe` veröffentlicht
  und der SignPath-Name in `TrustedPublishers` steht.
- Auf dem Entwickler-PC bleibt der Weg aus ADR-0012 (`setup-signing.ps1`, `publish.ps1 -UiAccess`,
  `install-uiaccess.ps1`).
