# Report schema 0.1.0

The C# model is in `src/WinGPUDoctor.Core/Model.cs`. The export contract is [report-0.1.0.schema.json](../schemas/report-0.1.0.schema.json), JSON Schema draft 2020-12. Examples in `examples/` are synthetic. M1 does not offer arbitrary report import; schema validation is not a sanitization mechanism.

## Envelope

| Field | Meaning |
|---|---|
| `schemaVersion` | Exact schema contract version; currently `0.1.0` |
| `toolVersion` | Diagnostic tool version; independent of Windows and schema versions |
| `collectedOnUtc` | UTC calendar date only; not a precise timestamp |
| `facts` | Source observations: system, GPU inventory, reserved display inventory |
| `findings` | Derived rule results: stable rule ID, severity, message, evidence paths |
| `warnings` | Stable warning codes; explanation catalog in `ReportWriter.WarningText` |
| `collection` | Per-source completion status and safe reason code; no exception strings |
| `privacy` | Policy version and number of removed fields |

“Raw collected facts” means directly observed, allowlisted facts with normalization documented; it never means a raw WMI dump. Internal temporary join identifiers are outside this model. Normalizations include PCI hex extraction, provider date to ISO calendar date, safe trimming, and privacy removal. Findings are separate interpretations, never replacements for facts.

## Observation contract

```json
{"state":"available","value":"32.0.15.1234","source":"wmiSignedDriver","reason":"none"}
```

```json
{"state":"unknown","value":null,"source":"wmiSignedDriver","reason":"noMatchingDriver"}
```

| State | Contract |
|---|---|
| `available` | Non-null observed value and reason `none`; an empty array is a successful empty inventory |
| `unknown` | Query succeeded but a value could not be established; null plus specific reason |
| `unsupported` | Feature/API not implemented or supported in this context; null plus reason |
| `failed` | Attempted collection failed; null plus safe reason |
| `redacted` | Privacy policy removed an observed value; null plus `sensitiveValue` |

Null alone has no meaning; always inspect `state`, `source`, and `reason`. Sources are fixed enum values, not provider-controlled strings. A GPU's provider/version/date each has its own observation. PCI IDs are type codes, not unique device identities. Non-PCI inventory entries remain visible with `nonPciDevice` for those fields. Driver matches never fall back to a different adapter by name/order.

`collection.status` is `succeeded`, `partial`, `failed`, or `unsupported`. A successful query with missing required values is partial; the source-level reason summarizes missing data, while field reasons retain specifics such as `ambiguousDriver`. A failed inventory is not represented as an available empty array. Classification and display collection are explicitly unsupported in M1. Driver collection is omitted from metadata if no GPUs were returned or GPU collection failed, because it was not attempted.

## Findings, warnings, and completeness

M1 rules are `inventory.multiple-adapters` and `inventory.empty`, with severity `information` and evidence `facts.gpus`. Neither determines health, actual rendering, routing, power, or hybrid mode. No findings does not mean the system is healthy. Warnings separately state collection limitations, source limitations, privacy review, and redaction.

Both outputs come from the same privacy-projected snapshot. Markdown displays every implemented fact with provenance and unavailable state/reason, then findings, warnings, collection outcomes, and privacy counts. JSON exposes the same information structurally. Enum naming is camelCase in JSON; Markdown displays enum labels for readability.

## Extension and compatibility

`DisplayFacts` and rational `DisplayMode` reserve room for the next spike, but M1 strips incoming display payloads and exports `unsupported`. The future native model will likely need separate source/target mode semantics and path relationships; refine/version that portion after evidence rather than claiming it is complete now. No native LUID, monitor path, or serial belongs in the public schema.

Increment the schema version when the contract changes; pre-1.0 readers must not assume unknown fields or enum additions are safe. Schema forbids undeclared fields. Future ZIP export should package the same `report.json`, `report.md`, and a minimal versioned manifest with relative allowlisted filenames. ZIP is a transport wrapper, not a new collection model or license to include raw logs.
