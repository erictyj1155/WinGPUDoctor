# Synthetic examples

`report.example.json` and `report.example.md` use schema 0.2.0 and wholly synthetic GPU/path facts. The active path comes from the synthetic topology test fixture through the actual topology collector transformation; system, video-controller and signed-driver facts come from a synthetic fixture that uses the production per-field provenance and collector order. Both pass through the production privacy boundary and exporters. They illustrate two inventory adapters and one active path matched to the second adapter. They are format examples, not measurements from a real machine.

After an intentional fixture or writer change, regenerate them with `./scripts/update-examples.ps1` (PowerShell 7, after a Release build) instead of editing them by hand. The deterministic tests check that the tracked files match the generated output and that the fixture's provenance matches the supervised production path.

Real smoke-check reports are kept only in ignored `artifacts/`. Never replace these examples with a machine dump.
