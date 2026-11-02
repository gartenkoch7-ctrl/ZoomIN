# UI-Neugestaltung & Mauszeiger-Seite

**Ziel:** Einstellungen im Stil der Windows-11-Einstellungen und alle Zeiger-Optionen auf einer
eigenen Seite.
**Voraussetzungen:** M5 (Zeiger), M7 (Einstellungsfenster).

## Entscheidungen (Grilling 2026-10-30)
- **Kartenstil:** Symbol, Titel und Beschreibung links, Steuerelement rechts; Schieberegler
  zeigen Wert und Einheit daneben, der Screenreader hört denselben Text.
- **Seiten:** Übersicht · Zoom & Modi · Farbfilter · **Mauszeiger** · Fokus & Textcursor ·
  Tastenkürzel · Profile · Allgemein · Info. „Farben & Zeiger“ ist aufgeteilt.
- **Übersicht** (Startseite): großer Zoom-Schalter mit −/+ und Stufe, Modus-Kacheln,
  Schnellschalter (Filter, Fadenkreuz, Fokus folgen, Suchhilfe), aktives Profil, Status.
- **Mauszeiger:** Live-Vorschau (hell/dunkel, ohne Zoom und bei 3×) und „Suchhilfe testen“.
  - Größe ohne Zoom 1–4× (Standard 1× = Windows-Zeiger unverändert; darüber zeichnet ZoomIN)
    und maximale Größe beim Zoomen.
  - *Nachtrag 2026-11-01:* „Windows-Zeigereinstellungen überschreiben“ (Standard aus): ZoomIN
    zeichnet immer – auch ohne Zoom – den scharfen Windows-Standardzeiger in eigener Größe und
    Farbe. „Größe relativ zum Zoom“ (Standard an): Größe ohne Zoom × Zoomfaktor bis zur
    maximalen Größe; aus = feste Größe. Details: KB eingabe-und-tracking.
  - Farbe: Original · Einfärben (helle Flächen, Umrandung bleibt) · Invertiert; Voreinstellungen
    Gelb, Grün, Cyan, Magenta, Weiß, Schwarz oder freier Farbwähler. Kontrastrand an/aus,
    Farbe und Breite.
  - Suchhilfe: Farbe, Größe, Dauer; Auslöser einzeln schaltbar – Strg+Alt+S (an), Schütteln
    (an), nach > 3 s Ruhe (an), Strg allein (aus).
  - Fadenkreuz: Standard aus, „nur beim Zoomen“ oder „immer“; Farbe, Breite, Deckkraft;
    Strg+Alt+K (im Kürzel-Editor änderbar).
  - Zeiger beim Tippen ausblenden: Standard aus.

## Umsetzung
- **Core/Pointer:** `PointerStyle` (ersetzt `CursorStyle`), `RgbColor`, `PointerColorMode`,
  `LocatorTriggers`, `ShakeDetector`, `IdleDetector`, `PointerPreview` (CPU-Vorschau nach
  denselben Regeln wie `Cursor.hlsl`), `IInputActivitySource`/`IInputActivityHandler`.
  `EffectsModel` bekommt `Signaled` für kurzlebige Ereignisse (Suchhilfe, Tippen) – diese
  wecken die Renderer, lösen aber kein Speichern aus.
- **Platform:** `Win32InputActivityHook` (Maus- und Tastatur-Hook, nur beobachten).
- **Rendering:** `Cursor.hlsl` färbt/invertiert; Fadenkreuz als zwei gefüllte Rechtecke;
  Suchhilfe mit Farbe/Größe/Dauer. Ohne Zoom und Filter: transparentes Overlay ohne
  Desktop-Kopie, nur wenn Zeiger > 1×, Farbmodus ≠ Original, Fadenkreuz „immer“ oder
  beim Tippen ausgeblendet.
- **App:** `SettingCard`, `ValueSlider`, `ColorChooser` (Views/Controls), neue Seiten
  `OverviewPage`, `FilterPage`, `PointerPage`, alle übrigen Seiten im Kartenstil;
  Texte der neuen Seiten in `TextsGermanCards`/`TextsEnglishCards`.

## Offene Fragen / Risiken
- Sichttest offen: Vorschau, Kartenlayout, Farbwähler, Fadenkreuz, Schütteln, Strg allein,
  Tippen-Ausblenden.
- Mit Zeiger > 1× oder Fadenkreuz „immer“ rendert das Overlay dauerhaft (etwas CPU/GPU, nur
  wenn eingeschaltet). Bei mehreren Monitoren laufen dann alle Renderer – später auf den
  Monitor mit Maus beschränken.
