# Glossar

| Begriff | Bedeutung |
|---|---|
| **Zoomfaktor** | Vergrößerungsstufe, z. B. 2,0 = doppelt so groß. Intern als `double`. |
| **Viewport** | Ausschnitt des Quell-Desktops (in physischen Pixeln), der vergrößert angezeigt wird. |
| **Fixpunkt** | Punkt, der beim Zoomen an derselben Bildschirmstelle bleibt (bei uns: der Mauszeiger). |
| **Vollbild-Modus** | Vergrößerung füllt den ganzen Monitor. |
| **Lupe / Lens** | Rechteckiges Fenster um den Zeiger, das den Bereich darunter vergrößert. |
| **Angedockt / Docked** | Fester Teil des Monitors zeigt die Vergrößerung, Rest bleibt normal. |
| **Tracking** | Welcher Quelle der Viewport folgt: Maus, Tastaturfokus, Textcursor (Caret). |
| **Caret** | Blinkender Textcursor in Eingabefeldern. |
| **Overlay** | Unser randloses, durchklickbares Topmost-Fenster, in das wir den Zoom zeichnen. |
| **Capture-Ausschluss** | `WDA_EXCLUDEFROMCAPTURE`: Fenster ist in Aufnahmen/Übertragungen unsichtbar. |
| **Desktop Duplication** | DXGI-API, die den Monitorinhalt als GPU-Textur liefert. |
| **Swapchain (Flip Model)** | Puffer-Kette für die Ausgabe; Flip Model = geringe Latenz, kein Kopieren. |
| **VSync** | Ausgabe synchron zur Bildwiederholrate des Monitors, verhindert Tearing. |
| **Hot Path / Render-Loop** | Code, der jeden Frame läuft; dort gelten strengste Regeln. |
| **Physische vs. logische Pixel** | Physisch = echte Monitorpixel; logisch = DPI-skaliert (z. B. 150 %). |
| **UIA** | UI Automation, Windows-API für Fokus-, Caret- und Elementinformationen. |
| **UIAccess** | Manifest-Recht für Hilfstechnologien; nötig für Input-Transform, verlangt Signatur. |
| **Clean-Room** | Nachbau nur aus beobachtbarem Verhalten, ohne fremden Code anzusehen. |
| **SuperNova** | Kommerzielle Lupen-/Screenreader-Software von Dolphin Computer Access; unser Vorbild. |
