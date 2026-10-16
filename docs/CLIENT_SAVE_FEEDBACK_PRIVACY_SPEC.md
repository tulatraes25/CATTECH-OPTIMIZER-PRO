# SPEC Kiro — Client/Equipment save feedback privacy

## Goal
Do not expose technician/customer absolute paths or untrusted storage
file names in the Client/Equipment WPF success or failure messages.

## Before
`ClientEquipmentViewModel.SaveAsync` displayed
`SaveReportAsync`'s raw returned filename upon success and
`Exception.Message` when the storage layer threw.
I/O exceptions can contain private usernames, absolute paths,
or patient/client-supplied content. A storage failure after a partial
operation also cannot prove that no report was saved.

## Design
Success acknowledges that the record was saved and guides the user
to the reports view; it does not echo the returned path/name.
Failure displays a constant safe message, explicitly asking the
technician to check existing reports *before* repeating a save.
The service result and exception are never logged or transmitted
by this ViewModel. No data deletion, changes to file-format or
write algorithms, or new navigation actions.

## Acceptance
- Injected synthetic `IOException` containing PRIVATE Windows
  paths must not appear in status or error fields.
- An injected storage filename containing a private path must not
  be reflected in the confirmation message.
- Existing new-record, hardware detection, form validation and
  report saving regressions still pass.
- Exact SHA Windows CI restore/build Release and full xUnit suite
  with zero failed/skipped, logs reviewed.

## Limits
Only UI message privacy and honest uncertainty are addressed;
not filesystem identity, user consent or real Windows UX QA.
Status DRAFT / CI pending, no main/release.
