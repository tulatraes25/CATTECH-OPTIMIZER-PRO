# SPEC Kiro #43 — verifiable integration and progress ledger (2026-10-15)

**Source of truth:** GitHub objects and exact SHAs. This is a
reconciliation ledger, **not** permission to merge or ship.

## 1. Measured release progress (not code-completion claims)

For candidate **v0.3**, define exactly six independent release-readiness
gates. Percentage is *count of gates with independently verified evidence*;
all gates have equal counting weight only for transparency. A partial gate
counts **zero**, not a fabricated fraction.

| Gate | Evidence required | Current decision |
|---|---|---|
| G1 | Candidate branch exists and builds Release | PASS: PR #71 @ `c7c8c3e85ec6a563b3c9e2451399912c6ad97e55` |
| G2 | Full Windows CI for G1 SHA has 0 fails/skips | PASS: CI #170, 1557/0/0 |
| G3 | Parallel lineage #29/#31/#34 reconciled semantically, source and tests | OPEN: matrix below, selected parity tests in draft |
| G4 | Safety P1 cleanup #10–#20 and report/file races disposition | OPEN: unresolved issues #10–#20, #53/#55 |
| G5 | Interactive WPF + hardware validation on Windows 10 and Windows 11 | OPEN: no valid real-machine certification |
| G6 | Release candidate, provenance, hashes, packaging and published verification | OPEN: stable `main` stays unchanged |

**v0.3 readiness score: 2/6 = 33%.** This is a *gate count*,
NOT a percentage of features or engineering hours.
Automated suite pass rate: 1557/1557 = 100% of executed tests, not code coverage.
Physical QA evidence gate: 0/1 = 0% for candidate v0.3.
Full roadmap release milestones v0.2 through v1.0: 1/5 = 20%
(one completed stage at v0.2, then v0.3/v0.4/v0.5/v1.0 pending).
These metrics deliberately use different denominators.

## 2. Live PR topology

The latest independently CI-certified development line is PR #71 @
`c7c8c3e85ec6a563b3c9e2451399912c6ad97e55`.
Its earlier stack includes #28, #32, #39, #42, #52, #54, #56,
#57, #59, #61, #62, #63, #64, #65, #66, #67, #68, #69 and #70 (verified with exact `compare_commits` ancestor
checks for #28/#39/#42/#52/#59). The parallel branches are:

| PR | Exact head SHA | Relationship to current #61 | Primary concern |
|---|---|---|---|
| #29 | `a5cbf06a6ace47f44f45b26c8d65439dc9c5bc2e` | DIVERGED, 13 side-only / 161 latest-only commits since common ancestor | Context isolation, provenance |
| #31 | `1773d4d08cb19c60b523a84a91747589c6946735` | DIVERGED, 17 / 161 | WPF passport consent revocation |
| #34 | `7fc060362515f318520efcbb6bfe9e03b600c056` | DIVERGED, 21 / 161 | Evaluator-only repair outcomes |

`#29` is a Git ancestor of `#31` (4 commits); `#31`
is a Git ancestor of `#34` (4 commits). The nearest combined
parallel tip is #34, **not** three separate patches to merge.
Shared merge-base to the current line:
`f7751da272cbedc31de5c170c5db73d4dc33edc4`.

Files with semantic overlap include
`DiagnosticContextId.cs`, `EvidenceRecord.cs`,
`DeterministicDiagnosticEvaluator.cs`, `RepairOutcomeEvaluator.cs`,
`TechnicalPassport.cs`, the passport WPF review ViewModel/view,
and their tests. The current branch adds component-level
`DiagnosticSubjectId` and stronger consent leases, so blind
cherry-picks/rebases can silently regress behavior.

A different test method name or line count is **not proof of a bug**.
Likewise, a green CI on a side branch is **not proof of compatibility**
with this newer stack.

## 3. Semantic parity: in progress

Selected assertions moved to the latest API in
`ParallelEvidenceBranchParityTests.cs`:

- Different service cases cannot combine observations to corroborate a
  diagnostic rule, independent of enumeration order.
- Empty evidence still requires an explicitly bound context.
- Unknown legacy case IDs and unknown components cannot produce evidence.
- Two matching source/fact values across different cases cannot claim repair.
- Two different disks in one case cannot claim repair.
- Public repair outcome JSON must not reveal opaque case/disk/evidence IDs.

