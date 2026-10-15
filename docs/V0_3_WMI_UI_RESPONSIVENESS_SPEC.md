# CATTECH v0.3 - SPEC Kiro: blocking WMI reads on WPF UI thread

## GOAL
Prevent WMI/DXGI probe latency from blocking CATTECH's UI dispatcher when a consumer awaits a hardware service call.

## ROOT CAUSE
WmiHardwareService exposes Task<T> methods (GetSystemInfoAsync, GetCpuInfoAsync, GetMemoryInfoAsync, GetGpuInfoAsync, GetDiskInfoAsync, GetMotherboardInfoAsync), but previously executed their entire synchronous WMI/DXGI read before returning Task.FromResult. An await on the caller does not move synchronous work off the calling thread. In particular, GetHardwareReportAsync sequentially awaited these already-completed tasks while running WMI in a UI event handler (ClientEquipmentViewModel.DetectHardwareAsync or QuickDiagnosticViewModel.RunDiagnosticAsync).

## REQUIREMENTS
1. Dispatch all six blocking source reads to worker threads and return Task<T> promptly.
2. Preserve existing public interfaces, error semantics, partial hardware data, injected memory/disk WMI fakes and UI binding behavior.
3. No blocking Result/Wait calls added; no subprocess launch, OS repairs, privilege elevation, file mutation or log containing sensitive WMI values.
4. Test two deliberately slow WMI sources (memory inventory and physical-disk topology), asserting the caller does not block and the probe runs on another thread. No real hardware in tests.
5. Keep v0.2.2 main and published release immutable; continue on develop/v0.3, PR #1 draft.
6. Full Release build and Windows CI for exact commit, 0 failed/0 skipped.

## DESIGN
Keep synchronous WMI internals in dedicated Read*Info methods and expose Task.Run(Read*Info) wrappers for all six public async calls. GetHardwareReportAsync already awaits each call in sequence, thus yields to the caller when the first worker task is scheduled. UI context is not touched within a worker.

## RISKS / LIMITS
- This is dispatcher responsiveness, not a hard WMI timeout/cancellation. A hung OS WMI provider may keep a background worker occupied until the OS call returns. Future bounded/cancellable probes need explicit design and platform validation.
- Parallel expensive WMI scans could cause load if UI permits overlapping commands. Guard UI commands separately; do not claim this commit alone solves concurrency.
- HardwareViewModel currently performs its own Task.Run(ReadInventory) and GetAwaiter().GetResult() on worker; this pre-existing sync-over-async orchestration is a separate improvement candidate.
- CI Windows and synthetic slow sources do not certify responsiveness against broken real WMI providers, DXGI drivers or legacy PCs.

## ACCEPTANCE / EVIDENCE
- Stable API; two isolated worker-dispatch tests; existing WMI memory inventory regression remains green.
- Exact head SHA, full Windows CI, tests, diff and warning delta read from GitHub.
- If gates fail, keep current slice in development and do not advance to dependent changes.
