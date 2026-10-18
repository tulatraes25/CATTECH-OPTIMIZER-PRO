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

## C2-C0 — preparacion y confirmacion coordinada antes de UX
- ReportSourceReviewCoordinator comprueba un caso de cliente guardado y unico, su hash serializado y una fuente del catalogo estrictamente unica. Prepara un token de revision local y efimero (5 minutos) sin datos personales visibles.
- Al confirmar, exige consentimiento expreso, revisa nuevamente el caso y la fuente, y delega la escritura a la bitacora C2-B4 ligada a la huella examinada. Cambio de caso, duplicidad, expiracion o alteracion del origen bloquean la escritura.
- Se mantiene privado de Infrastructure y desconectado de WPF/exportador hasta implementar C2-C UI y C2-D con validacion de bytes durante la generacion. Las pruebas son sinteticas.

## C2-C1 — interfaz WPF para revisión expresa de diagnostico
- La vista de informes ofrece verificar el caso y diagnóstico seleccionados, muestra un resumen legible y un prefijo de SHA-256, pide alias técnico local y casilla de confirmación desmarcada por defecto.
- El ViewModel invalida instantáneamente el token en cambios de cliente, diagnóstico, carga nueva o selección A-B-A. Respuestas asincrónicas tardías no reviven el consentimiento.
- La escritura revalida nuevamente el caso y la fuente mediante el coordinador. Se confirma solamente el registro auditado; el bloqueo de informes combinados sigue activo hasta C2-D y QA físico de WPF.


## C2-D0 — negative export authorization regression
- A recorded or revoked journal claim alone cannot authorize inclusion of independent diagnostics in a named-client HTML/PDF report. The eventual export pathway needs a detached, current, on-disk verified snapshot, not a flag or matching GUID.
- Synthetic regression covers a reviewed claim and an explicitly revoked claim while confirming the renderer still rejects mixed attribution before writing any report file. Source evidence remains byte-identical.
- C2-D activation must re-open and revalidate source bytes, currently effective revocations and selected service case at use time; the renderer, PDF and history index must use the same trusted snapshot and fail closed on changes. This stage does not enable combined exports.


## C2-C2 — cancellation on customer / source / consent changes
- WPF passes a per-operation CancellationToken into both PrepareAsync and ConfirmAsync; switching case, diagnostic, inclusion scope, reviewer alias during confirmation, or withdrawing consent cancels pending storage work and invalidates the UI attempt epoch. Abandoned callbacks cannot display success.
- A cooperative fake verifies cancellation before commit for four adversarial mid-await changes. No report export permission is granted.
- Residual risk: cancellation is cooperative, and a user event arriving after atomic File.Move may not undo an already published journal receipt. The eventual C2-D must always inspect current binding/revocation at use time; real WPF QA remains mandatory.


## C2-C3 — coordinator-issued single-use review ticket
- The coordinator issues a fresh ephemeral ReviewTicket only after a saved-case and unique-source proof. Confirmation must supply the exact in-memory preview instance issued by that coordinator, with explicit consent; a field-identical copy, fabricated preview, stale generation, or replay is rejected.
- Each new PrepareAsync invalidates the prior issued ticket, even when the new preparation fails. A true confirmation consumes the ticket atomically before storage awaits; failed/stale confirmations require another preparation.
- Synthetic tests cover forged/copied/replayed previews, newer preview replacing older, and competing simultaneous confirmations. This prevents inadvertent replays; it is not external user authentication, physical hardware attestation, or an export authorization.


## C2-C4 — prompt cancellation of cross-process journal waits
- Source-binding creation and revocation now pass the user-review CancellationToken through the shared cross-process file-lease helper. A canceled review interrupts its retry delay instead of waiting up to 15 seconds for another process; existing report-store callers continue using the optional uncancelled default.
- The journal also checks cancellation immediately before publishing a staged claim/revocation file without overwrite. Synthetic tests hold the marker in an independent FileStream and cancel both creation/revocation attempts: no delayed receipt, no private raw evidence mutation, no orphan temp file.
- Cancellation remains cooperative; an event already atomically published before a later user change cannot be undone by canceling a token. Revocations and fresh source verification at C2-D export-time remain mandatory.


