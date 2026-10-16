# CATTECH SPEC Kiro: execute the actual release.yml input-validation Bash block
# using fake workflow environment variables. No packaging, writes to GitHub, or releases.
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$workflowPath = Join-Path $root '.github/workflows/release.yml'
$workflow = @(Get-Content -LiteralPath $workflowPath -Encoding utf8)
$markerIndex = -1
for ($i = 0; $i -lt $workflow.Count; $i++) {
    if ($workflow[$i] -ceq '      - name: Validate inputs') {
        if ($markerIndex -ge 0) { throw 'Duplicate release input-validation steps.' }
        $markerIndex = $i
    }
}
if ($markerIndex -lt 0) { throw 'Missing release input-validation step.' }

$runIndex = -1
for ($i = $markerIndex + 1; $i -lt $workflow.Count; $i++) {
    if ($workflow[$i] -ceq '        run: |') {
        $runIndex = $i
        break
    }
    if ($workflow[$i] -match '^      - name: ') { break }
}
if ($runIndex -lt 0) { throw 'Missing multiline Bash input-validation block.' }

$scriptLines = [System.Collections.Generic.List[string]]::new()
for ($i = $runIndex + 1; $i -lt $workflow.Count; $i++) {
    $line = [string]$workflow[$i]
    if ($line.Trim().Length -gt 0 -and -not $line.StartsWith('          ')) {
        break
    }
    $scriptLines.Add($(if ($line.StartsWith('          ')) { $line.Substring(10) } else { '' }))
}
if ($scriptLines.Count -lt 15) { throw 'Unexpectedly short input-validation script.' }
if (-not (Get-Command bash -ErrorAction SilentlyContinue)) {
    throw 'Git Bash is required for the Windows CI release input regression gate.'
}

$scratchRelative = 'TestResults/ReleasePreflightInputs'
$scratch = Join-Path $root $scratchRelative
New-Item -ItemType Directory -Path $scratch -Force | Out-Null
$scriptPath = Join-Path $scratch 'validate.sh'
$scriptRelative = './TestResults/ReleasePreflightInputs/validate.sh'
$outputRelative = './TestResults/ReleasePreflightInputs/output.txt'
$outputPath = Join-Path $scratch 'output.txt'
$lf = [string][char]10
[System.IO.File]::WriteAllText(
    $scriptPath, ($scriptLines -join $lf) + $lf, [System.Text.UTF8Encoding]::new($false))

$sha = 'a' * 40
$cases = @(
    @{ Name = 'valid-docs-dry-run'; Version = '0.3.0'; Sha = $sha; Notes = 'docs/RELEASE_NOTES_V0_3.md'; Dry = 'true'; Pass = $true },
    @{ Name = 'valid-root-live-flag'; Version = '0.3.0'; Sha = $sha; Notes = 'CHANGELOG.md'; Dry = 'false'; Pass = $true },
    @{ Name = 'reject-notes-newline-output-injection'; Version = '0.3.0'; Sha = $sha; Notes = ('docs/notes.md' + [char]10 + 'tag_name=v9.9.9'); Dry = 'true'; Pass = $false },
    @{ Name = 'reject-notes-cr-output-injection'; Version = '0.3.0'; Sha = $sha; Notes = ('docs/notes.md' + [char]13 + 'dry_run=false'); Dry = 'true'; Pass = $false },
    @{ Name = 'reject-notes-leading-hyphen'; Version = '0.3.0'; Sha = $sha; Notes = '-notes.md'; Dry = 'true'; Pass = $false },
    @{ Name = 'reject-notes-absolute'; Version = '0.3.0'; Sha = $sha; Notes = '/etc/passwd'; Dry = 'true'; Pass = $false },
    @{ Name = 'reject-notes-traversal'; Version = '0.3.0'; Sha = $sha; Notes = 'docs/../other.md'; Dry = 'true'; Pass = $false },
    @{ Name = 'reject-notes-relative-dot'; Version = '0.3.0'; Sha = $sha; Notes = 'docs/./other.md'; Dry = 'true'; Pass = $false },
    @{ Name = 'reject-notes-shell-metachar'; Version = '0.3.0'; Sha = $sha; Notes = 'docs/notes.md;exit 0'; Dry = 'true'; Pass = $false },
    @{ Name = 'reject-notes-tab'; Version = '0.3.0'; Sha = $sha; Notes = ('docs/notes' + [char]9 + '.md'); Dry = 'true'; Pass = $false },
    @{ Name = 'reject-notes-url'; Version = '0.3.0'; Sha = $sha; Notes = 'https://example.com/release.md'; Dry = 'true'; Pass = $false },
    @{ Name = 'reject-dry-run-value'; Version = '0.3.0'; Sha = $sha; Notes = 'docs/notes.md'; Dry = 'maybe'; Pass = $false },
    @{ Name = 'reject-source-sha-newline'; Version = '0.3.0'; Sha = ($sha + [char]10 + 'dry_run=false'); Notes = 'docs/notes.md'; Dry = 'true'; Pass = $false },
    @{ Name = 'reject-version-injection'; Version = '0.3.0;exit 0'; Sha = $sha; Notes = 'docs/notes.md'; Dry = 'true'; Pass = $false }
)
$vars = @('CATTECH_INPUT_VERSION', 'CATTECH_INPUT_SOURCE_SHA',
          'CATTECH_INPUT_RELEASE_NOTES_PATH', 'CATTECH_INPUT_DRY_RUN', 'GITHUB_OUTPUT')
