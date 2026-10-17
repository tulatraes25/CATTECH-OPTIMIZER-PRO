# SPEC Kiro #92 — C2: vínculo auditable de fuentes con caso de servicio

**Estado:** DISEÑO VERIFICABLE / BLOQUEO P0 ABIERTO. No es autorización para publicar.
**Base exacta:** `359d983e257980ea0e382861925b90b8ea9e4bef`. Se inspeccionó el CI Windows #253 (1725/1725), paquete candidato #31 y las implementaciones actuales de `ReportSourceCasePolicy` y `ReportViewModel`.

## GOAL
El técnico debe poder asociar un diagnóstico, inicio, SMART o self-test con un caso concreto **sin falsificar origen ni mezclar computadoras diferentes**. Una coincidencia de GUID es NECESARIA pero nunca suficiente.

## THREAT MODEL
- Dos clientes distintos con discos de igual modelo, serial vacío/duplicado y mismo horario.
- Fuentes `legacy` con `ServiceCaseId=null`, origen desconocido y archivos históricos inmutables.
- Un JSON editado manualmente para copiar un `ServiceCaseId` ajeno.
- Cambios A→B→A en la selección mientras se genera HTML o PDF.
- Archivo renombrado o modificado después de asociarlo.
- Un diagnóstico de disco físico distinto del volumen lógico del cliente.
- Un evento SMART de otra sesión cuya hora o dispositivo coincide por casualidad.
- Una fuente legítima de un equipo compartido que no corresponde a la intervención actual.

## REQUIRED DATA CONTRACT (new versioned sidecar; DO NOT ALTER legacy source payloads)
- `schemaVersion` entero explícito; `bindingId` GUID independiente y no vacío.
- `serviceCaseId`, `sourceKind`, `sourceRecordId`, `sourceContentSha256`, `sourceFileIdentity` (relative canonical path under approved store).
- `confirmedAtUtc`, `confirmedBy` local technician identity, `confirmationMethod` enum `ExplicitTechnicianReview`.
- `sourceObservations`: capture limited factual equipment cues with privacy classification, not an attestation (e.g. disk serial may be N/D); never export raw paths or identifiers by default.
- `rejectedReason`/revocation event in append-only local audit, without arbitrary raw exception text.
- Compatibility: old source and reports remain readable without a binding. Sidecar is opt-in, local and reversible, not written into original historical evidence.

## BOUNDARY RULES
1. `ServiceCaseId` equality ALONE cannot create a binding. An included source requires a currently valid sidecar confirming exact source record/digest, case identity and local technician action.
2. Inspect the unmodified stored source **at export time**; verify digest, canonical path and no reparse/redirected storage traversal before using its data. Refuse stale/missing/tampered source. No save in report-generation path.
3. Bindings from different cases or source kinds are rejected even when serial/model/timestamp match. Never auto-match by heuristics.
4. If source digest changes or file is replaced, previously accepted binding becomes invalid and must be explicitly reviewed again; no automatic repair.
5. The UI displays both associated and unassociated history, but permits inclusion in a named-client report **only** when binding passes. Warning text: `Sin asociar al equipo` / `Fuente modificada: requiere nueva revisión` without private paths.
6. All HTML, PDF, recommendation generation and generated report history receive a **detached validated snapshot**; do not use mutable `Selected*` after an await.
7. The binding grants attribution only to a **service case** and source snapshot, never asserts sensor truth, device ownership or confirmed repair cause.
8. Duplicate/ambiguous sidecars for the same source and case fail closed rather than choose the newest arbitrarily.
9. No binding in generic client-free exports unless an explicitly titled standalone diagnostic scope is used; never inherit stale client context.
10. An association must be revocable without deleting its source. Revocation immediately invalidates generated preview buttons; historical exported PDFs remain immutable and retain provenance explanation.

