# Real Windows Smoke QA — Protocolo

## Objetivo

Validar una Release REAL de CATTECH OPTIMIZER PRO en equipos Windows reales, descubriendo diferencias que los tests automatizados (fakes) no detectan: permisos, WMI, LibreHardwareMonitor, sensores, smartctl, Edge PDF, rutas, WPF, escalado, estado persistido.

## Alcance

- P.3.1 (actual): toolkit + verificación del paquete oficial publicado.
- P.3.2 (pendiente): ejecución manual real por perfil de hardware (Desktop / Notebook).

## Qué NO cubre

- Corrección de bugs funcionales (P.3.1 solo documenta hallazgos).
- Pruebas destructivas.
- Automatización de UI (sin Selenium/WinAppDriver en P.3.1).

## Prohibiciones (durante smoke base)

NO ejecutar:

- Limpieza de temporales (borrado)
- Optimización visual (aplicación)
- Desactivación de programas de inicio
- Creación de puntos de restauración
- Self-tests SMART (cortos o extendidos)
- Cualquier escritura en discos fuera de los archivos de evidencia QA
- Uso de datos reales de clientes: usar SIEMPRE datos ficticios

El smoke NO es una sesión de trabajo real: es validación de la aplicación publicada.

## Diferencia entre tests automatizados y smoke real

- Tests automatizados: 945+ unit/integration con fakes, ejecutados por CI en runners efímeros.
- Smoke real: observación manual de la aplicación publicada en una máquina real, con hardware real o ausencia del mismo.

No asumir que "los tests pasan" implica que la app funciona en una PC real.

---

## Paso 0 — Integridad de la Release oficial

Descargar los assets oficiales (GitHub CLI disponible):

```powershell
gh release download v0.2.0 `
  --repo tulatraes25/CATTECH-OPTIMIZER-PRO `
  -p "CATTECH-Optimizer-Pro-v0.2.0-win-x64.zip" `
  -p "CATTECH-Optimizer-Pro-v0.2.0-win-x64.sha256.txt" `
  -D output/qa-smoke/source
```

Verificar el checksum oficial (fixture v0.2.0):

```powershell
Get-FileHash output/qa-smoke/source/CATTECH-Optimizer-Pro-v0.2.0-win-x64.zip -Algorithm SHA256
```

Esperado para v0.2.0:

```
797AD589438F22D16F0DDC8BF79C02B1BCF3B0E7851B6DC7FA32998C587A50BF
```

Debe coincidir con el contenido de `CATTECH-Optimizer-Pro-v0.2.0-win-x64.sha256.txt`. Si no coincide: FAIL, no continuar.

## Paso 1 — Extraer en carpeta aislada

```powershell
Expand-Archive output/qa-smoke/source/CATTECH-Optimizer-Pro-v0.2.0-win-x64.zip -DestinationPath output/qa-smoke/v0.2.0
```

La raíz de `output/qa-smoke/v0.2.0` debe contener directamente `Cattech.Optimizer.Pro.UI.exe` (sin carpeta anidada extra).

## Paso 2 — Collector automático (sin PII)

```powershell
powershell -ExecutionPolicy Bypass -File scripts/qa/Collect-SmokeEvidence.ps1 `
  -PackagePath output/qa-smoke/v0.2.0 `
  -Label baseline-v0.2.0
```

Genera `output/qa-smoke/smoke-evidence-baseline-v0.2.0-{timestamp}.json` y `.md` con:

- Package: FileVersion, ProductVersion, EXE SHA-256, archivos críticos, smartctl bundled (esperado No), config válida
- Environment: Windows/build/arquitectura, RAM, CPU (sin seriales), PowerShell, Admin
- Automatic checks: Package baseline PASS

Esperado: exit 0. Revisar el JSON/MD: NO debe contener username, hostname, `C:\Users`, IP, MAC ni seriales.

### Evidencia — schema v2

- SchemaVersion: 2 (v1 persistía `PackagePath` y Label raw; v2 elimina ambas cosas)
- **NO se persiste** `PackagePath` ni `OutputDirectory` (solo se usan localmente para validación y mensajes de consola)
- **Label persistido**: versión sanitizada (solo letras/dígitos/`-`/`_`)
- **Timestamps**: UTC real (ISO 8601 con offset, ej. `2026-08-10T19:45:12.3456789+00:00`); los nombres de archivo derivan del UTC
- **Exit code**: `0` solo si `PackageBaseline == PASS`; `1` si el baseline falla (EXE ausente, critical file ausente o herramientas.json inválido) — la evidencia se genera antes del exit siempre que sea posible
- `smartctl` ausente NO produce FAIL (dependencia externa esperada)

## Paso 3 — Checklist manual (ver docs/QA_SMOKE_RESULT_TEMPLATE.md para registrar)

Cada prueba admite: **PASS / FAIL / N/D / NO EJECUTADO**. No inventar PASS cuando el hardware no existe (batería en desktop → N/D; SPD no disponible → N/D si CATTECH lo maneja correctamente).

### Arranque normal
1. Ejecutar CATTECH sin elevar.
2. Verificar: abre, ventana visible, sin crash, footer v0.2.0, Home muestra "CATTECH OPTIMIZER PRO v0.2.0", navegación responde.

### Navegación (abrir cada sección, sin acciones)
Home · Configuración · Cliente/equipo · Diagnóstico · Programas de inicio · Limpieza · Optimización · Punto de restauración · Discos SMART · Hardware · Informes

### Configuración (datos FICTICIOS)
- Empresa: `CATTECH QA` · Técnico: `Técnico QA`
- Guardar, cerrar/reabrir, verificar persistencia. No usar datos reales.

