# ADR 0006: Optional monitor friendly-name absence is non-blocking metadata

Status: accepted, 2026-09-13. Supersedes only the missing-friendly-name consequence in ADR 0005; the DisplayConfig interop, exact SetupAPI correlation, privacy boundary, and CLI export model remain unchanged.

Context: Windows can successfully complete the target-name query while leaving the optional monitor friendly name empty. M2 retained that outcome as `unknown/missingValue` but treated the diagnostic as a partial display collection, which also produced `collectionIncomplete` and CLI exit `3`. That conflated optional metadata absence with substantive collection failure.

Decision: a `targetName/missingValue` issue remains explicit in collection metadata, but it does not independently make the display run partial or incomplete. The display collector still records the name as `unknown/null/missingValue`, preserves all path and correlation facts, and does not invent a fallback name. Any target-name API failure, source-name failure, invalid mode, unavailable target, adapter-correlation failure, provider failure, or retry exhaustion remains substantive and incomplete.

Consequences: `CollectionIssue.BlocksCompletion()` is the single policy point for distinguishing this non-blocking metadata issue from failures. `CollectionIssue` history remains visible in reports, while `CollectorRun`, `CollectionIncomplete`, and CLI exit status stay driven by the corrected run result. Schema 0.2.0 and the privacy projection remain structurally unchanged.
