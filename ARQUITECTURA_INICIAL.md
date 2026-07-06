# Arquitectura Inicial - CATTECH OPTIMIZER PRO

Versión: 0.1 (MVP)  
Fecha: 2024  
Objetivo: Definir estructura, tecnologías y módulos del proyecto.

---

## 1. Justificación Tecnológica

### WPF vs WinUI: Decisión Final

**Decisión: WPF (Windows Presentation Foundation)**

| Criterio | WPF | WinUI 3 |
|----------|-----|---------|
| **Estabilidad** | Maduro (2006+), testeado en producción | Más nuevo, menos battle-tested |
| **Compatibilidad** | .NET Framework 4.7.2+ / .NET 6-8 | Solo .NET 5+ / .NET Core |
| **Portabilidad** | Windows 10/11 (con .NET 8) | Solo Windows 10 1803+ |
| **Controls** | Amplio ecosistema (Telerik, DevExpress, MaterialDesign) | Controles limitados aún |
| **Documentación** | Extensa, muchos ejemplos | Creciente pero menor |
| **Community** | Gran base de usuarios, Stack Overflow extenso | Comunidad creciente |
| **Learning Curve** | Familiar para desarrolladores .NET | Requiere aprendizaje nuevo |
| **MVP suitability** | Excelente para MVP rápido | Mejor para apps modernas a futuro |

**Conclusión**: WPF es la elección correcta para un MVP técnico portable. Permite:
- **Objetivo principal**: Windows 10/11 (donde .NET 8 funciona oficialmente)
- **Compatibilidad futura limitada**: Windows 7/8/8.1 mediante rama legacy con .NET Framework 4.8 (no en MVP)
- Ecosistema maduro de controles
- Facilidad para desarrolladores .NET experimentados
- Mejor rendimiento en máquinas con menos recursos
- Portabilidad: puede ejecutarse sin instalación (carpeta portable)

### Stack Tecnológico Definido

```
┌─────────────────────────────────────────────────────────────┐
│                    CATTECH OPTIMIZER PRO                     │
├─────────────────────────────────────────────────────────────┤
│  UI Layer:        WPF (.NET 8) + XAML                      │
│  MVVM Framework:  CommunityToolkit.Mvvm                     │
│  DI Container:    Microsoft.Extensions.DependencyInjection  │
│  Logging:         Serilog +Seq                              │
│  PDF Generation:  QuestPDF (open source, MIT)               │
│  JSON:            System.Text.Json                          │
│  Hardware:        LibreHardwareMonitorLib (NuGet)           │
│  Diagnostics:     smartctl.exe (externo)                    │
│  Installer:       Inno Setup o WiX                          │
└─────────────────────────────────────────────────────────────┘
```

---

## 2. Estructura de Carpetas

