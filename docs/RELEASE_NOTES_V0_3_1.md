# CATTECH OPTIMIZER PRO v0.3.1 — Notas de la versión

**Fecha:** 2026-11-22  
**Runtime:** .NET 10 LTS · paquete autocontenido `win-x64` (no requiere instalar .NET)  
**Plataforma:** Windows 11 y Windows 10 LTSC (ediciones admitidas oficialmente por .NET 10)  
**Estado:** release publicada. Corrección funcional sobre v0.3.0: las pantallas de limpieza, programas de inicio, punto de restauración, optimización visual, SMART e informes ahora ejecutan realmente sus acciones.

## Descarga e integridad

**Instalador (recomendado):**
- `CATTECH-Optimizer-Pro-v0.3.1-Setup.exe` — asistente con instalación "Solo para mí" (sin UAC) o "Para todos los usuarios", accesos directos y desinstalador.
- `CATTECH-Optimizer-Pro-v0.3.1-Setup.exe.sha256.txt` (verificar antes de usar)

**Portable:**
- `CATTECH-Optimizer-Pro-v0.3.1-win-x64.zip` (autocontenido, x64)
- `CATTECH-Optimizer-Pro-v0.3.1-win-x64.sha256.txt` (verificar antes de usar)

## UX para técnicos avanzados

- Barra de estado global con admin, build de Windows, runtime, ruta de datos y estado de smartctl; panel de información con rutas y modo de instalación.
- Panel técnico de inicio con sistema, contadores de almacenamiento y acciones rápidas; F5 refresca la sección actual y Ctrl+0..9 navega.
- Configuración › Herramientas externas para fijar/autodetectar `smartctl.exe`, probarlo y guardar la configuración.
- Diagnóstico con estado parcial y errores por fuente; Hardware con exportar CSV; SMART con atributos crudos, sesiones y exportación.
- Programas de inicio con filtros reales, detalle técnico y abrir ubicación; Limpieza con historial; Pantallas azules con carpeta de dumps y copiar análisis; Reparaciones con comandos copiables.
- Informes con estado técnico, archivos dañados listados y secciones reales de historial.
- Errores inesperados registrados en `logs/app-YYYYMMDD.log` sin cerrar la aplicación.
## Qué se corrigió (botones que "no hacían nada")

- **Limpieza de temporales**: borra también subcarpetas, sin la retención de 7 días que dejaba casi todo intacto, y pide confirmación mostrando archivos y MB reales. Incluye el **vaciado de la Papelera de reciclaje** con la API de Windows (antes figuraba como no disponible).
- **Programas de inicio**: la desactivación abre la clave del registro en modo escritura (antes el borrado fallaba siempre por abrirla en solo lectura), permite seleccionar cualquier entrada de terceros, conserva el tipo de valor del registro y restaura desde el panel de Backups.
- **Punto de restauración**: se eliminó la comprobación del servicio `srservice` (no existe en Windows 10/11) que mantenía "Crear" deshabilitado para siempre; ahora se intenta la creación y se reporta el error real de Windows.
- **Optimización visual**: los checkboxes quedan seleccionados, "Aplicar" ejecuta y "Restaurar" restaura el backup correcto, incluidos valores binarios.
- **SMART**: verificación automática de smartctl al abrir, errores reales en lugar de "análisis completado" vacío, y estado visible.
- **Informes**: carga automática al abrir, "Abrir carpeta" funciona aunque la carpeta no existiera, y avisa si el informe o PDF ya no está.

## Verificación

- Build Release sin warnings y **2055 tests** xUnit (0 fallas, 0 omitidos) sobre el SHA exacto.
- Pruebas end-to-end con UI Automation sobre la aplicación publicada:
  - Limpieza real: 16.845 archivos / 260 MB detectados, 162 MB liberados, archivos en uso omitidos, nombres protegidos conservados y subcarpeta vacía eliminada.
  - Programas de inicio: entrada de prueba en `HKCU\...\Run` desactivada desde la UI (valor eliminado + backup creado) y restaurada desde el panel de Backups.
  - Navegación por las 14 pantallas sin crashes.
- La verificación física en equipos de clientes sigue siendo recomendable; el protocolo está en `docs/QA_REAL_SMOKE.md`.

## Instalación

**Con instalador (recomendado):** descargar el `Setup.exe` y su `.sha256.txt`, verificar el hash, ejecutar el asistente y elegir el modo. La desinstalación conserva `data\`, `reports\` y `backups\`.

**Portable:** descargar el ZIP, verificar el hash, extraer en una carpeta escribible y ejecutar `Cattech.Optimizer.Pro.UI.exe` como administrador.

## Historial

- v0.3.0 — BSOD, reparaciones con consentimiento, Evidence Engine, pasaporte técnico y migración a .NET 10
- v0.2.2 — informes sincronizados y memoria GPU por DXGI
- v0.2.1 — corrección de crash XAML en Cliente/equipo
- v0.2.0 — SMART completo y hardware en tiempo real
- v0.1.1 — MVP estabilizado

Ver `CHANGELOG.md` para el detalle completo.
