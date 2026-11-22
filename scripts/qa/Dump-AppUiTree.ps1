param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string]$OutFile = "",
    [int]$WindowTimeoutSeconds = 60
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing

$process = Start-Process -FilePath $Exe -PassThru
try {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $process.Id)
    $deadline = (Get-Date).AddSeconds($WindowTimeoutSeconds)
    $window = $null
    while ((Get-Date) -lt $deadline) {
        $window = $root.FindFirst(
            [System.Windows.Automation.TreeScope]::Children, $condition)
        if ($window) { break }
        Start-Sleep -Milliseconds 400
    }
    if (-not $window) { throw "No se encontro la ventana de la app en $WindowTimeoutSeconds s." }
    Start-Sleep -Seconds 2

    $lines = [System.Collections.Generic.List[string]]::new()
    $all = $window.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    $lines.Add("window=$($window.Current.Name) pid=$($process.Id) elements=$($all.Count)")
    foreach ($element in $all) {
        $type = $element.Current.ControlType.ProgrammaticName -replace 'ControlType\.', ''
        $name = ($element.Current.Name -replace '\s+', ' ').Trim()
        if (-not $name) { continue }
        if ($type -in @('Button', 'CheckBox', 'DataItem', 'Text', 'ComboBox', 'TabItem',
                'Edit', 'Document', 'Group', 'ProgressBar', 'RadioButton')) {
            $lines.Add("$type`t$name")
        }
    }
    if ($OutFile) { $lines | Set-Content -LiteralPath $OutFile -Encoding utf8 }
    $lines
}
finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
}
