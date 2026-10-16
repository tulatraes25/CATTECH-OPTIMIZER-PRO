# SPEC Kiro #43/#50 — real Client/Equipment logical case coordinator

## Goal
Connect existing, thoroughly tested `DiagnosticSessionCoordinator`
to the *actual* CATTECH Main navigation and Client/Equipment form.
Prior to this change the session lease model existed only in Core
and the technical-passport demo. MainViewModel never selected a real
logical case, so real form edits could not revoke earlier review leases.

## Design
- MainViewModel owns exactly one in-memory coordinator for the
  application window.
- The first explicit ClientEquipment view creation starts a fresh
  opaque logical session (not an equipment attestation).
- ClientEquipmentViewModel receives only an optional `Action`
  to notify Main that a case identity selection changed. It does
  NOT receive mutable session authority.
- Client name or brand/model/type/serial edits select a fresh case,
  revoking all prior lease epochs. A NewRecord invocation always
  rotates, including reopening a visually identical/blank form.
- Phone/email/notes edits don't change selected equipment identity.
- Navigating between pages without changing the selected case
  preserves its lease; no hidden phantom case is created by navigation.
- The owning MainVM retains only an internal ID and coordinator;
  no customer records, serial numbers or hostnames are used in GUIDs,
  and no public passport JSON contains that scope.

## Acceptance
In Core tests with synthetic fake services, identity edits/new
record revoke old leases, while contact details do not.
WPF STA smoke creates the *real* MainViewModel and navigates the
client view, confirming lease revocation on case edit/restart but
not normal sidebar navigation. All Windows Restore/Release build,
xUnit tests on exact SHA with 0 failed/skipped and real CI log review.

## Non-goals / release blockers
- This does not prove that a logical case belongs to a physical
  computer; real WMI/SMART identity requires separate field tests.
- A production TechnicalPassportReview view is not yet connected
  to case selection: the dedicated demo remains isolated.
- No real clipboard, destructive cleanup or repair, service report
  migration, branch merge, tag change or release.
- Mixed case/device evidence needs additional application workflow
  adapters before G3 of issue #43 can pass.
- Interactive Windows QA remains separate from STA/XAML smoke.

State: DRAFT / CI pending. No direct merge to main.
