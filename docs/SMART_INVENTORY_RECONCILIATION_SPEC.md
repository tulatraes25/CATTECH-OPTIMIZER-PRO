# SPEC Kiro — SMART Inventory Reconciliation (read-only preview)

Issue: #30. Branch `feat/smart-inventory-reconciliation-20261015`, stacked after the previously CI-certified Evidence Engine PR #35, SHA `7ba6616013da07dbc2e48e11a0c430f4e944661d`. **Not ready for merge or release.**

## GOAL
Use the existing `SmartDiskReport` data model to identify *candidate* correspondences between two SMART inventories from the same service case, while refusing silent rebinding of a different, replaced or ambiguous disk. The output must never authorize a diagnostic comparison automatically.

## REQUIREMENTS
1. `SmartInventorySnapshot.Capture(DiagnosticContextId, capturedAt, SmartDiskReport[])` takes an immutable in-memory snapshot (maximum 128 reports) of private SMART identity descriptors, not health measurements. The inventory must have an explicit case context.
2. A trustworthy *candidate* requires `IsAnalysisSuccessful`, a nonblank device identifier (presence check only), unique non-placeholder serial, nonblank model and smartctl transport/protocol, positive capacity. Matching uses normalized `serial + model + capacity + transport`, **never disk enumeration index or device path**.
3. `Preview(baseline, current, baselineSubjects, evaluationAt)` must reject different cases, inverted/future timelines, duplicate/unscoped previous subject IDs and wrong count. Duplicated serials in either inventory are ambiguous **even if one row has a failed or incomplete SMART analysis but still claims that serial**; changed capacity/model/transport yields `ChangedIdentity`; missing identity `UnverifiableIdentity`; unmatched serial `NewDevice`.
4. Even an exact match produces only `CandidateRequiresReview` with an opaque previous subject ID (nonserializable) and the current row index. It **does not bind the current observation, grant consent, generate evidence or classify a disk as repaired**.
5. A report or identity mutation after capture must not alter the previously reviewed snapshot. Comparison results and default JSON serialization omit device paths, serials, models and case/subject IDs.
6. Existing hardware service, `SmartDiskService`, `smartctl`, WMI and persisted SMART reports remain untouched. The new component has no direct I/O or subprocess execution.

## NON-GOALS
No cryptographic device identity, inventory attestation, automatic proof of a physical disk, hardware discovery at runtime, automatic disk-to-volume mapping, automatic repair validation or cloud export. A smartctl serial can be copied/spoofed, device firmware can lie, bus bridges can hide or change identifiers, and report errors can be incomplete.

## DESIGN
Use existing `Core.Models.Smart.SmartDiskReport` as input. Store only private normalized identity strings in snapshot memory. Public preview status is a limited enum; keep source identity invisible. Require **a later technician review in a trusted case/subject UI** before reusing a prior `DiagnosticSubjectId`. A review UI must display safe labels without disclosing serial or path through general logs or passports.

## RISKS
- An exact serial/model/capacity/transport match can still represent copied or swapped devices: never bypass confirmation.
- An absent/duplicate serial, failed analysis or old persisted report with incomplete fields is not eligible for automatic matching.
- In-memory private identifiers remain sensitive and could still be observed by a debugger, memory dump or malicious in-process code; JSON omission is not encryption.
- `SmartDiskReport` is mutable and older saved reports may lack fields; snapshot once and fail closed.
- A unique identifier provided by a caller for an unrelated disk is not validated by this preview. The future binding workflow must be tied to a trusted service case and current inventory selection; revoke authorization on equipment/session change.
- Users may replace storage mid-session; recapture after hardware change and recheck even prior approved identities.
- CPU/GPU/RAM inventory is outside this SMART-specific slice; needs separate policies.