```
CATTECH-OPTIMIZER-PRO/
│
├── src/
│   ├── Cattech.Optimizer.Pro.sln                    # Solución principal
│   │
│   ├── Cattech.Optimizer.Pro.Core/                  # Lógica de negocio
│   │   ├── Cattech.Optimizer.Pro.Core.csproj
│   │   ├── Interfaces/
│   │   │   ├── IHardwareService.cs
│   │   │   ├── IDiagnosticService.cs
│   │   │   ├── IOptimizationService.cs
│   │   │   ├── IRepairService.cs
│   │   │   ├── IReportService.cs
│   │   │   ├── IBackupService.cs
│   │   │   └── ILogger.cs
│   │   ├── Models/
│   │   │   ├── Hardware/
│   │   │   │   ├── CpuInfo.cs
│   │   │   │   ├── GpuInfo.cs
│   │   │   │   ├── DiskInfo.cs
│   │   │   │   ├── MemoryInfo.cs
│   │   │   │   ├── MotherboardInfo.cs
│   │   │   │   └── SystemInfo.cs
│   │   │   ├── Diagnostics/
│   │   │   │   ├── SmartResult.cs
│   │   │   │   ├── HealthStatus.cs
│   │   │   │   └── DiagnosticReport.cs
│   │   │   ├── Reports/
│   │   │   │   ├── CompanyInfo.cs
│   │   │   │   ├── TechnicianInfo.cs
│   │   │   │   ├── ClientInfo.cs
│   │   │   │   └── ServiceReport.cs
│   │   │   └── Configuration/
│   │   │       ├── AppSettings.cs
│   │   │       └── UserPreferences.cs
│   │   ├── Services/
│   │   │   ├── Hardware/
│   │   │   │   ├── HardwareDetectionService.cs
│   │   │   │   └── SensorService.cs
│   │   │   ├── Diagnostics/
│   │   │   │   ├── SmartDiagnosticService.cs
│   │   │   │   └── SystemHealthService.cs
│   │   │   └── Helpers/
│   │   │       ├── ProcessRunner.cs
│   │   │       └── FileHelper.cs
│   │   └── Constants/
│   │       ├── Paths.cs
│   │       └── ErrorMessages.cs
│   │
│   ├── Cattech.Optimizer.Pro.Infrastructure/        # Acceso a datos e integración
│   │   ├── Cattech.Optimizer.Pro.Infrastructure.csproj
│   │   ├── Hardware/
│   │   │   ├── LhmHardwareProvider.cs              # LibreHardwareMonitor integration
│   │   │   ├── WmiHardwareProvider.cs              # Fallback a WMI
│   │   │   └── SmartProvider.cs                    # smartctl.exe wrapper
│   │   ├── Data/
│   │   │   ├── AppSettingsRepository.cs
│   │   │   └── ReportRepository.cs
│   │   ├── External/
│   │   │   ├── SmartctlWrapper.cs
│   │   │   └── ProcessExecutor.cs
│   │   └── Templates/
│   │       ├── ReportHtmlTemplate.cs
│   │       └── ReportPdfTemplate.cs
│   │
│   ├── Cattech.Optimizer.Pro.UI/                    # Interfaz gráfica WPF
│   │   ├── Cattech.Optimizer.Pro.UI.csproj
│   │   ├── App.xaml
│   │   ├── App.xaml.cs
│   │   ├── MainWindow.xaml
│   │   ├── MainWindow.xaml.cs
│   │   ├── Views/
│   │   │   ├── Dashboard/
│   │   │   │   ├── DashboardView.xaml
│   │   │   │   └── DashboardView.xaml.cs
│   │   │   ├── Hardware/
│   │   │   │   ├── HardwareInfoView.xaml
│   │   │   │   └── HardwareInfoView.xaml.cs
│   │   │   ├── Diagnostics/
│   │   │   │   ├── DiagnosticsView.xaml
│   │   │   │   └── DiagnosticsView.xaml.cs
│   │   │   ├── Optimization/
│   │   │   │   ├── OptimizationView.xaml
│   │   │   │   └── OptimizationView.xaml.cs
│   │   │   ├── Reports/
│   │   │   │   ├── ReportsView.xaml
│   │   │   │   └── ReportsView.xaml.cs
│   │   │   ├── Settings/
│   │   │   │   ├── CompanySettingsView.xaml
│   │   │   │   └── CompanySettingsView.xaml.cs
│   │   │   └── Client/
│   │   │       ├── ClientInfoView.xaml
│   │   │       └── ClientInfoView.xaml.cs
│   │   ├── ViewModels/
│   │   │   ├── MainViewModel.cs
│   │   │   ├── DashboardViewModel.cs
│   │   │   ├── HardwareViewModel.cs
│   │   │   ├── DiagnosticsViewModel.cs
│   │   │   ├── OptimizationViewModel.cs
│   │   │   ├── ReportsViewModel.cs
│   │   │   └── SettingsViewModel.cs
│   │   ├── Converters/
│   │   │   ├── BoolToVisibilityConverter.cs
│   │   │   ├── StatusToColorConverter.cs
│   │   │   └── ByteArrayToImageConverter.cs
│   │   ├── Resources/
│   │   │   ├── Styles/
│   │   │   │   ├── AppStyles.xaml
│   │   │   │   └── ControlStyles.xaml
│   │   │   ├── Themes/
│   │   │   │   ├── LightTheme.xaml
│   │   │   │   └── DarkTheme.xaml
│   │   │   ├── Images/
│   │   │   │   ├── logo.png
│   │   │   │   └── icons/
│   │   │   └── Fonts/
│   │   └── Controls/
│   │       ├── StatusIndicator.xaml
│   │       ├── ProgressCard.xaml
│   │       └── DiagnosticPanel.xaml
│   │
│   └── Cattech.Optimizer.Pro.Installer/            # Proyecto de instalación
│       ├── setup.iss                               # Inno Setup script
│       └── Resources/
│           ├── cattech-icon.ico
│           └── banner.bmp
│
├── tools/
│   ├── smartctl/                                   # Binario smartctl.exe
│   │   ├── smartctl.exe
│   │   └── smartctl.exe.config
│   ├── 7zip/                                       # Compresión (opcional)
│   │   └── 7z.exe
│   └── README.md                                   # Licencias de herramientas externas
│
├── config/
│   ├── appsettings.json                           # Configuración de la aplicación
│   ├── company.json                               # Datos de empresa (se crea al configurar)
│   └── bugcheckcodes.json                         # Base de datos de códigos BSOD (futuro)
│
├── templates/
│   ├── report-html.html                           # Plantilla HTML para informes
│   ├── report-pdf.html                            # Plantilla para conversión PDF
│   └── email-template.html                        # Plantilla de email (futuro)
│
├── docs/
│   ├── AUDITORIA_REFERENCIAS.md                   # Auditoría de herramientas
│   ├── ARQUITECTURA_INICIAL.md                    # Este documento
│   ├── SEGURIDAD.md                               # Reglas de seguridad
│   ├── ROADMAP.md                                 # Plan de desarrollo
│   ├── CHANGELOG.md                               # Historial de cambios
│   └── guia-tecnico.md                            # Guía para técnicos (futuro)
│
├── scripts/
│   ├── build.ps1                                  # Script de build
│   ├── publish.ps1                                # Script de publicación
│   └── test.ps1                                   # Script de tests
│
├── tests/
│   ├── Cattech.Optimizer.Pro.Core.Tests/
│   │   ├── Cattech.Optimizer.Pro.Core.Tests.csproj
│   │   ├── Services/
│   │   │   ├── HardwareDetectionServiceTests.cs
│   │   │   └── SmartDiagnosticServiceTests.cs
│   │   └── Models/
│   │       └── SystemInfoTests.cs
│   └── Cattech.Optimizer.Pro.Infrastructure.Tests/
│       ├── Cattech.Optimizer.Pro.Infrastructure.Tests.csproj
│       └── External/
│           └── SmartctlWrapperTests.cs
│
├── output/                                        # Builds generados (en .gitignore)
│   ├── Debug/
│   └── Release/
│
├── .gitignore
├── .editorconfig
├── README.md
├── LICENSE                                        # MIT License
└── CATTECH-OPTIMIZER-PRO.sln                     # Solución en raíz (opcional)
```

