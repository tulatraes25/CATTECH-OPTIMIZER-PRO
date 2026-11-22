# Prueba end-to-end de Programas de inicio via UI Automation.
# Crea un valor de prueba en HKCU\...\Run, lo desactiva desde la UI real
# (análisis -> filtro -> selección -> confirmar -> backup) y luego lo restaura
# desde el panel de Backups. Verifica el registro en cada paso.
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string]$EvidencePath = ""
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
if (-not ('Win32Mouse' -as [type])) {
    Add-Type -Namespace '' -Name 'Win32Mouse' -MemberDefinition @"
[DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
[DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
"@
}

$runKey = 'Software\Microsoft\Windows\CurrentVersion\Run'
$disabledRunKey = 'Software\CATTECH\OptimizerPro\DisabledStartup\Run'
$valueName = 'CATTECH_QA_UI_' + (Get-Random -Maximum 99999)

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

function Get-ControlType {
    param($Element)
    $Element.Current.ControlType.ProgrammaticName -replace 'ControlType\.', ''
}

function Find-ByNameContains {
    param($Root, [string]$Text, [string]$ControlType = "")
    foreach ($element in (Get-Descendants $Root)) {
        if ($ControlType -and (Get-ControlType $element) -ne $ControlType) { continue }
        if ($element.Current.Name -and $element.Current.Name.Contains($Text)) { return $element }
    }
    return $null
}

function Find-ByType {
    param($Root, [string]$ControlType)
    foreach ($element in (Get-Descendants $Root)) {
        if ((Get-ControlType $element) -eq $ControlType) { return $element }
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

function Set-EditText {
    param($Element, [string]$Text)
    $pattern = $null
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$pattern)) {
        $pattern.SetValue($Text); return $true
    }
    return $false
}

function Set-ForegroundWindow {
    param($Element)
    $hwnd = [IntPtr]$Element.Current.NativeWindowHandle
    if ($hwnd -ne [IntPtr]::Zero) {
        [void][Win32Mouse]::SetForegroundWindow($hwnd)
        Start-Sleep -Milliseconds 300
    }
}

function Click-ElementCenter {
    param($Element)
    $rect = $Element.Current.BoundingRectangle
    if ($rect.Width -le 0 -or $rect.Height -le 0) { throw 'Elemento sin area clicable.' }
    $x = [int]($rect.X + $rect.Width / 2)
    $y = [int]($rect.Y + $rect.Height / 2)
    [void][Win32Mouse]::SetCursorPos($x, $y)
    Start-Sleep -Milliseconds 150
    [Win32Mouse]::mouse_event(0x0002, 0, 0, 0, 0) # left down
    Start-Sleep -Milliseconds 60
    [Win32Mouse]::mouse_event(0x0004, 0, 0, 0, 0) # left up
}

function Get-UiTexts {
    param($Root)
    @(foreach ($element in (Get-Descendants $Root)) {
            $type = Get-ControlType $element
            $name = ($element.Current.Name -replace '\s+', ' ').Trim()
            if ($name -and $type -in @('Text', 'CheckBox', 'Button', 'Edit')) { $name }
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

function Find-ParentRow {
    param($Element)
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $current = $Element
    for ($i = 0; $i -lt 8; $i++) {
        $parent = $walker.GetParent($current)
        if (-not $parent) { break }
        if ((Get-ControlType $parent) -in @('DataItem', 'ListItem', 'Custom', 'Group')) {
            return $parent
        }
        $current = $parent
    }
    return $null
}

function Find-RowContainingText {
    param($Root, [string]$Text, [string]$RowControlType = 'DataItem')
    foreach ($row in (Get-Descendants $Root)) {
        if ((Get-ControlType $row) -ne $RowControlType) { continue }
        foreach ($child in (Get-Descendants $row)) {
            if ((Get-ControlType $child) -eq 'Text' -and
                $child.Current.Name -and $child.Current.Name.Contains($Text)) {
                return $row
            }
        }
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

function Test-RunValueExists {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($runKey)
    try { return $null -ne $key.GetValue($valueName, $null) }
    finally { if ($key) { $key.Close() } }
}

$evidence = [ordered]@{
    timestamp       = (Get-Date).ToString('o')
    exe             = $Exe
    valueName       = $valueName
    analysisOk      = $false
    filtered        = $false
    selectedViaUi   = $false
    dialogConfirmed = $false
    disableStatus   = @()
    valueRemoved    = $false
    backupCreated   = $false
    backupsPanelClicked = $false
    backupNameVisible = $false
    restoreButtonFound = $false
    restoreClicked  = $false
    valueRestored   = $false
    success         = $false
}

# Valor de prueba bajo HKCU (nunca HKLM): ruta real para que el análisis lo vea.
$testCommand = "$env:SystemRoot\System32\notepad.exe"
$run = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($runKey)
try { $run.SetValue($valueName, $testCommand, [Microsoft.Win32.RegistryValueKind]::String) }
finally { $run.Close() }

$process = Start-Process -FilePath $Exe -PassThru
try {
    $window = Get-AppWindow -Process $process
    Start-Sleep -Seconds 2

    $nav = Find-ByNameContains -Root $window -Text 'Programas de inicio' -ControlType 'Button'
    if (-not $nav) { throw 'No se encontro el boton Programas de inicio.' }
    [void](Invoke-Element $nav)
    Start-Sleep -Seconds 2

    if (-not (Wait-TextMatch -Root $window -Patterns @('^Análisis completado') -TimeoutSeconds 60)) {
        $analyze = Find-ByNameContains -Root $window -Text 'Analizar' -ControlType 'Button'
        if ($analyze) { [void](Invoke-Element $analyze) }
        if (-not (Wait-TextMatch -Root $window -Patterns @('^Análisis completado') -TimeoutSeconds 120)) {
            throw 'El análisis de inicio no termino.'
        }
    }
    $evidence.analysisOk = $true

    # La fila puede quedar fuera de foco/virtualizada: se localiza por el texto
    # del nombre y el checkbox se elige por alineacion vertical con la fila.
    $nameEl = Find-ByNameContains -Root $window -Text $valueName -ControlType 'Text'
    if (-not $nameEl) { throw "No se encontro la fila $valueName en la grilla." }
    $evidence.rowVisible = $true
    $rowY = $nameEl.Current.BoundingRectangle.Y

    $check = $null
    foreach ($candidate in (Get-Descendants $window)) {
        if ((Get-ControlType $candidate) -ne 'CheckBox') { continue }
        if (-not $candidate.Current.IsEnabled) { continue }
        if ([Math]::Abs($candidate.Current.BoundingRectangle.Y - $rowY) -le 8) {
            $check = $candidate
            break
        }
    }
    if (-not $check) { throw 'No se encontro el checkbox alineado con la fila.' }
    Set-ForegroundWindow $window
    Click-ElementCenter $check
    Start-Sleep -Milliseconds 800
    $toggle = $null
    if ($check.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$toggle)) {
        $evidence.selectedViaUi = $toggle.Current.ToggleState -eq 'On'
    }
    $disableButton = Find-ByNameContains -Root $window -Text 'Desactivar seleccionados' -ControlType 'Button'
    if (-not $disableButton) { throw 'No se encontro el boton Desactivar.' }
    [void](Invoke-Element $disableButton)

    $evidence.dialogConfirmed = Confirm-ModalDialog -Root $window

    $disableTexts = Wait-TextMatch -Root $window -Patterns @(
        '^Desactivadas \d+', '^Fallaron \d+', '^Error durante') -TimeoutSeconds 120
    if (-not $disableTexts) { throw 'La desactivación no termino.' }
    $evidence.disableStatus = @($disableTexts | Where-Object { $_ -match '^(Desactivadas|Fallaron|Error)' })

    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline -and (Test-RunValueExists)) { Start-Sleep -Milliseconds 500 }
    $evidence.valueRemoved = -not (Test-RunValueExists)

    $backupKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($disabledRunKey)
    try {
        $evidence.backupCreated = $null -ne $backupKey -and
            @($backupKey.GetValueNames() | Where-Object { $_ -like "*$valueName*" }).Count -gt 0
    }
    finally { if ($backupKey) { $backupKey.Close() } }

    # Restaurar desde el panel Backups: boton de la tarjeta que muestra nuestro nombre.
    $backupsButton = Find-ByNameContains -Root $window -Text 'Backups' -ControlType 'Button'
    if ($backupsButton) {
        [void](Invoke-Element $backupsButton)
        $evidence.backupsPanelClicked = $true
        Start-Sleep -Seconds 2
        $backupNameEl = Find-ByNameContains -Root $window -Text $valueName -ControlType 'Text'
        $evidence.backupNameVisible = $null -ne $backupNameEl
        $restoreButton = $null
        if ($backupNameEl) {
            $backupY = $backupNameEl.Current.BoundingRectangle.Y
            $bestDelta = [double]::MaxValue
            foreach ($candidate in (Get-Descendants $window)) {
                try {
                    if ((Get-ControlType $candidate) -ne 'Button' -or $candidate.Current.Name -ne 'Restaurar') { continue }
                    if (-not $candidate.Current.IsEnabled) { continue }
                    $candidateY = $candidate.Current.BoundingRectangle.Y
                    if ([double]::IsInfinity($candidateY)) { continue }
                    $delta = $candidateY - $backupY
                    # La tarjeta del backup: nombre arriba y boton Restaurar debajo.
                    if ($delta -ge 0 -and $delta -le 160 -and $delta -lt $bestDelta) {
                        $restoreButton = $candidate
                        $bestDelta = $delta
                    }
                } catch { }
            }
        }
        $evidence.restoreButtonFound = $null -ne $restoreButton
        if ($restoreButton) {
            [void](Invoke-Element $restoreButton)
            $evidence.restoreClicked = $true
            $deadline = (Get-Date).AddSeconds(20)
            while ((Get-Date) -lt $deadline -and -not (Test-RunValueExists)) {
                Start-Sleep -Milliseconds 500
            }
        }
    }    $evidence.valueRestored = Test-RunValueExists
    $evidence.success = $evidence.analysisOk -and $evidence.valueRemoved -and
        $evidence.backupCreated -and $evidence.valueRestored
}
finally {
    if (-not $process.HasExited) {
        try { $process.CloseMainWindow() | Out-Null } catch { }
        Start-Sleep -Seconds 2
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    }
    # Limpieza del valor de prueba (nunca tocar otras entradas)
    try {
        $cleanup = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($runKey, $true)
        if ($cleanup) { $cleanup.DeleteValue($valueName, $false); $cleanup.Close() }
        $cleanupDisabled = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($disabledRunKey, $true)
        if ($cleanupDisabled) {
            foreach ($name in $cleanupDisabled.GetValueNames()) {
                if ($name -like "*$valueName*") { $cleanupDisabled.DeleteValue($name, $false) }
            }
            $cleanupDisabled.Close()
        }
    } catch { }
}

if ($EvidencePath) {
    $evidence | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $EvidencePath -Encoding utf8
}
$evidence | ConvertTo-Json -Depth 6
if (-not $evidence.success) { exit 1 } else { exit 0 }
