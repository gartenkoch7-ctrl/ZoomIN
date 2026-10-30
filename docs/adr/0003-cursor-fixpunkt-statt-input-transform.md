# ADR-0003: Cursor-Fixpunkt im MVP, Input-Transform später

- **Status:** Angenommen
- **Datum:** 2026-10-30

## Kontext
Das Overlay ist durchklickbar; Klicks landen am echten Mauspunkt. `MagSetInputTransform`
könnte Eingaben umrechnen, verlangt aber UIAccess, eine signierte EXE und Installation in
einem geschützten Ordner.

## Entscheidung
Im MVP wird immer um den Mauszeiger als Fixpunkt vergrößert (`angezeigt(p) = p`). Der Punkt
unter der Maus stimmt dadurch immer, Klicks treffen ohne Sonderrechte.

## Konsequenzen
- ✅ Kein Code-Signing und kein Installer für den Start nötig.
- ⚠️ Tracking-Modi wie „Kanten-Push“ oder „Maus zentriert“ sind erst mit Input-Transform möglich.
- Wird ersetzt, sobald Installer + Signatur (z. B. SignPath für OSS) existieren.