---

## 3. Arquitectura de la Aplicación

### Patrón: MVVM + Services + Dependency Injection

```
┌─────────────────────────────────────────────────────────────────────┐
│                         PRESENTATION LAYER                         │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │                    WPF Application (XAML)                    │  │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐    │  │
│  │  │Dashboard │  │Hardware  │  │Diagnost. │  │Reports   │    │  │
│  │  │  View    │  │  View    │  │  View    │  │  View    │    │  │
│  │  └────┬─────┘  └────┬─────┘  └────┬─────┘  └────┬─────┘    │  │
│  │       │              │              │              │          │  │
│  │  ┌────┴─────┐  ┌────┴─────┐  ┌────┴─────┐  ┌────┴─────┐    │  │
│  │  │Dashboard │  │Hardware  │  │Diagnost. │  │Reports   │    │  │
│  │  │ViewModel │  │ViewModel │  │ViewModel │  │ViewModel │    │  │
│  │  └────┬─────┘  └────┬─────┘  └────┬─────┘  └────┬─────┘    │  │
│  └───────┼──────────────┼──────────────┼──────────────┼──────────┘  │
│          │              │              │              │              │
├──────────┼──────────────┼──────────────┼──────────────┼──────────────┤
│          │          BUSINESS LAYER      │              │              │
│  ┌───────┴──────────────┴──────────────┴──────────────┴──────────┐  │
│  │                    Services (Interfaces)                      │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐        │  │
│  │  │  IHardware   │  │ IDiagnostic  │  │ IReport      │        │  │
│  │  │   Service    │  │   Service    │  │   Service    │        │  │
│  │  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘        │  │
│  │         │                 │                  │                │  │
│  │  ┌──────┴───────┐  ┌──────┴───────┐  ┌──────┴───────┐        │  │
│  │  │ IOptimization│  │ IBackup      │  │ IRepair      │        │  │
│  │  │   Service    │  │  Service     │  │  Service     │        │  │
│  │  └──────────────┘  └──────────────┘  └──────────────┘        │  │
│  └───────────────────────────────────────────────────────────────┘  │
│                              │                                      │
├──────────────────────────────┼──────────────────────────────────────┤
│                         INFRASTRUCTURE LAYER                        │
│  ┌────────────────────────────┴────────────────────────────────┐   │
│  │                  Concrete Implementations                   │   │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │   │
│  │  │     Lhm      │  │   Smartctl   │  │    Quest     │     │   │
│  │  │  Hardware    │  │   Wrapper    │  │     PDF      │     │   │
│  │  │  Provider    │  │              │  │   Generator  │     │   │
│  │  └──────────────┘  └──────────────┘  └──────────────┘     │   │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │   │
│  │  │     Wmi      │  │  Process     │  │   Settings   │     │   │
│  │  │  Hardware    │  │   Executor   │  │  Repository  │     │   │
│  │  │  Provider    │  │              │  │              │     │   │
│  │  └──────────────┘  └──────────────┘  └──────────────┘     │   │
│  └────────────────────────────────────────────────────────────┘   │
│                              │                                      │
├──────────────────────────────┼──────────────────────────────────────┤
│                           EXTERNAL TOOLS                            │
│  ┌────────────────────────────┴────────────────────────────────┐   │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │   │
│  │  │  smartctl    │  │   7zip.exe   │  │  PowerShell  │     │   │
│  │  │   .exe       │  │              │  │    Scripts   │     │   │
│  │  └──────────────┘  └──────────────┘  └──────────────┘     │   │
│  └────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
```

