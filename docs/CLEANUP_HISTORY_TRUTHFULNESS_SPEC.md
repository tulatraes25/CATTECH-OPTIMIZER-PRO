# SPEC Kiro — Cleanup audit history must be truthful

## GOAL
Browsing cleanup history must never fabricate events from default
.NET model values, hide valid history because malformed entries
consume the result limit, or alter an audit record on read.

## SOURCE FINDINGS
Before this change, `ListResultsAsync` used
`Directory.GetFiles(...).OrderByDescending(...).Take(maxResults)`
BEFORE parsing, silently skipped corrupt entries and deserialized JSON
`{}` into a result with a newly generated random ID (the model default).
This could hide valid earlier history or display a cleanup that never
occurred.

## DESIGN
- A reader only advertises records having explicit single, nonblank
  camelCase `id` and string `startedAt` / `finishedAt` properties.
  Duplicate case-variant keys fail closed.
- After deserialization the timestamps must be nondefault and ordered;
  counts/bytes nonnegative and nested errors/targets lists nonnull.
  Unknown additional fields and default optional lists remain supported.
- `maxResults` applies AFTER a verified entry is added.
  A nonpositive limit yields an empty result.
- Corrupt/unreadable records remain untouched; the legacy API is a
  best-effort list, not proof that there are no damaged audit files.

## ACCEPTANCE
Synthetic audit directory tests for valid old history following a
newer corrupt JSON, missing/null/duplicate ID, missing dates,
backwards timestamps, negative counters and nonpositive max limits.
No actual cleanup, customer files, privileged operations, or
automatic repair. Full exact-SHA Windows restore, Release build,
xUnit with zero failures/skips, log review.

## NON-GOALS / RISKS
Do not modify existing historical files, migrate audit schemas,
advertise strong interprocess filesystem identity, or claim full
history integrity from a best-effort list. A separate status API
would be needed before exposing damaged-record counts to WPF.
No main merges/release/hardware certification here.

Status: implementation committed in isolated draft PR; CI pending.

## Stage 2 — validate the audit *before* writing

The same structural reader validates the serialized snapshot in
`SaveResultAsync` before `EnsureDirectoryExists` or file creation.
No new history with blank ID, default/reversed dates, negative
counters/bytes or null audit collections can be published.
Null list elements are rejected both on read and on write, and no
raw user path is needed to describe invalid data.

11 synthetic invalid-write scenarios plus one existing-record
preservation regression. The previously existing unique token staging
and no-overwrite publication flow remain unchanged.

This is a structural integrity check, not proof that a claimed
cleanup physically happened. A forged but structurally valid audit
is a separate trust problem and must never be labeled authenticated
without independently protected provenance.
