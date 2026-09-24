# Report schema 0.2.0

The model is in `src/WinGPUDoctor.Core/Model.cs`; the export contract is [report-0.2.0.schema.json](../schemas/report-0.2.0.schema.json), draft 2020-12. Schema 0.1.0 is retained as the M1 historical contract. M2 changes the previously reserved display shape and adds collection diagnostics; readers must check the version. M1 observation semantics and test cases are preserved. There is no arbitrary report import feature, and schema validation is not sanitization.

## Envelope and observations

`schemaVersion`, `toolVersion`, and `collectedOnUtc` describe format/tool/date. `facts` holds source observations; `findings` holds derived rules and evidence paths; `warnings` uses stable codes; `collection` records source completion; `privacy` records policy version and removed-field count. No raw provider object or correlation identifier is part of this envelope.

Each observation has `state`, `value`, `source`, and `reason`:

| State | Meaning |
|---|---|
| `available` | Non-null value and reason `none`; an empty array is a successfully empty inventory |
| `unknown` | Value not established; null plus reason such as missing name or unmatched identity |
| `unsupported` | Feature/API unsupported or deliberately unimplemented; null plus reason |
| `failed` | Attempted collection failed; null plus a safe reason |
| `redacted` | Privacy policy removed the value; null plus `sensitiveValue` |

A failed inventory is never an available empty list. Numeric values remain directly reported facts, with calendar-date/PCI normalization documented in the collectors. No uncertain field receives a guessed substitute. M5 adds four informational topology interpretations to the two conservative M1 inventory rules without a mode or health verdict.

## M5 interpreted findings

Rules run on the privacy-projected facts used by both exporters. The existing inventory rules retain their predicates, messages, severity and evidence. They appear before M5 topology findings. An empty findings array means only that no configured rule fired; it is not an all-clear or evidence-completeness claim.

| ID | Predicate for available topology | Meaning |
|---|---|---|
| `topology.endpoint-adapter-association` | Active path; both source and target associations are exact | Names the two projected GPU labels through the existing identity correlation; neither label identifies an application rendering GPU |
| `topology.correlation-unresolved` | Active path; at least one endpoint association is unavailable | Preserves each endpoint independently, including any exact label and the unavailable state/source/reason; it does not assert that no GPU exists |
| `topology.active-path-target-unavailable` | Active path; `targetAvailable` is false | Reports that flag without assigning a cable, monitor, port, GPU or driver cause |
| `topology.no-active-paths` | Available display observation containing zero entries | Describes this report's available empty active-path collection, not the raw native record count or absence of display hardware |

All four use `information`. For each valid active path, exactly one association/unresolved finding appears, then the target-unavailable finding if applicable. Paths follow projected array order. An available empty collection produces one no-active-paths finding; a nonavailable whole-topology observation produces none. Partial collector status does not suppress usable path facts. Finding severity does not change collection status, warnings or CLI exits.

M5 evidence is limited to `facts.displays` for the empty observation, or these exact fields for zero-based projected array position `i`:

```text
facts.displays.value[i].id
facts.displays.value[i].pathActive
facts.displays.value[i].sourceAdapter
facts.displays.value[i].targetAdapter
facts.displays.value[i].targetAvailable
```

Association and unresolved findings list `id`, `pathActive`, `sourceAdapter`, `targetAdapter` in that order. Target-unavailable lists `id`, `pathActive`, `targetAvailable`. Adapter evidence points to the complete observation object, so unavailable state/source/reason remain visible even when its value is null. Evidence paths use projected positions and neutral labels only; tests resolve them against the same exported report. The schema checks their `facts.` prefix but does not itself prove resolution.

The ID denotes the rule kind, so repeated IDs across paths are expected. The evidence index supplies report-local instance context, not stable cross-run identity. JSON and Markdown use the same findings in the same order; Markdown escapes finding-provided text for display. Neither format establishes rendering, workload, power, graphics mode, electrical wiring, health or driver correctness from a topology association.

## Active display path model

`facts.displays` is an observation containing one entry per returned active path, not a flat inventory of all physical monitors.