### Flujo de Datos

```
┌─────────────────────────────────────────────────────────────────┐
│                    Flujo de Operación Típica                    │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  1. Configuración Inicial                                      │
│     ┌─────────────┐                                            │
│     │ Técnico     │──► CompanySettingsView                     │
│     │ configura   │    ├── Logo de empresa                     │
│     │ empresa     │    ├── Datos de contacto                   │
│     └─────────────┘    └── Guarda en company.json              │
│                                                                 │
│  2. Nuevo Servicio                                             │
│     ┌─────────────┐                                            │
│     │ Técnico     │──► ClientInfoView                          │
│     │ ingresa     │    ├── Datos del cliente                   │
│     │ cliente     │    ├── Datos del equipo                    │
│     └─────────────┘    └── Asociado al reporte                 │
│                                                                 │
│  3. Diagnóstico                                                │
│     ┌─────────────┐                                            │
│     │ Hardware    │──► HardwareDetectionService                │
│     │ Detection   │    ├── LibreHardwareMonitorLib              │
│     └──────┬──────┘    └── WMI Fallback                        │
│            │                                                    │
│     ┌──────▼──────┐                                            │
│     │ Diagnostic  │──► SmartDiagnosticService                  │
│     │ Execution   │    ├── Ejecuta smartctl.exe                │
│     └──────┬──────┘    └── Parsea salida JSON                  │
│            │                                                    │
│     ┌──────▼──────┐                                            │
│     │ Results     │──► Muestra en DiagnosticsView              │
│     │ Display     │    └── Gráficos y tablas                   │
│     └─────────────┘                                            │
│                                                                 │
│  4. Optimización (con confirmación)                            │
│     ┌─────────────┐                                            │
│     │ Optimization│──► OptimizationView                         │
│     │ Selection   │    ├── Limpieza temporal                    │
│     └──────┬──────┘    ├── Optimización visual                 │
│            │             └── Punto de restauración              │
│     ┌──────▼──────┐                                            │
│     │ Confirm     │──► Diálogo de confirmación                 │
│     │ Dialog      │    └── "¿Estás seguro?"                    │
│     └──────┬──────┘                                            │
│            │                                                    │
│     ┌──────▼──────┐                                            │
│     │ Execute     │──► Ejecuta con logging                     │
│     │ Actions     │    └── Registra cada paso                  │
│     └─────────────┘                                            │
│                                                                 │
│  5. Generación de Informe                                      │
│     ┌─────────────┐                                            │
│     │ Report Gen  │──► ReportService                           │
│     │             │    ├── Recopila datos                      │
│     │             │    ├── Genera HTML                         │
│     │             │    └── Convierte a PDF (QuestPDF)          │
│     └──────┬──────┘                                            │
│            │                                                    │
│     ┌──────▼──────┐                                            │
│     │ Save/Print  │──► Opciones de salida                      │
│     │             │    ├── Guardar en carpeta                  │
│     │             │    ├── Abrir en navegador                  │
│     │             │    └── Enviar por email (futuro)           │
│     └─────────────┘                                            │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 4. Módulos del MVP v0.1

### 4.1 Configuración de Empresa/Técnico

**Propósito**: Permitir al técnico configurar sus datos y logo para incluir en informes.

**Componentes**:
- `CompanySettingsView.xaml`: Interfaz de configuración
- `SettingsViewModel.cs`: Lógica de presentación
- `AppSettingsRepository.cs`: Persistencia de configuración
- `company.json`: Archivo de configuración

**Datos a configurar**:
```json
{
  "company": {
    "name": "CATTECH Services",
    "logo": "base64_encoded_logo",
    "address": "...",
    "phone": "...",
    "email": "...",
    "website": "..."
  },
  "technician": {
    "name": "Juan Pérez",
    "id": "TEC-001",
    "email": "juan@cattech.com"
  }
}
```

### 4.2 Datos de Cliente/Equipo

**Propósito**: Registrar información del cliente y equipo a diagnosticar.

**Componentes**:
- `ClientInfoView.xaml`: Formulario de cliente
- `ClientViewModel.cs`: Lógica de presentación
- `ClientInfo.cs`: Modelo de datos

**Datos a registrar**:
- Cliente: Nombre, email, teléfono, dirección
- Equipo: Marca, modelo, número de serie, OS, fecha de compra
- Motivo de visita
- Notas del técnico

### 4.3 Diagnóstico Básico

**Propósito**: Recopilar información del estado del sistema.

**Componentes**:
- `DiagnosticsView.xaml`: Panel de diagnóstico
- `DiagnosticsViewModel.cs`: Orquestación
- `HardwareDetectionService.cs`: Detección de hardware
- `SmartDiagnosticService.cs`: Análisis SMART

**Diagnósticos incluidos**:
1. **Información del Sistema**: OS, versión, build, arquitectura
2. **CPU**: Modelo, núcleos, temperatura, uso
3. **RAM**: Total, disponible, velocidad, slots
4. **Discos**: Capacidad, salud SMART, temperatura
5. **GPU**: Modelo, memoria, temperatura
6. **Red**: Adaptadores, velocidad, estado

### 4.4 Programas de Inicio

**Propósito**: Mostrar y permitir gestionar programas que inician con Windows.

**Componentes**:
- Sección en `OptimizationView.xaml`
- `StartupManagerService.cs` (futuro)

**Funcionalidad MVP**:
- Lista de programas de inicio
- Información de cada programa
- **NO** permite deshabilitar en MVP (solo informativo)

### 4.5 Limpieza Segura de Temporales

**Propósito**: Eliminar archivos temporales de forma segura.

**Componentes**:
- `CleanupService.cs`: Lógica de limpieza
- Integración en `OptimizationView.xaml`

**Limpiezas incluidas**:
1. `%TEMP%` del usuario
2. `%TEMP%` del sistema
3. `C:\Windows\Temp`
4. `C:\Windows\Prefetch` (opcional)
5. Papelera de reciclaje (opcional)
6. Caché de Windows Update (post-instalación)

**Seguridad**:
- Lista blanca de carpetas a limpiar
- NUNCA elimina archivos del usuario
- Muestra resumen antes de eliminar
- Crea punto de restauración previamente

### 4.6 Optimización Visual Segura

**Propósito**: Aplicar mejoras visuales sin riesgo.

**Componentes**:
- `VisualOptimizationService.cs`
- Integración en `OptimizationView.xaml`

**Optimizaciones incluidas**:
1. Desactivar animaciones innecesarias
2. Desactivar transparencias
3. Ajustar rendimiento visual
4. Desactivar efectos de sombra
5. Optimizar fuentes

**NO incluye** (requiere confirmación avanzada):
- Cambios en registro del sistema
- Modificación de servicios
- Desactivación de funciones de Windows

### 4.7 Punto de Restauración

**Propósito**: Crear punto de restauración antes de cambios.

**Componentes**:
- `RestorePointService.cs`
- Uso de API de Windows: `SystemRestore` WMI

**Implementación**:
```csharp
// Crear punto de restauración
ManagementClass restorePoint = new ManagementClass("SystemRestore");
ManagementBaseObject inParams = restorePoint.GetMethodParameters("CreateRestorePoint");
inParams["Description"] = "CATTECH Optimizer - Pre-optimization";
inParams["RestorePointType"] = 12; // MODIFY_SETTINGS
inParams["EventType"] = 100; // BEGIN_SYSTEM_CHANGE
ManagementBaseObject outParams = restorePoint.InvokeMethod("CreateRestorePoint", inParams, null);
```

### 4.8 Generación de Informe HTML/PDF

**Propósito**: Crear informes profesionales para el cliente.

**Componentes**:
- `ReportService.cs`: Orquestación
- `ReportHtmlTemplate.cs`: Generación HTML
- `ReportPdfTemplate.cs`: Conversión PDF (QuestPDF)
- `report-html.html`: Plantilla HTML

**Contenido del informe**:
1. **Encabezado**: Logo de empresa, datos del técnico
2. **Datos del cliente**: Nombre, equipo, fecha
3. **Resumen ejecutivo**: Estado general, hallazgos principales
4. **Detalles de hardware**: Especificaciones completas
5. **Resultados de diagnóstico**: SMART, salud del sistema
6. **Acciones realizadas**: Limpieza, optimización, puntos de restauración
7. **Recomendaciones**: Próximos pasos sugeridos
8. **Pie de página**: Términos, garantía, contacto

**Formatos de salida**:
- HTML: Para vista previa y envío por email
- PDF: Para impresión y archivado

---

## 5. Consideraciones de Licencia

### Licencias de Dependencias

| Dependencia | Licencia | Uso | Restricciones |
|-------------|----------|-----|---------------|
| .NET 8 | MIT | Runtime | Ninguna |
| WPF | - | UI Framework | Parte de .NET |
| CommunityToolkit.Mvvm | MIT | MVVM | Ninguna |
| QuestPDF | MIT | PDF Generation | Ninguna |
| Serilog | Apache 2.0 | Logging | Attribution |
| LibreHardwareMonitorLib | MPL 2.0 | Hardware | Attribution, share changes |
| smartctl.exe | GPL-2.0 | SMART | Binario externo, no vincular |
| 7zip.exe | LGPL | Compression | Binario externo |

### Licencia del Proyecto

**Decisión: MIT License**

```text
MIT License