## C2-D1 — same-handle detached diagnostic capture (pre-export)
- ReportSourceFingerprintReader.CaptureDiagnosticAsync deserializes a diagnostic from the SAME already path-verified Windows file handle and bounded byte buffer used to calculate source ID and SHA-256. An independently reloaded DTO must not be treated as equivalent to the examined bytes.
- The diagnostic capture rejects case-insensitive duplicate JSON property names at any depth, malformed model values and ID mismatches; the input byte buffer is cleared after use. Only the detached model and its internal fingerprint survive the read, never raw JSON or client-identifying paths in the report UI.
- Synthetic tests check replacement between reads, nested duplicate fields, invalid typed payloads and unsafe relative paths. No customer report is rendered, and the existing named-client fail-closed policy is unchanged.
- Important: the detached model remains a mutable in-memory object, not a signed or lasting authorization. C2-D2 must bind the EXACT saved case and active non-revoked audit receipt to an immutable staged export with revalidation near HTML/PDF/index publication; physical Windows QA remains required.

## C2-D2 — comprobación conjunta del caso, diagnóstico y bitácora (solo lectura)
- DiagnosticAttributionPreflight compara el caso seleccionado con el caso realmente guardado y su hash, busca el ID del diagnóstico en todo el catálogo acotado (sin seleccionar por fecha ni serial), verifica la bitácora vigente no revocada, captura los bytes originales mediante C2-D1, y repite las verificaciones de bitácora y caso antes de devolver el resultado.
- Fallan sin permiso las selecciones de otro cliente, IDs ambiguos, diagnóstico reemplazado, recibo inexistente o revocado, bitácora inaccesible y ServiceCaseId explícito ajeno dentro del diagnóstico.
- El resultado SnapshotForFurtherValidation es interno y efímero, y NO constituye autorización para generar HTML, PDF, recomendaciones ni entrada del índice. Las capturas deserializadas todavía pueden modificarse en memoria. No se cambió la prohibición actual de mezclar cliente + fuente independiente.
- C2-D3 deberá crear un snapshot inmutable de salida y conservar una transacción de auditoría/validación hasta publicar los archivos; exigir pruebas de carreras con revocación y selección A-B-A, coherencia de todos los formatos e integración con WPF. QA física Windows sigue pendiente.


## C2-D2b — snapshot privado frente a mutaciones de DTO
- DiagnosticAttributionCandidate deja de exponer referencias directas a ServiceReport y DiagnosticReport mutables: guarda serializaciones privadas obtenidas del caso persistido y del diagnóstico capturado mediante C2-D1. ReadDetachedModels genera instancias nuevas y separadas para un eventual consumidor interno.
- Se publican internamente solo BindingId, ServiceCaseId, ID de fuente y huellas SHA-256; no se expone la ruta privada de archivo ni un campo IsAuthorized. Las pruebas mutan tanto la selección original como la primera copia y comprueban que ninguna altere una captura posterior.
- La serialización privada puede contener datos personales y no debe registrarse, enviarse, guardarse en reportes o considerarse evidencia de identidad física. C2-D3 todavía debe crear la transacción de salida y revalidar asociación y revocación al publicar; HTML/PDF mixtos permanecen bloqueados.


## C2-D3a — prepublicación HTML con rechazo limpio
- HtmlReportService incorpora una comprobación interna opcional al final de la escritura privada en staging y antes de mover el archivo HTML al nombre definitivo. Cancelación, validación negativa o excepción dejan sin publicación y limpian el archivo temporal. El servicio público mantiene la misma funcionalidad por defecto.
- La protección P0 de cliente más diagnóstico independiente se evalúa ANTES de crear directorios o invocar la comprobación; ni siquiera un callback true puede saltarla. Pruebas sintéticas cubren aceptación, rechazo, cancelación y bypass intentado.
- Este punto de control no bloquea por sí solo una revocación que ocurra después de validar y antes del Move: la siguiente etapa debe mantener un candado de bitácora de origen y snapshots completos durante la transacción, y coordinar HTML/PDF/recomendaciones/índice. No se habilitó todavía ningún reporte mixto.


## C2-D3b — exclusive journal read lease (pre-export, fail-closed)
- InspectCurrentAsync and the new internal ExecuteWhileCurrentAsync share exactly the same persisted receipt, revocation inventory and fresh source-fingerprint validation. The exclusive, non-creating FileStream marker remains held while a trusted callback executes; CATTECH review/revoke writers cannot interleave a change during this scope.
- A before/after scan and cancellation checkpoints reject changed source files, corrupt revocations and failed operations; tests model concurrent revocation, unknown/foreign/revoked cases, external data mutation and cancellation. This is a cooperative, bounded internal primitive, not a general-purpose transaction manager.
- Constraints: an administrator modifying files outside CATTECH can still race with checks; arbitrary callbacks may have side effects and must not treat a successful status as permission to publish. C2-D3c must retain authenticated case/source snapshots and journal protection through actual HTML/PDF/recommendations/index publication. Existing P0 named-client mixed-source guard remains enabled.