## DESIGN PHASES
- **C2-A:** pure validator and immutable sidecar DTO + canonical serialization/hash fixtures, null and duplicate rejection, zero filesystem writes (Core).
- **C2-B:** isolated Infrastructure reader/writer in approved local report store, atomic create-no-overwrite, append-only audit; prove redirected/symlink and concurrent write rejection.
- **C2-C:** explicit WPF confirmation surface with readable evidence, source/case selection and consent expiration. No silent default selection.
- **C2-D:** adopt validated detached bindings at ReportViewModel + HtmlReportService boundaries, PDF and recommendations; retest after every await, no private metadata leaks.
- **C2-E:** targeted legacy migration UX (read-only, no guessing), 2-client/2-disk adversarial matrix, Windows hardware/Edge PDF smoke via OpenCode on exact signed-off commit.
Each phase is a small GitHub PR/commit and needs a fresh exact-SHA Windows CI plus candidate package verification. Do not merge a partially trusted C2 chain to main.

## ACCEPTANCE MATRIX (synthetic)
| Case | Expected |
|---|---|
| Case A source A with valid verified binding | include, clearly labeled reviewed association |
| Case A source B with same timestamp/serial | reject |
| Case A source B with forged `ServiceCaseId=A` | reject |
| Case A old source without sidecar | visible as unassociated, not included |
| Source changed after binding / same ID different bytes | reject |
| Duplicate binding files / concurrent acceptance | reject |
| Reparse point path to outside approved store | reject |
| Case A→B→A while reading source | reject old export |
| Access denied/unavailable source | reject attribution; preserve older source |
| PDF, HTML, recommendations and index | exactly same accepted provenance, no private path/PII |
| Binding revoked during export | abort old export, preserve current UI |
| Broken binding audit write | no success claim/no source mutation |

## CURRENT NON-GOALS
No deletion of old records; no network/cloud account, no fake hardware attestation, no automatic binding by serial/hostname, no real cleanup/SFC/DISM/Defender changes; no public release or main merge.

## CURRENT DISPOSITION
C1/C1b established the case ID and generated-report trace. C2-A and C2-B source/journal foundations are implemented and CI-verified in development; C2-C, C2-D and C2-E remain **NOT IMPLEMENTED**. Current `ReportSourceCasePolicy.HasMatchingExplicitCase` is a necessary **not sufficient** condition; its existing passing test proves only equal GUIDs, not provenance. Maintain issue #92 P0 until real binding, negative tests and Windows physical QA pass.

## P0 interim containment — pending C2 audited binding
- A matching mutable GUID no longer grants permission to attribute independent diagnostic, startup, SMART or self-test measurements to a named customer's equipment. HTML, PDF and direct recommendation generation enforce this before attributed output.
- WPF defaults those source sections off when a named client is selected; manual forcing is rejected with a readable, non-private error. Existing local histories remain intact and can be exported as independent client-free reports.
- This is a temporary fail-closed release gate, not C2 completion. C2-B through C2-E must restore safely reviewed, on-disk provenance-verified combinations, with revocation, race tests and physical Windows QA. Never re-enable attribution by comparing GUIDs alone.

## C2-A identity and uniqueness hardening
- The source record ID is the ACTUAL validated string ID from the independent source JSON (including historical 8-character IDs), not an invented GUID. The BindingId and ServiceCaseId remain GUIDs. Comparing a source ID is not attestation.
- Pure preflight rejects terminal control characters in hashes and relative paths, Windows special device names, ambiguous path segments, and duplicate claim identities/records/files across cases. The exact audited claim must be present once, not merely an arbitrary colliding claim.
- All validations operate on untrusted DTOs only. A caller-provided inventory cannot certify complete journal custody. Infrastructure C2-B must independently verify on-disk stored bytes and source identity before granting a reviewed association.

## C2-B1 — isolated, read-only on-disk source fingerprint
- ReportSourceFingerprintReader opens only the four known local JSON source directories: diagnostics, startup-analysis, smart-reports, smart-tests. Unsupported kinds, redirected locations and ambiguous names fail closed.
- Under Windows, read-only file bytes, source JSON ID and SHA-256 come from one open FileStream. A final-path check through the actual Windows handle rejects unexpected target aliases/junctions, while ancestor reparse checks add defense-in-depth. FileShare.Read prevents concurrent writes while this reader holds the stream.
- Input is bounded at 4 MiB, duplicate/invalid top-level IDs fail closed, and private payload buffers are cleared. No source files, sidecars, reports or customer indexes are modified by this reader.
- The returned in-memory fingerprint is **NOT** technician consent, customer linkage, source custody over time, nor device attestation. Later C2-B2 must implement an append-only audited review journal and C2-D must re-open/revalidate at report export time; no named-client mixed sections are enabled by C2-B1.
- Path checks reduce risk but do not claim universal resistance to hostile kernel/file-system substitution, hardlink tricks or post-read file replacement. Windows symlink/junction and real-machine tests remain part of C2-E.

