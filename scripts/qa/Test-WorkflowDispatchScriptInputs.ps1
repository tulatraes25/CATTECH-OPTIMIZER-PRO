# SPEC Kiro #78: never embed workflow_dispatch strings in shell source.
# env:/with:/if: declarations are safe data bindings, not executable scripts.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$names = @('release-package.yml', 'release.yml')
$checkedBlocks = 0

foreach ($name in $names) {
    $path = Join-Path $root ".github/workflows/$name"
    $lines = Get-Content -LiteralPath $path -Encoding utf8
    $inRun = $false
    $runIndent = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = [string]$lines[$i]
        $m = [regex]::Match($line, '^( *)run:\s*(?:\|[-+]?|>[-+]?)\s*$')
        if ($m.Success) {
            $inRun = $true
            $runIndent = $m.Groups[1].Length
            $checkedBlocks++
            continue
        }
        $indent = [regex]::Match($line, '^ *').Value.Length
        if ($inRun -and $line.Trim().Length -gt 0 -and $indent -le $runIndent) {
            $inRun = $false
        }
        $inlineRun = $line -match '^\s*run:\s*[^|>\s]'
        if (($inRun -or $inlineRun) -and
            $line -match '\$\{\{\s*(?:inputs\.|github\.event\.inputs\.)') {
            throw "Unsafe manual-input script interpolation at ${name}:$($i+1). Use env variables."
        }
    }
}
if ($checkedBlocks -lt 10) {
    throw "Too few release shell blocks audited ($checkedBlocks); review workflow format."
}
Write-Host "PASS: $checkedBlocks release script blocks inspected; no direct manual input expressions."
