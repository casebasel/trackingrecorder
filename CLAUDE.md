# TrackingRecorder

Windows-App (WPF, .NET 8) für Kamera-Tracking von VIVE Mars mit Timecode; Werkzeuge in `werkzeuge/` (Python).
Plan und Stand: `ROADMAP.md`. Befunde: `docs/`.

- **UI:** Design System „Filmstudio Stage“, Familie Datenzentrale — vor UI-Arbeit `project/README.md` und
  `project/tokens.json` aus https://claude.ai/artifact/DLVnnCHU46neKXpxK6adYK lesen. Abweichungen in `DESIGN.md`.
- **Absprachen mit anderen Stage-Apps** an die Session `stage-system` oder mit ihr in Kopie (Systemkarte casebasel/stage-system).
- **Mars gehört der Stage:** Einstellungen am Mars nur nach Absprache mit der Stage.
- **Veröffentlichen:** Version im App-csproj erhöhen, dann `werkzeuge/release.sh` (Velopack, GitHub-Releases).
