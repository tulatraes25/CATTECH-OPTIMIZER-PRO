# CATTECH Evidence Engine — Diagnostic Context Isolation (Issue #27)

**SPEC Kiro · 2026-10-15 · Development only**

## GOAL
Prevent combining observations, diagnostic rules, repair outcomes or public passport
statistics from distinct equipment/service sessions. CATTECH must not report a
condition improved on equipment B because a prior observation was recorded on A.

## REQUIREMENTS / CONTRACT
- `DiagnosticContextId`: opaque, randomly generated GUID for **one explicitly selected
  service/diagnostic session**. Use `NewSession()` for a new session.
  `FromGuid(guid)` is for a trusted already-established context; never derive from
  serial, MAC, hostname, username, client name, fingerprint or user-supplied files.
- An evidence producer must bind **every observed or missing record** to that session.
  A record without a context is a *legacy/unbound* record, not a wildcard.
- `DeterministicDiagnosticEvaluator.Evaluate` rejects unbound or mixed evidence;
  for zero records a bound expected context is required, yielding
  `InsufficientEvidence`, not a positive conclusion.
- `RepairOutcomeEvaluator.Compare` returns `Indeterminate/ContextMismatch`
  for different or unbound scopes **before comparing measurement values**.
- `TechnicalPassportBuilder.Build` refuses mixed, unbound and empty evidence;
  every linked assessment and outcome must belong to the same context, even if
  linked evidence GUIDs happen to be reused.
- Technical Passport's in-memory artifact retains context for future UI lifecycle
  checks, but **its public JSON never contains context, serial or raw evidence**.
- An unknown context is never silently inferred from the hostname or hardware.

## RISKS / THREAT MODEL
- A corrupted producer may accidentally reuse a session ID after changing the
  selected PC; GUID alone does not authenticate physical device identity.
- An untrusted or hostile producer can claim the same context incorrectly;
  this Core defense detects simple accidental mixing, not forgery.
- Legacy records lack context. They can still be shown privately as historical
  unverified observations but **must not** be used for rule corroboration, pre/post
  attribution or public Technical Passport export.
- For long-term visits to the *same* PC, a distinct new session must be generated
  unless a separately authorized, reviewed longitudinal design exists. Do not
  silently reuse an old session for a new customer or visit.
- User-facing consent to copy a passport must be invalidated when the active
  customer, equipment, or session changes. The current view is not wired to live
  navigation yet; the UI rebind/invalidation implementation remains pending.
- Versioned public JSON contract `cattech.technical-passport.public.v1` does not
  change; in-process context is not an added public export field. Any future
  persisted evidence schema needs a versioned migration. Do **not** backfill a
  missing context with one synthetic/shared ID.

## DESIGN / TASKS
1. Pure Core `DiagnosticContextId` and optional context on evidence factory
   methods, preserving compilation of legacy API calls as deliberately unbound.
2. Guard evidence combinations in rule evaluation.
3. Guard pre/post comparisons, with explicit context-mismatch reason.
4. Fail closed in passport builder and preserve in-memory scope.
5. Regression fixtures: mixed cases with same source/fact, missing context,
   foreign assessment, foreign outcome, public JSON redaction and no records.
6. Later, implement trusted session lifecycle in a real evidence adapter and
   explicit WPF case-switch invalidation. No live integration in this slice.

## NON-GOALS
No hardware fingerprint, cross-device monitoring, automatic repair, cloud
identity, local data migration, signature/key management, claims of device proof
or production installation.

## ACCEPTANCE
- Windows .NET 8 Restore/Release build/full xUnit suite on exact PR SHA, zero
  failed/skipped; inspect logs and incremental remote diff.
- No Windows repairs, file deletions, privileged commands, network requests or
  real customer identifiers in tests.
- Same source, fact and time but different session => not corroborated/improved.
- Legacy unbound => explicit error or `Indeterminate` depending on API.
- No context identifier in public passport JSON or accidental artifact serialization.
- `main`, `develop/v0.3`, releases and tags unchanged.

## EVIDENCE / DISPOSITION
Before this slice, v0.3 stacked PR #26 passed CI #110 with 1301 tests.
Do not call this feature production-ready until the exact new SHA passes CI, and
until Windows interactive QA and trusted session lifecycle are certified.

**Remaining blocker:** real UI session change, trusted acquisition adapter,
historical record migration and QA are not implemented here.


## UI follow-up — Session-aware clipboard review (isolated component)

The WPF `TechnicalPassportReviewViewModel` no longer permits an unscoped
two-argument constructor. Its caller must pass a trusted
`Func<DiagnosticContextId>` reflecting the **currently selected** service
session. CanExecute and actual clipboard invocation compare this value to the
frozen `TechnicalPassportArtifact.ContextId`. An exception in the scope
provider denies clipboard disclosure.

The future navigation/controller layer MUST call
`InvalidateForCaseSelectionChange()` when changing customer, equipment or
diagnostic session. Revocation is irreversible for that preview, including if
a previous session is selected again; a new snapshot and fresh consent are
required.

If the scope changes during a synchronous clipboard callback, the component
does not report safe completion and explicitly warns that the clipboard
**may already contain the old JSON**. It does not attempt to claim a rollback
or silently clear the operating system clipboard.

**Integration boundary:** the view is still not registered in navigation
and no trusted live session coordinator has been built. The callback and
notification are contract requirements, not proof that a real UI case switch
has been handled. Treat any missing notification hookup as a release blocker.
WPF interactive/clipboard QA remains mandatory.