## C2-B2-A — local append-only review receipts (pending consumer integration)
- An internal SourceBindingReviewJournal accepts only an explicitly acknowledged synthetic review request, safe local reviewer alias, nonempty service-case GUID, and a source that has independently passed the C2-B1 fingerprint reader twice.
- Within a cooperating-process exclusive lock, the journal enumerates every prior claim. An unreadable/duplicated/tampered entry fails closed; a source already linked to another case or reused path is rejected.
- A new review claim is staged in an isolated temporary file and moved without overwriting as a standalone immutable-by-CATTECH JSON receipt. Existing diagnosis, SMART and service records are untouched. No success status is returned before readback succeeds.
- This journal is only append-only under normal CATTECH writes; an administrator controlling the local filesystem can tamper with files. There is no signed hardware attestation or authenticated reviewer UI yet.
- C2-B2-B must implement explicit revocation events and verify current provenance/review state, C2-C must implement technician confirmation UX and C2-D must revalidate the exact source file and active review at every export boundary. Until then HTML/PDF/automatic recommendations remain blocked for mixed named-client sources.

## C2-B2-B — append-only revocation receipts
- Revocations write a new dedicated JSON event keyed to the accepted binding ID. CATTECH never edits/deletes the previous claim or raw diagnostic. A second revocation does not create duplicate records.
- The same per-store OS process lease protects review and revocation publication; malformed, duplicated or orphaned revocation files invalidate new source-review writes rather than being ignored.
- This initial stage deliberately does not silently rebind a revoked source to a new case. Future C2-C requires a fresh explicit review/reassignment workflow; C2-D must inspect the CURRENT revocation journal on every export, not merely an old stored GUID.
- Local record files remain under workstation administrator control, so these are auditable-by-application receipts, not tamper-proof external attestations. No named-client mixed output is enabled.

## C2-B2-C — read-only live state and fail-closed revocation custody
- An internal InspectCurrentAsync checks a specific binding against an intact, complete claim and revocation inventory, the requested case identity and a NEW independent on-disk SHA-256 reading. Invalid or orphaned revocation events block reading and further review.
- Inspection opens the existing OS coordination marker with FileMode.Open: it creates no directory, marker, record or source file. Concurrent journal writing yields an explicit unsafe/invalid state, never an optimistic match.
- Matching status is ephemeral ONLY. Export is still prohibited from using it directly: C2-C/D must provide explicit UI consent and re-open the source under its own validated output boundary, including revocations checked again at the point of use.
- New negative tests include synthetic two-customer mismatch, same ID and serial but different source bytes, revocation, corrupted/orphan journal and locked store.

## C2-B3 — catalogo estricto de ID historicos
- ResolveUniqueAsync inspecciona solamente las cuatro familias conocidas de archivos JSON locales, con identificadores reales (incluyendo los de ocho caracteres).
- Dos archivos con el mismo ID producen Ambiguous; cualquier fuente hermana corrupta, inaccesible o redirigida produce Unverifiable. No se selecciona la mas reciente ni se infiere por serial o fecha.
- Busqueda acotada a 4096 archivos y sin escritura. Esta resolucion es temporal y NO autoriza asociar cliente/diagnostico ni exportar; requiere confirmacion C2-C y revalidacion C2-D.

## C2-B4 — confirmacion vinculada al digest mostrado
- RecordReviewedByIdAsync acepta un ID real encontrado de manera univoca y el SHA-256 visto durante la revision del tecnico. Exige confirmacion explicita y alias local validado.
- Si cambia el contenido entre la revision y la confirmacion, se rechaza sin escribir; un segundo barrido dentro del candado de la bitacora comprueba identidad, ruta y unicidad otra vez.
- Las comprobaciones son conservadoras frente a archivos hostiles y no prueban identidad fisica. La interfaz y el renderer siguen sin aceptar fuentes mixtas hasta C2-C/D.
- El catalogo ahora limita la enumeracion antes de materializar el listado de mas de 4096 rutas.
