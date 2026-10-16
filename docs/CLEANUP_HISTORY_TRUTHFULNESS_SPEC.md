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