Copyright (c) 2024 CATTECH

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

### Requisitos de Atribución

1. **LibreHardwareMonitorLib**: Incluir attribution en About/Settings
2. **Serilog**: Incluir en ThirdPartyNotices.txt
3. **QuestPDF**: Incluir en ThirdPartyNotices.txt
4. **smartctl.exe**: Distribuir binario con licencia GPL-2.0

---

## 6. Decisiones Técnicas Principales

### 6.1 Framework UI: WPF

**Justificación**:
- Compatibilidad con Windows 7/8/10/11
- Ecosistema maduro de controles
- Mejor para aplicaciones técnicas/empresariales
- Soporte nativo para DPI scaling
- Portabilidad (puede ser portable sin instalación)

### 6.2 Patrón: MVVM

**Justificación**:
- Separación clara de responsabilidades
- Facilidad para testing unitario
- Reutilización de ViewModels
- Comunidad activa (CommunityToolkit.Mvvm)

### 6.3 Generación de PDF: QuestPDF

**Justificación**:
- Open source (MIT)
- API fluida y moderna
- Sin dependencias de wkhtmltopdf o PhantomJS
- Buena documentación
- Rendimiento aceptable

### 6.4 Hardware Monitoring: LibreHardwareMonitorLib

**Justificación**:
- Licencia MPL 2.0 (compatible)
- Disponible como NuGet
- Acceso a todos los sensores
- Actualizaciones regulares
- Documentación oficial

