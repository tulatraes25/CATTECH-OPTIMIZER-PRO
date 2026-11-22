# Prueba end-to-end de la pantalla Limpieza via UI Automation.
# Crea marcadores reales en %TEMP%, ejecuta la UI (escanear -> limpiar ->
# confirmar en el diálogo) y verifica que los archivos se borren y que los
# nombres protegidos se conserven.
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string]$EvidencePath = ""
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$script:appProcessId = 0

function Invoke-UiRetry {
    param([scriptblock]$Action, [int]$Attempts = 8)
    for ($i = 0; $i -lt $Attempts; $i++) {
        try { return & $Action }
        catch { Start-Sleep -Milliseconds 400 }
    }
    throw "UI Automation fallo luego de $Attempts intentos."
}

function Get-AppWindow {
    param($Process, [int]$TimeoutSeconds = 60)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $window = Invoke-UiRetry {
            $root = [System.Windows.Automation.AutomationElement]::RootElement
            $condition = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $Process.Id)
            $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition)
        }
        if ($window) { return $window }
        Start-Sleep -Milliseconds 400
    }
    throw "No se encontro la ventana de la aplicacion."
}

function Get-Descendants {
    param($Root)
    Invoke-UiRetry {
        $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.Condition]::TrueCondition)
    }
}

function Find-ByNameContains {
    param($Root, [string]$Text, [string]$ControlType = "")
    foreach ($element in (Get-Descendants $Root)) {
        $type = $element.Current.ControlType.ProgrammaticName -replace 'ControlType\.', ''
        if ($ControlType -and $type -ne $ControlType) { continue }
        if ($element.Current.Name -and $element.Current.Name.Contains($Text)) { return $element }
    }
    return $null
}

function Invoke-Element {
    param($Element)
    $pattern = $null
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
        $pattern.Invoke(); return $true
    }
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$pattern)) {
        $pattern.Toggle(); return $true
    }
    return $false
}

function Get-UiTexts {
    param($Root)
    @(foreach ($element in (Get-Descendants $Root)) {
            $type = $element.Current.ControlType.ProgrammaticName -replace 'ControlType\.', ''
            $name = ($element.Current.Name -replace '\s+', ' ').Trim()
            if ($name -and $type -in @('Text', 'CheckBox', 'Button')) { $name }
        })
}

function Wait-TextMatch {
    param($Root, [string[]]$Patterns, [int]$TimeoutSeconds = 180)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $texts = Get-UiTexts $Root
        foreach ($pattern in $Patterns) {
            if ($texts -match $pattern) { return $texts }
        }
        Start-Sleep -Milliseconds 500
    }
    return $null
}

function Confirm-ModalDialog {
    param($Root, [int]$TimeoutSeconds = 30)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        foreach ($label in @('Sí', 'Si', 'Yes')) {
            $button = Find-ByNameContains -Root $Root -Text $label -ControlType 'Button'
            if ($button) {
                [void](Invoke-Element $button)
                return $true
            }
        }
        Start-Sleep -Milliseconds 300
    }
    return $false
}
$temp = [System.IO.Path]::GetTempPath()
$markers = @{
    root      = Join-Path $temp 'cattech-qa-root.tmp'
    nested    = Join-Path $temp 'cattech-qa-nested\cattech-qa-nested.tmp'
    protected = Join-Path $temp 'cattech-qa.lock'
}
$nestedDir = Join-Path $temp 'cattech-qa-nested'
Remove-Item $markers.root, $markers.protected -Force -ErrorAction SilentlyContinue
Remove-Item $nestedDir -Recurse -Force -ErrorAction SilentlyContinue
Set-Content -Path $markers.root -Value ('x' * 4096) -NoNewline
New-Item -ItemType Directory -Force -Path $nestedDir | Out-Null
Set-Content -Path $markers.nested -Value ('y' * 2048) -NoNewline
Set-Content -Path $markers.protected -Value 'protected' -NoNewline

$evidence = [ordered]@{
    timestamp         = (Get-Date).ToString('o')
    exe               = $Exe
    markers           = $markers
    scanStatus        = $null
    dialogConfirmed   = $false
    resultTexts       = @()
    deletedRoot       = $false
    deletedNested     = $false
    protectedKept     = $false
    nestedDirRemoved  = $false
    success           = $false
}

$process = Start-Process -FilePath $Exe -PassThru
$script:appProcessId = $process.Id
$window = $null
try {
    $window = Get-AppWindow -Process $process
    Start-Sleep -Seconds 2

    $nav = Find-ByNameContains -Root $window -Text 'Limpieza' -ControlType 'Button'
    if (-not $nav) { throw 'No se encontro el boton Limpieza.' }
    [void](Invoke-Element $nav)
    Start-Sleep -Seconds 2

    # El escaneo ahora arranca solo al entrar; si no, se fuerza con el boton.
    if (-not (Wait-TextMatch -Root $window -Patterns @('^Listo para limpiar$', '^Escaneo ') -TimeoutSeconds 20)) {
        $scanButton = Find-ByNameContains -Root $window -Text 'Escanear temporales' -ControlType 'Button'
        if (-not $scanButton) { throw 'No se encontro el boton de escaneo.' }
        [void](Invoke-Element $scanButton)
        $afterScan = Wait-TextMatch -Root $window -Patterns @(
            '^Listo para limpiar$', '^Escaneo completado', '^Escaneo sin ubicaciones', '^Error al escanear$') -TimeoutSeconds 180
        if (-not $afterScan) { throw 'El escaneo no termino en el tiempo esperado.' }
    }

    $scanTexts = Get-UiTexts $window
    $evidence.scanStatus = ($scanTexts | Where-Object { $_ -match '^(Listo para limpiar|Escaneo)' } | Select-Object -First 1)
    $evidence['uiTextsAfterScan'] = $scanTexts

    $cleanButton = Find-ByNameContains -Root $window -Text 'Limpiar seleccionados' -ControlType 'Button'
    if (-not $cleanButton) { throw 'No se encontro el boton de limpieza.' }
    [void](Invoke-Element $cleanButton)

    $evidence.dialogConfirmed = Confirm-ModalDialog -Root $window

    $afterClean = Wait-TextMatch -Root $window -Patterns @(
        '^Limpieza completada', '^Limpieza incompleta', '^Error durante la limpieza$') -TimeoutSeconds 300
    if (-not $afterClean) { throw 'La limpieza no termino en el tiempo esperado.' }
    $evidence.resultTexts = @($afterClean | Where-Object {
            $_ -match '^Limpieza (completada|incompleta)' -or $_ -match 'Duraci' -or
            $_ -match 'registro' -or $_ -match 'no se pudo' })
}
finally {
    if (-not $process.HasExited) {
        try { $process.CloseMainWindow() | Out-Null } catch { }
        Start-Sleep -Seconds 2
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    }
}

$evidence.deletedRoot = -not (Test-Path $markers.root)
$evidence.deletedNested = -not (Test-Path $markers.nested)
$evidence.protectedKept = Test-Path $markers.protected
$evidence.nestedDirRemoved = -not (Test-Path $nestedDir)
$evidence.success = $evidence.deletedRoot -and $evidence.deletedNested -and
    $evidence.protectedKept -and $evidence.nestedDirRemoved

if ($EvidencePath) {
    $evidence | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $EvidencePath -Encoding utf8
}
$evidence | ConvertTo-Json -Depth 6
if (-not $evidence.success) { exit 1 } else { exit 0 }