Tests use synthetic, deterministic GUIDs only. No real equipment tested.
Still to reconcile: passport WPF revocation sequences from #31 and
forged/out-of-context passport assessments from #34, along with
complete overlap review and CI on a single integration candidate.

## 4. Risk / non-goals

No merge of #29/#31/#34 into main, no force-push, no tag movement,
no release, no real Windows repair, no device fingerprint claims.
CI passing synthetic tests does not eliminate cross-process TOCTOU or
substitute for hardware QA. Each subsequent integration slice needs
its own head SHA, diff verification, Restore/Release build/full
xUnit and inspected CI logs.

Status: **PARTIAL / DRAFT**. Do not mark G3 PASS merely because
these selected six semantic assertions pass.

## 5. Stage 2: passport semantics from parallel PR #34 (2026-10-16)

Goal: restore actual *safety contracts* from the divergent #34 line rather than
blindly copying old signatures. The baseline #71 API requires an explicit
`DiagnosticContextId` and stronger `DiagnosticSubjectId`; preserving
those improvements takes priority over byte-for-byte equality.

Source finding: the parallel #34 rejected a passport with **zero evidence**,
but the newer builder accepted a case GUID plus an empty record set. This can
create a superficially credible report without even an explicit unavailable
reading. Stage 2 restores a strict nonempty record requirement while still
allowing an explicit `Unavailable` or `AccessDenied` observation with
clearly labeled counts.

Added six synthetic `PassportIntegrationParityStage2Tests` assertions:
- zero evidence and zero conclusions must not create a public passport;
- an insufficient assessment with zero evidence must not make one exportable;
- mixed-case observations are rejected;
- a foreign diagnostic with an evidence ID reused by a local record is rejected;
- a cross-case repair outcome with IDs also present locally cannot be exported;
- an explicit unavailable measurement is allowed but never reported as observed
  or leaked with its opaque case GUID.

Architecture review: existing `PassportConsentContextSecurityTests` and
`DiagnosticSessionCoordinatorTests` cover the key parallel #31
selection/clipboard cases with stronger epoch-bound leases and callback
reentrancy guards; stage 2 does not reintroduce the older unversioned public
context-provider constructor.

Acceptance: exact-SHA Windows restore, Release build, full xUnit 0 fail/skip,
reviewed CI job logs; then semantic comparison of the remaining passport
constructor, forbidden-mixed-assessment and WPF cases.
**G3 remains OPEN** despite selected parity tests. No WPF interactive
certification, real hardware, public release or `main` merge.

## 6. Stage 3: derived-count uniqueness (2026-10-16)

### GOAL
A client-facing passport must not inflate its conclusions if an enumerable
contains the same diagnostic assessment or repair postcheck repeatedly.
Stage 2 ensured there is **at least one** real case-bound evidence record;
stage 3 ensures each derived basis is counted **at most once**.

### DESIGN
The Core passport builder checks two logical identities before computing
public aggregate status counts:

- Diagnostic: rule ID+version and the **full ordered supporting/contradicting
  evidence ID sets**, plus missing/future fact and subject references.
  Status is intentionally not part of the identity: conflicting statuses
  over the same basis cannot both become separate results.
- Postcheck: rule ID+version, fact key and the exact before/after evidence
  pair. A duplicate pair cannot add a second improvement.

Different subjects/disks remain valid when their underlying evidence
identities differ, including multiple assessments using one rule. This
avoids the simplistic but incorrect "one rule ID per equipment" ban.

### ACCEPTANCE / EVIDENCE
Six deterministic synthetic tests check both repeated object references and
independently reconstructed equivalent assessments/outcomes; also prove
that two distinct disks can both contribute valid results for the same
rules. No API accepts caller-fabricated arbitrary improvement status.
Exact-SHA Windows Release build, all xUnit 0 fails/skips and raw CI logs
are mandatory before marking code VERIFIED.

### LIMITS / DISPOSITION
No cryptographic physical component attestation; no direct repair calls,
filesystem modifications, client PII or actual clipboard. The public
passport v1 has **no evaluation event ID/time**, so repeated independent
evaluations of precisely the same basis must fail closed rather than
claim separate events. A future schema may add explicit event identity.
**G3 remains open** until the full source-diff parity matrix is reconciled.
