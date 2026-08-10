<#
.SYNOPSIS
    Captures a SAFE baseline of the environment and a CATTECH package for real-Windows smoke QA.
.DESCRIPTION
    Read-only collector: inspects a CATTECH package (extracted) and general environment facts.
    Never captures PII (username, hostname, IP/MAC, serials, product keys, full user paths).
    Only reads; writes only its own evidence files under OutputDirectory.
.PARAMETER PackagePath
    Required. Directory containing the extracted CATTECH package (must include Cattech.Optimizer.Pro.UI.exe).
.PARAMETER OutputDirectory
    Optional. Evidence output directory. Default: output/qa-smoke.
.PARAMETER Label
    Optional. Short label for filenames (sanitized). Example: desktop-intel
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts/qa/Collect-SmokeEvidence.ps1 -PackagePath output/qa-smoke/v0.2.0 -Label baseline-v0.2.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [string]$OutputDirectory = "output/qa-smoke",

    [string]$Label = ""
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Message) {
    Write-Host "[smoke] $Message"
}

# Sanitize label for filenames (allow letters, digits, dash, underscore)
$sanitizedLabel = ""
if (-not [string]::IsNullOrWhiteSpace($Label)) {
    $sanitizedLabel = ($Label -replace '[^a-zA-Z0-9_-]', '-').Trim('-')
}

$timestamp = Get-Date
$timestampCompact = $timestamp.ToString("yyyyMMdd-HHmmss")
$timestampIso = $timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ")

$fileBase = if ($sanitizedLabel) { "smoke-evidence-$sanitizedLabel-$timestampCompact" } else { "smoke-evidence-$timestampCompact" }

$evidence = [ordered]@{
    SchemaVersion = 1
    CapturedAtIso8601 = $timestampIso
    Label = $Label
    Package = $null
    Environment = $null
    AutomaticChecks = $null
}

# ---------------------------------------------------------------
# Package validation
# ---------------------------------------------------------------

$package = [ordered]@{}
$packagePath = $PackagePath

if (-not (Test-Path -LiteralPath $packagePath -PathType Container)) {
    Write-Error "PackagePath is not a directory: $packagePath"
    exit 1
}

$exePath = Join-Path $packagePath "Cattech.Optimizer.Pro.UI.exe"
$exePresent = Test-Path -LiteralPath $exePath -PathType Leaf
$package.PackagePath = $packagePath
$package.ExePresent = $exePresent
$package.PackageValid = $exePresent

if (-not $exePresent) {
    $package.ValidationReason = "Cattech.Optimizer.Pro.UI.exe not found in package path"
    $evidence.Package = $package
    $evidence.AutomaticChecks = [ordered]@{ PackageBaseline = "FAIL" }
    Write-Error "Cattech.Optimizer.Pro.UI.exe not found in: $packagePath"
    exit 1
}

try {
    $fileInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exePath)
    $package.FileVersion = $fileInfo.FileVersion
    $package.ProductVersion = $fileInfo.ProductVersion
}
catch {
    $package.FileVersion = $null
    $package.ProductVersion = $null
}