$original = @{}
foreach ($name in $vars) {
    $original[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
try {
    Push-Location $root
    try {
        foreach ($case in $cases) {
            [Environment]::SetEnvironmentVariable('CATTECH_INPUT_VERSION', $case.Version, 'Process')
            [Environment]::SetEnvironmentVariable('CATTECH_INPUT_SOURCE_SHA', $case.Sha, 'Process')
            [Environment]::SetEnvironmentVariable('CATTECH_INPUT_RELEASE_NOTES_PATH', $case.Notes, 'Process')
            [Environment]::SetEnvironmentVariable('CATTECH_INPUT_DRY_RUN', $case.Dry, 'Process')
            [Environment]::SetEnvironmentVariable('GITHUB_OUTPUT', $outputRelative, 'Process')
            Remove-Item -LiteralPath $outputPath -Force -ErrorAction SilentlyContinue

            & bash $scriptRelative 2>&1 | Out-Null
            $exitCode = $LASTEXITCODE
            if ($case.Pass) {
                if ($exitCode -ne 0) { throw "Valid input rejected: $($case.Name) (exit $exitCode)" }
                if (-not (Test-Path -LiteralPath $outputPath)) {
                    throw "Missing GITHUB_OUTPUT for $($case.Name)"
                }
                $actual = @(Get-Content -LiteralPath $outputPath -Encoding utf8)
                $expected = @(
                    'tag_name=v0.3.0',
                    "source_sha=$sha",
                    "release_notes_path=$($case.Notes)",
                    "dry_run=$($case.Dry)"
                )
                if ($actual.Count -ne $expected.Count) {
                    throw "GITHUB_OUTPUT line count mismatch for $($case.Name)"
                }
                for ($i = 0; $i -lt $expected.Count; $i++) {
                    if ($actual[$i] -cne $expected[$i]) {
                        throw "GITHUB_OUTPUT mismatch for $($case.Name) at line $i"
                    }
                }
            } else {
                if ($exitCode -eq 0) { throw "Unsafe input accepted: $($case.Name)" }
                if ((Test-Path -LiteralPath $outputPath) -and
                    (Get-Item -LiteralPath $outputPath).Length -gt 0) {
                    throw "Unsafe input wrote GITHUB_OUTPUT: $($case.Name)"
                }
            }
        }
    } finally {
        Pop-Location
    }
} finally {
    foreach ($name in $vars) {
        [Environment]::SetEnvironmentVariable($name, $original[$name], 'Process')
    }
    Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host "PASS: $($cases.Count) real release preflight scenarios; no workflow dispatch or tag/release writes."
