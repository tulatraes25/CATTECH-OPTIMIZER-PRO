param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string]$Section = "Limpieza",
    [string]$OutFile = ""
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

function Get-AppWindow {
    param($Process, [int]$TimeoutSeconds)
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $Process.Id)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $window = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition)
        if ($window) { return $window }
        Start-Sleep -Milliseconds 400
    }
    throw "No se encontro la ventana."
}

function Find-ByNameContains {
    param($Root, [string]$Text, [string]$ControlType = "")
    $all = $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($element in $all) {
        $type = $element.Current.ControlType.ProgrammaticName -replace 'ControlType\.', ''
        if ($ControlType -and $type -ne $ControlType) { continue }
        if ($element.Current.Name -and $element.Current.Name.Contains($Text)) { return $element }
    }
    return $null
}

function Invoke-Element {
    param($Element)
    $pattern = $null
    if ($Element.TryGetCurrentPattern(
            [System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
        $pattern.Invoke()
        return $true
    }
    if ($Element.TryGetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern, [ref]$pattern)) {
        $pattern.Toggle()
        return $true
    }
    if ($Element.TryGetCurrentPattern(
            [System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) {
        $pattern.Select()
        return $true
    }
    return $false
}

$process = Start-Process -FilePath $Exe -PassThru
try {
    $window = Get-AppWindow -Process $process -TimeoutSeconds 60
    Start-Sleep -Seconds 2

    $navButton = Find-ByNameContains -Root $window -Text $Section -ControlType "Button"
    if (-not $navButton) { throw "No se encontro el boton de navegacion '$Section'." }
    [void](Invoke-Element $navButton)
    Start-Sleep -Seconds 2

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("section=$Section")
    $all = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($element in $all) {
        $type = $element.Current.ControlType.ProgrammaticName -replace 'ControlType\.', ''
        $name = ($element.Current.Name -replace '\s+', ' ').Trim()
        if (-not $name) { continue }
        if ($type -in @('Button', 'CheckBox', 'DataItem', 'Text', 'ComboBox', 'TabItem')) {
            $lines.Add("$type`t$name")
        }
    }
    if ($OutFile) { $lines | Set-Content -LiteralPath $OutFile -Encoding utf8 }
    $lines
}
finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
}