## C2-D3c — staged publication under one audited marker (not enabled)
- An internal IJournalStagedPublication contract separates private staging from a single atomic PublishAsync. SourceBindingReviewJournal.StageAndPublishWhileCurrentAsync acquires the same exclusive marker for validation, staging, a SECOND complete journal/source check and publication, then disposes the staged resource on every exit.
- Rejected/revoked receipts cannot even stage output; changed bytes or user cancellation after staging must dispose staging without publication. Tests cover valid staging and both failure modes without generating client reports.
- This is an internal primitive, NOT permission to export a named-client diagnostic. Real HTML/PDF/recommendations/index form a multi-file transaction, which File.Move cannot make atomic. A dedicated manifest/recovery and output-scoped source/case validation are still required; no public guard or release gate was lifted.


## C2-D3d — real HTML staging adapter for the exclusive audit journal
- HtmlReportService.StageHtmlForPublicationAsync writes the already-rendered HTML exclusively into a unique private .tmp, returns a disposable StagedHtmlReport whose PublishAsync atomically moves it without overwriting, and removes staging on cancellation/rejection. Public GenerateHtmlReportAsync now reuses exactly this private stage + publish path.
- Synthetic integration tests exercise StageAndPublishWhileCurrentAsync with the genuine HTML stage for client-free output. If source evidence changes between staging and verification, no visible HTML and no leftover temp file remain; normal client-free output is published once.
- The current ReportSourceCasePolicy guard is applied BEFORE directory creation in StageHtmlForPublicationAsync, including to exact-equal case GUID attempts. Neither this method nor a journal receipt allows a named-client diagnostic. PDF conversion, recommendations, report index, immutable multi-output manifest and a real WPF export workflow remain blocked pending C2-D4 / #92 validation.


## C2-D4a — cliente-free output bundle + atomic directory visibility (internal)
- ReportBundlePublicationStore establishes a new, isolated `reports/bundles` format with private `.pending-<guid>` staging, fixed `report.html` and optional `report.pdf`, a strict bounded `manifest.json` containing only schema version, bundle UUID, ClientFree scope, length and SHA-256. No client names, case IDs, source paths or serials are accepted as metadata.
- The single `Directory.Move` from pending to `bundle-<guid>` on the same filesystem is the logical visibility point for future BUNDLE-AWARE readers. InspectAsync is read-only, ignores abandoned `.pending` directories, rejects unknown/duplicate manifest keys, extra artifacts, reparse points, truncated/modified files and hash mismatches. Data buffers are copied before hashing/staging; HTML <=8MiB, PDF <=64MiB, manifest <=4KiB.
- Synthetic adversarial tests: HTML-only and HTML+PDF success, post-publication tampering, extra files, malformed manifests, orphaned pending artifacts and pre-cancellation/invalid inputs. No Edge, WPF, customer identity or hardware interaction.
- Caveats: legacy report history still reads `reports-info.json` and is NOT wired to this bundle store. No automatic orphan cleanup or claim of power-loss durability; out-of-process administrators may race directory checks. The bundle is NOT an authenticated source/case attestation, a release gate, or a completed multi-output export. C2-D4b must connect staged HTML/PDF/index to an audited and recoverable reader, with explicit rollback semantics and Windows physical QA.


## C2-D4b — HTML renderer bridged to client-free bundle
- HtmlReportService.GenerateClientFreeBundleAsync calls the existing synchronous HTML renderer and publishes the resulting bytes through ReportBundlePublicationStore, yielding a single checksum-verified client-free bundle. The public GenerateHtmlReportAsync/ExportPdf commands and legacy reports-info.json remain untouched.
- A checked IncludeClient, even without a ServiceReport, or a hidden ServiceReport reference while IncludeClient=false is rejected before creating any report directory. The bridge never carries a case identifier, reviewer claim or physical attestation. Cancellation is checked before rendering and before publication.
- Windows synthetic tests exercise the real diagnosis HTML renderer, bundle manifest verification, filename/metadata isolation, both client-reference guards and pre-cancellation. Real PDF, index migration and Windows WPF QA remain outside this slice.