## ACCEPTANCE
- Changed `Device` path but exact private identity => candidate, not approval.
- Reordered inventory matches prior candidate by identity, never by position.
- Replaced disk on same path, changed model/size/type, missing/placeholder serial, duplicate serial, failed analysis => no approved match.
- Cross-case and invalid chronology, duplicate previous subjects => hard refusal.
- JSON emits neither raw serial/path/model nor previous subject IDs; in-memory capture immune to source mutation.
- GitHub Windows CI at exact HEAD: `dotnet restore`, Release build, full xUnit suite 0 failed/0 skipped, diff reviewed. Interactive Windows QA and actual physical mapping **remain pending**.

## DISPOSITION
Implement/read-only testing only. **Keep #30 open**: real hardware binding, human-reviewed candidate acceptance, identity revocation, report consent and WPF UX all remain before production use. No merging `main` or publishing releases.

## Hardening de entradas de firmware — slice posterior

El firmware SMART puede devolver texto con caracteres de control, marcas
Unicode de dirección/zero-width o identificadores uniformes de relleno (ej.
`FFFFFFFFFFFF`). En comparaciones entre snapshots esos valores no deben
producir candidatos. Se considera `UnverifiableIdentity` toda identidad
que contenga caracteres de control/sustitutos/formato en serial/modelo/
transporte, y todo serial de cuatro o más caracteres idénticos.

Este criterio es deliberadamente conservador: podría rechazar un serial
real inusual (falso negativo), pero nunca autoriza un emparejamiento.
No usar normalizaciones visuales/confusables para forzar equivalencias.
No implica validación de identidad física. QA con informes de distintos
fabricantes y bridges sigue pendiente.

## Revision de candidatos vinculada a capturas exactas

Un `CandidateRequiresReview` del inventario anterior no sirve como prueba
de correspondencia despues de capturar nuevamente el equipo, aunque los
campos serial/modelo/capacidad/transporte sean iguales. Cada captura en
memoria dispone de un token aleatorio independiente del hardware; el
`SmartInventoryMatchPreview` recuerda, sin serializarlos, los tokens de
ambos snapshots y la posicion original de la identidad de sujeto.

`IsCurrentCandidateFor(baseline,current,baselineSubjects,currentIndex)`
solo devuelve `true` si es un candidato del mismo par de capturas, con
el mismo indice actual seleccionado y la misma asignacion de sujeto en
la posicion del inventario base. Las listas del llamador se copian al
hacer `Preview` para evitar resultados inconsistentes por mutaciones
posteriores. El token nunca figura en JSON exportado.

**Limites**: este chequeo es una precondicion de frescura de vista previa,
no un acto de consentimiento ni una prueba de identidad fisica.
Codigo malicioso u otros consumidores pueden ignorar el metodo, por lo
que cualquier futuro adaptador que transforme candidatos en evidencias
debera imponer nuevamente estos controles y confirmar el hardware
en la interfaz con el tecnico. El plan de revocacion al cambiar el
equipo sigue pendiente de QA WPF/Windows real.

## Validacion pre-trim y tamanos de firmware (slice SMART-H3)

La validacion sobre texto de firmware se realiza sobre la **cadena cruda**
antes de aplicar `Trim`: un serial como `"SN-123\\n"` nunca debe
convertirse silenciosamente en el mismo candidato que `"SN-123"`.
Tambien se rechazan controles/formato Unicode en el nombre de modelo, la
ruta logica del dispositivo y el tipo explicito devuelto por smartctl,
aunque el codigo pudiera sustituir ese tipo por el campo `Protocol`.

Todo valor crudo de identidad queda limitado a 1024 caracteres, y el
normalizado usado para coincidencia, a 256; los espacios ASCII normales en
los extremos todavia se pueden recortar cuando no hay control ni formato.
No se eliminan marcas peligrosas para fabricar equivalencias. Si un
parseador externo agrega CRLF al dato, debe corregirse en la capa de
adquisicion confiable y volver a capturar, no en la comparacion SMART.

Estos checks minimizan falsos positivos pero tambien pueden aumentar los
falsos negativos, lo cual es intencional cuando no hay prueba de identidad
fisica. La funcionalidad sigue siendo solo vista previa y no ejecuta
operaciones ni comparte datos reales.
