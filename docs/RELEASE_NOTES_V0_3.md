# CATTECH OPTIMIZER PRO v0.3.0 — Notas de la versión

**Fecha:** 2026-11-22  
**Runtime:** .NET 10 LTS · paquete autocontenido `win-x64` (no requiere instalar .NET)  
**Plataforma:** Windows 11 y Windows 10 LTSC (ediciones admitidas oficialmente por .NET 10)  
**Estado:** release publicada. Build y pruebas automatizadas en Windows CI sobre el SHA exacto; verificación física en equipos reales según `docs/QA_REAL_SMOKE.md`.

## Descarga e integridad

- `CATTECH-Optimizer-Pro-v0.3.0-win-x64.zip` (autocontenido, x64)
- `CATTECH-Optimizer-Pro-v0.3.0-win-x64.sha256.txt` (verificar antes de usar)
- Atestación de procedencia de GitHub sobre el ZIP publicado

## Objetivo técnico

CATTECH v0.3 refuerza diagnósticos, reparaciones con consentimiento, evidencia e informes para técnicos informáticos, manteniendo siempre: diagnosticar, respaldar, solicitar confirmación, intervenir de forma reversible y documentar.

## Cambios incluidos

- **BSOD y DumpChk** read-only cuando está disponible, con límites de tiempo, fuentes identificadas y separación entre síntomas y atribuciones causales.
- **Comprobaciones de Windows Update, SFC/DISM y restauración** con distinción entre lectura/vista previa y tareas mutables que requieren permisos y consentimiento explícito; nunca desactivar Defender/firewall como optimización.
- **Evidence Engine / Decision Center** y pasaporte técnico de revisión: conclusiones conservadoras sujetas a contexto de cliente, sesión y componente; no equivalen a certificación física.
- **SMART y sensores** con manejo de resultados no disponibles. `smartctl` sigue siendo externo y NO va incluido en el ZIP.
- **Informes y PDF** más seguros: datos actuales por cliente, IDs desambiguados, publicación atómica con checksum, protección de archivos existentes y mensajes sin rutas privadas.
- **Limpieza acotada y validada**, con protección de temporales recientes/ocultos/sistema y auditoría de resultados.
- **Cliente/equipo**: Cancelar recupera solamente un guardado exitoso previo o descarta un borrador nuevo y revoca datos asíncronos obsoletos.

## Runtime y plataformas

El paquete utiliza .NET 10 LTS y se distribuye autocontenido para Windows x64. La compatibilidad reconocida por Microsoft para .NET 10 incluye versiones vigentes de Windows 11 y Windows 10 Enterprise/IoT LTSC; las ediciones Windows 10 comunes ya fuera de soporte no cuentan con cobertura oficial garantizada. Consultar https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md .

## Certificación del paquete

- GitHub Actions: restore, build Release sin warnings, pruebas xUnit completas dos veces sobre el commit exacto, generación `win-x64` self-contained, verificación de `ProductVersion`/`FileVersion` del EXE, contenido del ZIP y SHA-256.
- El pipeline de release exige que el SHA publicado sea el HEAD de `main` y que el tag no exista previamente.
- **Verificación física recomendada:** navegación WPF, sensores, WMI/SMBIOS, SMART, DXGI, BSOD, exportación PDF con Edge, permisos y protección de informes. El protocolo está en `docs/QA_REAL_SMOKE.md`.

## Instalación

1. Descargar el ZIP y su SHA-256.
2. Verificar el hash (`Get-FileHash .\CATTECH-Optimizer-Pro-v0.3.0-win-x64.zip -Algorithm SHA256`).
3. Extraer en una carpeta y ejecutar `Cattech.Optimizer.Pro.UI.exe` como administrador.

## Historial

- v0.2.2 — informes sincronizados y memoria GPU por DXGI
- v0.2.1 — corrección de crash XAML en Cliente/equipo
- v0.2.0 — SMART completo y hardware en tiempo real
- v0.1.1 — MVP estabilizado

Ver `CHANGELOG.md` para el detalle completo.