### Cliente/equipo (datos ficticios)
- Cliente: `Cliente QA` · Equipo: `Equipo QA` · Motivo: `Prueba Smoke`

### Diagnóstico (read-only)
- Ejecutar diagnóstico rápido; verificar que termina, UI no queda bloqueada, datos plausibles, alertas controladas, resultado guardable. No aplicar recomendaciones.

### Programas de inicio
- Solo ANÁLISIS (listado). NO desactivar ni restaurar.

### Limpieza
- Solo ESCANEAR temporales (si es read-only). NO borrar.

### Optimización
- Solo ANALIZAR ajustes. NO aplicar.

### Punto de restauración
- Abrir y verificar estado (solo si es read-only). NO crear punto de restauración.

### Hardware live
1. Abrir Hardware → "Actualizar una vez".
2. Registrar qué pestañas tienen datos.
3. Iniciar monitoreo ~10-15 s, detener.
4. Navegar a otra sección, regresar; verificar que no quedó sesión duplicada/stale.
5. Actualizar inventario estático.
- Registrar por pestaña: Temperaturas, CPU/GPU, Memoria GPU, Batería, RAM SPD, Inventario — PASS / N/D / FAIL.

### SMART sin smartctl
- Abrir Discos SMART sin smartctl disponible: sin crash, mensaje claro, estado No disponible, resto de la app funcional → PASS si la degradación es correcta. No instalar smartctl para el smoke.

### SMART con smartctl natural
- Solo si smartctl YA está disponible: Verificar smartctl, Listar discos, Analizar SMART (read-only). Verificar estados y que un disco que falla no aborte el resto.
- PROHIBIDO en smoke base: tests cortos y extendidos.

### Informe
- Generar HTML con datos QA ficticios: verificar que incluye v0.2.0, empresa QA, y que estados Unknown/NotAvailable no aparecen como "sano".
- Generar PDF si Edge está disponible: verificar que es PDF real (cabecera `%PDF`) y abre.

### Reinicio de app
- Cerrar normalmente, reabrir: sin crash, configuración QA persiste, sin monitoreo fantasma, sin operaciones pendientes.

### Admin opcional
- Segunda apertura como administrador, solo para observar read-only (Hardware, SMART, Diagnóstico, estado Restore Point). NO ejecutar acciones con elevación. Registrar: Admin smoke PASS / FAIL / NO EJECUTADO.

### Escalado y UI
- Verificar: textos no cortados, botones accesibles, tabs legibles, dialogs dentro de pantalla, scroll funcional. Probar 100%; si es posible 125%/150%. Registrar qué se probó.

### Mensajes de error
- Registrar cualquier excepción visible, dialog confuso, stack trace, pantalla vacía, spinner infinito, botón deshabilitado o mensaje que afirma éxito sin datos. Documentar pasos de reproducción (no corregir en P.3.1).

## Estado actual

- P.3.1: ✅ toolkit + baseline package verification
- P.3.2: ⏳ pruebas manuales reales por perfil (Desktop / Notebook)

No marcar Desktop PASS / Notebook PASS hasta ejecutarlos realmente.

## Hallazgos conocidos de v0.2.0

### SMOKE-B1-001 — Crash XAML al navegar a Cliente/equipo

- **Detected in**: v0.2.0 official release (smoke P.3.2-B1, 2/2 reproducible, sin admin)
- **Severity**: Blocker
- **Cause**: `ClientEquipmentView.xaml` usaba `{StaticResource InvertBoolConverter}` sin registrarlo en sus `UserControl.Resources` → `XamlParseException` al instanciar la vista (Event Viewer: .NET Runtime ID 1026)
- **Fix status**: Fixed on main / pending verification (se corrigió también QuickDiagnosticView, VisualOptimizationView y CompanySettingsView por la misma auditoría; se agregaron smoke tests STA en `tests/.../UI/XamlViewSmokeTests.cs`)
- **Nota**: la Release oficial v0.2.0 continúa conteniendo el defecto (no se modifica la release histórica); el fix se distribuirá en v0.2.1

### QA-META-001 — Trazabilidad de build del artefacto publicado

- **Release v0.2.0 tag source**: `8d54e8a527f0183314eb1812ffb226f7ac1d5255`
- **EXE ProductVersion del asset publicado**: `0.2.0+13c0c26...` (metadata SourceRevisionId)
- **Clasificación**: Low / trazabilidad de build
- **Impacto**: no se observó impacto funcional
- **Estado preventivo**: ✅ mitigado para futuras releases mediante P.2.3 (provenance guard: todo candidate generado por P.2 debe tener `ProductVersion == version + source SHA` completo; falla antes de empaquetar)
- **Descripción observable**: el artefacto publicado fue generado en el proceso de release anterior a la automatización actual y su metadata SourceRevisionId no coincide con el commit final etiquetado (no se afirma una causa definitiva)
- **Nota**: v0.2.0 histórica sigue mostrando `0.2.0+13c0c26...` y NO se modifica; la mitigación aplica a releases futuras. El pipeline P.2 empaqueta `source_ref` explícito (P.2.3 exige coincidencia de provenance)

## Matriz P.3.2 (recomendada, para futura ejecución)

| Perfil | Hardware ideal | Observaciones |
|--------|----------------|---------------|
| A — Desktop | Windows 10/11 x64, GPU integrada o dedicada, sin batería | Batería → N/D |
| B — Notebook | Windows 10/11, batería, sensores portátiles | Batería/SPD pueden tener datos |
| C — Con SMART (opcional) | smartctl disponible naturalmente | Solo análisis read-only |