| Field | Meaning |
|---|---|
| `id` | Per-report path label (`display-1`, etc.) |
| `sourceId`, `targetId` | Neutral endpoint labels; shared source IDs preserve clone relationships |
| `sourceAdapterId`, `targetAdapterId` | Per-report CCD adapter keys; can differ; not GPU inventory indexes |
| `sourceAdapter`, `targetAdapter` | Independent observations of an exact inventory match or unavailable state |
| `sourceGdiName` | Dedicated allowlisted GDI alias, such as `\\.\DISPLAY1`; not a panel-location inference |
| `name` | Filtered monitor friendly name; missing names remain unknown |
| `outputTechnology` | Connector enum label from CCD; an unrecognized enum stays unknown |
| `sourceResolution` | Source mode width/height in pixels; not a claim about native panel or signal dimensions |
| `pathRefreshRate` | Exact numerator/denominator from the path; meaning accompanied by query mode |
| `signalRefreshRate` | Exact target-signal vertical-sync numerator/denominator, kept separately |
| `rotation`, `scanLineOrdering` | Target enum observations; unknown values remain explicit |
| `pathActive`, `targetAvailable` | Separate reported flags; an active path can transiently have an unavailable target |
| `refreshRateBoost` | Windows 11 reported boost flag; unsupported when not queried with virtual-refresh awareness |
| `cloneGroupId` | Neutral clone-group fallback when reported and source mode is absent; otherwise unknown, not a “no cloning” verdict |
| `queryMode` | `activePaths`, `virtualModeAware`, or `virtualModeAndRefreshAware` |

An available adapter match contains a regenerated `gpuId`, `evidence: exactSetupApiInstanceId`, and `confidence: exact`. This means one complete case-insensitive SetupAPI/WMI instance-ID equality match. It does not certify actual rendering, chronology, physical port wiring, health, or hardware type. Unmatched/ambiguous joins retain their CCD adapter label and never borrow a GPU name by order or PCI type. No raw LUID, native source/target ID, interface path, PnP path, or EDID identifier is exported.

Refresh ratios are not rounded in JSON. Markdown adds a rounded human-readable Hz value and retains the ratio. With Windows 11 virtual-refresh awareness, the path value and physical target signal can differ. These fields do not measure FPS, GPU activity, or instantaneous variable refresh. Scan-line ordering is retained so an interlaced signal is not silently treated as progressive.

## Collection metadata

Each source result has `status`, `reason`, `attempts`, `queryMode`, and `issues`. Each issue contains a fixed `operation`, safe `reason`, and an actual integer `nativeErrorCode` when available; otherwise that optional error metadata is null. No exception strings or device-specific paths are permitted. WMI uses `notQueried` for display query mode.

An insufficient-buffer race is retried at most three times. Recovered races remain recorded while a final complete query can succeed. Exhaustion is `failed/topologyChanged`. Access denial records a session/access limitation; unsupported APIs remain unsupported; generic errors remain generic. Source-name failures, target-name API failures, modes, adapter resolution, and target availability can make a result partial while other path data remains usable. A successful target-name query with an empty optional friendly name keeps the name `unknown/null/missingValue` and a `targetName/missingValue` issue, but does not independently make the run partial or add `collectionIncomplete`; missing optional clone-group data alone is likewise not treated as partial.

## Export and compatibility

Both formats use the same privacy-projected snapshot. The current schema/tool/privacy versions are 0.2.0/0.1.0/0.2. The tool release version is distinct from the report schema version. The schema forbids undeclared fields. Topology keys are regenerated and references remapped before writing, while GDI aliases and connector/rotation/scan-line strings have field-specific allowlists. Preview remains necessary for arbitrary OEM/monitor descriptions.

The separate future report-export ZIP design remains a wrapper for sanitized JSON/Markdown plus a minimal reviewed manifest. No report-export ZIP implementation or raw-log inclusion has been added; the v0.1 application ZIP is a different artifact.

M4 does not change this schema. Production collection now uses the worker supervisor to obtain the existing facts, but the parent reconstructs the prior `CollectionSnapshot` shape before privacy projection. Provider failures, supervisor timeouts, partial/failed runs, attempts, missing optional names, and collection-incomplete warnings remain representable under schema 0.2.0. Integrated fixtures cover WMI/topology timeout, intentional driver omission, zero observed attempts, partial unmatched topology and optional missing names. Worker output bounds become existing `failed/resourceLimit` observations without native-error or QueryPaths diagnostics; that internal failure frame adds no public schema field.

Controlled Ctrl+C stays out-of-band: it produces no report, no `Cancelled` state and no new fixture, and the CLI returns `3` with nothing exported. Calibrated timing budgets are internal policy and add no schema field.