$exeSha = (Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash
$package.ExeSha256 = $exeSha

$criticalFiles = [ordered]@{
    "Cattech.Optimizer.Pro.UI.exe" = $exePresent
    "LibreHardwareMonitorLib.dll" = (Test-Path -LiteralPath (Join-Path $packagePath "LibreHardwareMonitorLib.dll") -PathType Leaf)
    "config/herramientas.json" = (Test-Path -LiteralPath (Join-Path $packagePath "config\herramientas.json") -PathType Leaf)
    "README.md" = (Test-Path -LiteralPath (Join-Path $packagePath "README.md") -PathType Leaf)
    "LICENSE" = (Test-Path -LiteralPath (Join-Path $packagePath "LICENSE") -PathType Leaf)
}
$package.CriticalFiles = $criticalFiles

# smartctl bundled (expected absent: external dependency)
$smartctlBundled = @(Get-ChildItem -LiteralPath $packagePath -Recurse -Filter "smartctl.exe" -File -ErrorAction SilentlyContinue).Count -gt 0
$package.SmartctlBundled = $smartctlBundled

# herramientas.json: validity only, never store the configured path value
$configPath = Join-Path $packagePath "config\herramientas.json"
$config = [ordered]@{
    ConfigValid = $false
    SmartctlAutoDetect = $null
    SmartctlPathConfigured = $null
}
if (Test-Path -LiteralPath $configPath -PathType Leaf) {
    try {
        $json = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
        $config.ConfigValid = $true
        $config.SmartctlAutoDetect = [bool]$json.smartctlAutoDetect
        $config.SmartctlPathConfigured = -not [string]::IsNullOrWhiteSpace([string]$json.smartctlPath)
    }
    catch {
        $config.ConfigValid = $false
    }
}
$package.Config = $config

$evidence.Package = $package

# ---------------------------------------------------------------
# Environment (no PII)
# ---------------------------------------------------------------

$envBlock = [ordered]@{}

$envBlock.PowerShellVersion = "$($PSVersionTable.PSVersion.Major).$($PSVersionTable.PSVersion.Minor)"
$envBlock.ProcessIs64Bit = [Environment]::Is64BitProcess
$envBlock.OSIs64Bit = [Environment]::Is64BitOperatingSystem

$envBlock.IsAdministrator = $false
try {
    $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object System.Security.Principal.WindowsPrincipal($identity)
    $envBlock.IsAdministrator = $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)
}
catch {
    $envBlock.IsAdministrator = $null
}

# OS facts (Caption/Version/BuildNumber/OSArchitecture are not PII)
$envBlock.Windows = "N/D"
$envBlock.WindowsBuild = "N/D"
$envBlock.WindowsArchitecture = "N/D"
try {
    $os = Get-CimInstance -ClassName Win32_OperatingSystem -ErrorAction Stop
    $envBlock.Windows = [string]$os.Caption
    $envBlock.WindowsBuild = [string]$os.BuildNumber
    $envBlock.WindowsArchitecture = [string]$os.OSArchitecture
}
catch {
    # optional data: keep N/D
}

# CPU facts (name/manufacturer/logical processors are not PII; no serials)
$envBlock.CpuName = "N/D"
$envBlock.CpuManufacturer = "N/D"
$envBlock.LogicalProcessors = "N/D"
try {
    $cpu = Get-CimInstance -ClassName Win32_Processor -ErrorAction Stop | Select-Object -First 1
    if ($cpu) {
        $envBlock.CpuName = [string]$cpu.Name
        $envBlock.CpuManufacturer = [string]$cpu.Manufacturer
        $envBlock.LogicalProcessors = [int]$cpu.NumberOfLogicalProcessors
    }
}
catch {
    # optional data: keep N/D
}

# Total RAM in GB (capacity only; no serials)
$envBlock.RamTotalGB = "N/D"
try {
    $cs = Get-CimInstance -ClassName Win32_ComputerSystem -ErrorAction Stop | Select-Object -First 1
    if ($cs -and $cs.TotalPhysicalMemory) {
        $envBlock.RamTotalGB = [Math]::Round([double]$cs.TotalPhysicalMemory / (1024 * 1024 * 1024), 1)
    }
}
catch {
    # optional data: keep N/D
}

$evidence.Environment = $envBlock

# ---------------------------------------------------------------
# Automatic checks
# ---------------------------------------------------------------

$checks = [ordered]@{}

$criticalOk = $true
foreach ($key in @("Cattech.Optimizer.Pro.UI.exe", "LibreHardwareMonitorLib.dll", "config/herramientas.json", "README.md", "LICENSE")) {
    if (-not $criticalFiles[$key]) { $criticalOk = $false }
}
$checks.PackageBaseline = if ($exePresent -and $config.ConfigValid -and $criticalOk) { "PASS" } else { "FAIL" }
$checks.SmartctlBundledExpectedAbsent = if ($smartctlBundled) { "UNEXPECTED PRESENT" } else { "PASS" }