### 6.5 SMART Diagnostics: smartctl.exe

**Justificación**:
- Estándar de la industria
- Soporte completo para todos los discos
- Binario independiente (sin integrar código GPL)
- Salida JSON para parsing

### 6.6 Persistencia: JSON Files

**Justificación**:
- Sin base de datos requerida
- Fácil de backups y transportar
- Legible para debugging
- suficiente para configuración y reportes

### 6.7 Logging: Serilog

**Justificación**:
- Estructurado (JSON)
- Múltiples sinks (archivo, consola, Seq)
- Alto rendimiento
- Open source

---

## 7. Requisitos del Sistema

### Para Ejecutar CATTECH OPTIMIZER PRO (MVP v0.1)

- **SO**: Windows 10 (1809+) / Windows 11 — **requisito obligatorio para .NET 8**
- **.NET**: .NET 8 Desktop Runtime (se instala automáticamente si no está presente)
- **RAM**: 512 MB mínimo, 1 GB recomendado
- **Disco**: 100 MB para instalación + espacio para reportes
- **Permisos**: Administrador (para diagnóstico completo)
- **Resolución**: 1280x720 mínimo, 1920x1080 recomendado

### Compatibilidad Futura (Post-MVP)

- **Windows 7/8/8.1**: Posible soporte mediante rama legacy separada que use .NET Framework 4.8 en lugar de .NET 8
- **No está planificado para v0.1**: Requeriría mantenimiento de dos codebases

