# v0.3 DumpChk file identity and reparse-point safety

This change responds to issue #2. It uses Windows handles to bind the selected minidump name to an open file while the external DumpChk process executes.

## Threat model

An untrusted filename, directory junction or file symlink could redirect a path from the intended Windows Minidump directory. Even a legitimate file can be replaced after initial discovery. Path normalization and File.Exists alone do not prevent this race.

## Defense implemented

1. Strict basename, directory containment, file existence, explicit technician confirmation, and trusted SDK locator continue to apply.
2. Open directory with FILE_FLAG_OPEN_REPARSE_POINT and FILE_FLAG_BACKUP_SEMANTICS. Reject a directory reparse point and verify the final opened handle path matches the expected canonical directory.
3. Open the dump with FILE_FLAG_OPEN_REPARSE_POINT and GENERIC_READ. Reject file reparse points and confirm its opened handle path equals the expected canonical file path.
4. Hold both handles until DumpChk completes. Directory sharing denies DELETE, and file sharing allows READ only (denies write and delete), so normal Windows file replacement cannot invalidate the verified name while the child opens and reads it.
5. If acquisition fails, do not start the external tool. No dump is copied or uploaded. Handle resources are released on failure, cancellation and normal completion.

## Evidence and limits

Unit tests check path mismatch, denial propagation, lease lifetime, and Windows filesystem sharing. Windows PR CI must verify the exact head SHA and all tests. Neither a file lock nor this application can protect against privileged kernel tampering, an untrusted SDK executable, or all external races. Test with real supported Windows DumpChk before stating hardware/field verification.

Do not claim full end-to-end certification until the Windows Actions logs and subsequent focused manual read-only smoke are checked. Leave issue #2 open if any residual problem is identified.