$evidence.AutomaticChecks = $checks

# ---------------------------------------------------------------
# Write evidence (JSON + Markdown)
# ---------------------------------------------------------------

if (-not (Test-Path -LiteralPath $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
}

$jsonPath = Join-Path $OutputDirectory "$fileBase.json"
$evidenceJson = $evidence | ConvertTo-Json -Depth 6
[System.IO.File]::WriteAllText($jsonPath, $evidenceJson, [System.Text.UTF8Encoding]::new($false))

$mdLines = New-Object System.Collections.Generic.List[string]
$mdLines.Add("# CATTECH Smoke Evidence")
$mdLines.Add("")
$mdLines.Add("Captured: $timestampIso")
$mdLines.Add("Label: $($evidence.Label)")
$mdLines.Add("")
$mdLines.Add("## Package")
$mdLines.Add("")
$mdLines.Add("| Field | Value |")
$mdLines.Add("|-------|-------|")
$mdLines.Add("| Version (FileVersion) | $($package.FileVersion) |")
$mdLines.Add("| ProductVersion | $($package.ProductVersion) |")
$mdLines.Add("| EXE SHA-256 | $($package.ExeSha256) |")
$mdLines.Add("| smartctl bundled | $(if ($smartctlBundled) { 'Si' } else { 'No' }) |")
$mdLines.Add("| Config | $(if ($config.ConfigValid) { 'Valida' } else { 'No valida' }) |")
$mdLines.Add("")
$mdLines.Add("### Critical files")
$mdLines.Add("")
foreach ($key in $criticalFiles.Keys) {
    $mdLines.Add("- $key : $(if ($criticalFiles[$key]) { 'Si' } else { 'No' })")
}
$mdLines.Add("")
$mdLines.Add("## Environment")
$mdLines.Add("")
$mdLines.Add("| Field | Value |")
$mdLines.Add("|-------|-------|")
$mdLines.Add("| Windows | $($envBlock.Windows) |")
$mdLines.Add("| Build | $($envBlock.WindowsBuild) |")
$mdLines.Add("| Arquitectura | $($envBlock.WindowsArchitecture) |")
$mdLines.Add("| RAM total (GB) | $($envBlock.RamTotalGB) |")
$mdLines.Add("| CPU | $($envBlock.CpuName) |")
$mdLines.Add("| CPU fabricante | $($envBlock.CpuManufacturer) |")
$mdLines.Add("| Procesadores logicos | $($envBlock.LogicalProcessors) |")
$mdLines.Add("| PowerShell | $($envBlock.PowerShellVersion) |")
$mdLines.Add("| Admin | $(if ($envBlock.IsAdministrator) { 'Si' } else { 'No' }) |")
$mdLines.Add("")
$mdLines.Add("## Automatic checks")
$mdLines.Add("")
$mdLines.Add("| Check | Result |")
$mdLines.Add("|-------|--------|")
$mdLines.Add("| Package baseline | $($checks.PackageBaseline) |")
$mdLines.Add("| smartctl bundled expected absent | $($checks.SmartctlBundledExpectedAbsent) |")
$mdLines.Add("")
$mdLines.Add("> Manual smoke checklist: see docs/QA_REAL_SMOKE.md. Interactive results belong in docs/QA_SMOKE_RESULT_TEMPLATE.md.")

$mdPath = Join-Path $OutputDirectory "$fileBase.md"
[System.IO.File]::WriteAllLines($mdPath, $mdLines, [System.Text.UTF8Encoding]::new($false))

Write-Step "Evidence written:"
Write-Step "  JSON: $jsonPath"
Write-Step "  MD:   $mdPath"
Write-Step "Package baseline: $($checks.PackageBaseline) (exit 0)"

exit 0