### Para Desarrollo

- **IDE**: Visual Studio 2022 17.8+ o Rider 2023.3+
- **SDK**: .NET 8 SDK
- **Windows ADK**: Solo para futura creación de USB Rescue

---

## 8. Roadmap Técnico

### v0.1 (MVP) - 6-8 semanas
- [ ] Setup de proyecto y estructura
- [ ] Configuración de empresa/técnico
- [ ] Formulario de cliente/equipo
- [ ] Detección de hardware básica (CPU, RAM, GPU, discos)
- [ ] Información de discos: espacio libre, tipo HDD/SSD (si Windows lo informa)
- [ ] **NO** implementar SMART completo (se posterga a v0.2)
- [ ] Limpieza de temporales (solo usuario, con confirmación)
- [ ] Optimización visual (con backup y confirmación)
- [ ] Punto de restauración
- [ ] Generación de informe HTML
- [ ] Generación de informe PDF
- [ ] Build portable (sin instalador)

### v0.2 - 4 semanas
- [ ] SMART completo con histórico
- [ ] Monitoreo de temperaturas en tiempo real
- [ ] Reportes comparativos (antes/después)
- [ ] Exportación a JSON/CSV
- [ ] Modo oscuro

### v0.3 - 4 semanas
- [ ] Análisis básico de minidumps (BSOD)
- [ ] Reparación de Windows Update
- [ ] Reparación de archivos del sistema (SFC/DISM)
- [ ] Historial de servicios por cliente

### v0.4 - 6 semanas
- [ ] CATTECH Preserve (backup de configuración)
- [ ] Restauración de configuración
- [ ] Backup de drivers
- [ ] Sincronización entre equipos

### v0.5 - 8 semanas
- [ ] CATTECH Rescue USB
- [ ] Creación de USB bootable con WinPE
- [ ] Integración de Memtest86+
- [ ] Herramientas de rescate offline

### v1.0 - 4 semanas
- [ ] Suite completa estable
- [ ] Instalador profesional
- [ ] Actualizaciones automáticas
- [ ] Documentación completa
- [ ] Soporte multi-idioma

---

## 9. Próximos Pasos Inmediatos

1. **Inicializar repositorio Git**
2. **Crear proyecto .NET 8 WPF en Visual Studio**
3. **Instalar dependencias NuGet iniciales**
4. **Implementar estructura de carpetas Core/Infrastructure/UI**
5. **Crear primer ViewModel y View de prueba**
6. **Configurar Serilog para logging**
7. **Implementar detección de hardware básica**
8. **Crear formulario de configuración de empresa**

---

*Documento generado como parte de la planificación de CATTECH OPTIMIZER PRO*
