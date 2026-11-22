# Captura pantallas de la aplicación publicada para revisión visual de UX.
# Navega con atajos Ctrl+0..9 y clic real (ventana traída al frente).
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string]$OutDir = "output\qa-ui\screens"
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

if (-not ('Win32Ux' -as [type])) {
    Add-Type -Namespace '' -Name 'Win32Ux' -MemberDefinition @"
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
[DllImport("user32.dll")] public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);
[DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
[DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
[DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, int extra);
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
public static void BringToFront(IntPtr hWnd) {
    ShowWindow(hWnd, 3); // maximize
    SetWindowPos(hWnd, (IntPtr)(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0040); // TOPMOST|NOSIZE|NOMOVE|SHOWWINDOW
    SwitchToThisWindow(hWnd, true);
    SetForegroundWindow(hWnd);
}
public static void NormalZOrder(IntPtr hWnd) {
    SetWindowPos(hWnd, (IntPtr)(-2), 0, 0, 0, 0, 0x0001 | 0x0002); // NOTOPMOST
}
public static void ClickAt(int x, int y) {
    SetCursorPos(x, y);
    mouse_event(0x0002, 0, 0, 0, 0);
    System.Threading.Thread.Sleep(50);
    mouse_event(0x0004, 0, 0, 0, 0);
}
"@
}

$resolvedOut = [System.IO.Path]::GetFullPath($OutDir)
New-Item -ItemType Directory -Force $resolvedOut | Out-Null

$plan = @(
    @{ Name = 'inicio'; Section = 'Inicio'; Shortcut = '0'; Marker = 'Diagnóstico, optimización' },
    @{ Name = 'diagnostico'; Section = 'Diagnóstico'; Shortcut = '4'; Marker = 'Diagnóstico' },
    @{ Name = 'hardware'; Section = 'Hardware'; Shortcut = '3'; Marker = 'Hardware' },
    @{ Name = 'smart'; Section = 'Discos SMART'; Shortcut = '2'; Marker = 'Discos' },
    @{ Name = 'limpieza'; Section = 'Limpieza'; Shortcut = '9'; Marker = 'Limpieza Segura' },
    @{ Name = 'inicio-programas'; Section = 'Programas de inicio'; Shortcut = '8'; Marker = 'Desactivar seleccionados' },
    @{ Name = 'optimizacion'; Section = 'Optimización'; Shortcut = $null; Marker = 'Optimización' },
    @{ Name = 'restauracion'; Section = 'Punto de restauración'; Shortcut = $null; Marker = 'restauración' },
    @{ Name = 'informes'; Section = 'Informes'; Shortcut = $null; Marker = 'Informe' },
    @{ Name = 'configuracion'; Section = 'Configuración'; Shortcut = $null; Marker = 'Configuración' }
)

function Get-Descendants {
    param($Root)
    $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
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

function Find-NavRow {
    param($Root, [string]$Section)
    foreach ($item in (Get-Descendants $Root)) {
        if ((Get-ControlType $item) -ne 'DataItem') { continue }
        foreach ($child in (Get-Descendants $item)) {
            if ((Get-ControlType $child) -eq 'Text' -and $child.Current.Name -eq $Section) {
                return $item
            }
        }
    }
    return $null
}

function Wait-Marker {
    param($Root, [string]$Marker, [int]$TimeoutSeconds = 15)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Find-ByNameContains -Root $Root -Text $Marker) { return $true }
        Start-Sleep -Milliseconds 400
    }
    return $false
}

function Navigate {
    param($Root, [string]$Section, [string]$Shortcut, [string]$Marker)
    $handle = [IntPtr]$Root.Current.NativeWindowHandle
    for ($attempt = 0; $attempt -lt 4; $attempt++) {
        [Win32Ux]::BringToFront($handle)
        Start-Sleep -Milliseconds 400
        if ($Shortcut) {
            [System.Windows.Forms.SendKeys]::SendWait('^' + $Shortcut)
        }
        else {
            $row = Find-NavRow -Root $Root -Section $Section
            if ($row) {
                $rect = $row.Current.BoundingRectangle
                [Win32Ux]::ClickAt([int]($rect.X + $rect.Width / 2), [int]($rect.Y + $rect.Height / 2))
            }
        }
        if (Wait-Marker -Root $Root -Marker $Marker) { return $true }
        Start-Sleep -Milliseconds 400
    }
    return $false
}

function Save-Capture {
    param([IntPtr]$Handle, [string]$Path)
    $rect = New-Object Win32Ux+RECT
    if (-not [Win32Ux]::GetWindowRect($Handle, [ref]$rect)) { return $false }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) { return $false }
    $bitmap = New-Object System.Drawing.Bitmap($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0,
            (New-Object System.Drawing.Size($width, $height)))
        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
        return $true
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$process = Start-Process -FilePath $Exe -PassThru
try {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $process.Id)
    $deadline = (Get-Date).AddSeconds(60)
    $window = $null
    while ((Get-Date) -lt $deadline) {
        $window = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition)
        if ($window) { break }
        Start-Sleep -Milliseconds 400
    }
    if (-not $window) { throw "No se encontró la ventana de la aplicación." }

    $handle = [IntPtr]$window.Current.NativeWindowHandle
    # Esperar a que la UI esté completamente construida.
    for ($i = 0; $i -lt 60; $i++) {
        if (Find-ByNameContains -Root $window -Text 'Limpieza' -ControlType 'Text') { break }
        Start-Sleep -Milliseconds 500
    }
    [Win32Ux]::BringToFront($handle)
    Start-Sleep -Seconds 1

    foreach ($item in $plan) {
        $ok = Navigate -Root $window -Section $item.Section -Shortcut $item.Shortcut -Marker $item.Marker
        Start-Sleep -Seconds 2
        $file = Join-Path $resolvedOut ("{0}.png" -f $item.Name)
        if (Save-Capture -Handle $handle -Path $file) {
            Write-Host ("captura [{0}] navegacion={1}: {2}" -f $item.Name, $ok, $file)
        }
        else {
            Write-Host ("falló captura: {0}" -f $item.Name)
        }
    }
    [Win32Ux]::NormalZOrder($handle)
}
finally {
    if (-not $process.HasExited) {
        try { $process.CloseMainWindow() | Out-Null } catch { }
        Start-Sleep -Seconds 2
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    }
}
