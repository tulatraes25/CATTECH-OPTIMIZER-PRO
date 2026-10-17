# CATTECH OPTIMIZER PRO v0.3.0 — Notas del candidato (NO PUBLICADO)

**Estado:** paquete candidato para controles CI, sin GitHub Release/tag oficial ni certificación sobre equipos Windows reales.
**Última versión publicada:** v0.2.2; no reemplazarla hasta completar todas las puertas de aceptación.

## Objetivo técnico
CATTECH v0.3 refuerza diagnósticos, reparaciones con consentimiento, evidencia e informes para técnicos informáticos, manteniendo siempre: diagnosticar, respaldar, solicitar confirmación, intervenir de forma reversible y documentar.

## Cambios incluidos en el código candidato
- BSOD y DumpChk read-only cuando está disponible, con límites de tiempo, fuentes identificadas y separación entre síntomas y atribuciones causales.
- Comprobaciones de Windows Update, SFC/DISM y restauración con distinción entre lectura/preview y tareas mutables que requieren permisos y consentimiento explícito; nunca desactivar Defender/firewall como optimización.
- Evidence Engine / Decision Center y pasaporte técnico de revisión: conclusiones conservadoras sujetas a contexto de cliente, sesión y componente; no equivalen a certificación física.
- SMART y sensores con manejo de resultados no disponibles. `smartctl` sigue siendo externo y NO va incluido en el ZIP.
- Informes y PDF más seguros, datos actuales por cliente, IDs desambiguados, protección de archivos existentes y mensajes sin rutas privadas.
- Limpieza acotada y validada, con protección de temporales recientes/ocultos/sistema y auditoría de resultados.
- Formulario Cliente/equipo: Cancelar recupera solamente un guardado exitoso previo o descarta un borrador nuevo y revoca datos asíncronos obsoletos.

## Runtime y plataformas
El candidato utiliza .NET 10 LTS y genera un paquete autocontenido Windows x64. La compatibilidad reconocida por Microsoft para .NET 10 incluye versiones vigentes de Windows 11 y Windows 10 Enterprise/IoT LTSC; las ediciones Windows 10 comunes ya fuera de soporte NO tienen cobertura oficial garantizada. Consultar https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md .

## Puertas de certificación antes de publicar
- GitHub Actions: restore, Release build, pruebas xUnit completas dos veces sobre el commit exacto, generación de win-x64 self-contained, EXE ProductVersion/FileVersion, contenido ZIP y SHA-256.
- Validación manual en equipos Windows 11 y Windows 10 LTSC: navegación WPF, sensores, WMI/SMBIOS, SMART, DXGI, BSOD, Edge PDF, permisos, protección de informes y seguridad de operaciones.
- Auditoría final de ramas y dependencias; protección efectiva de `main` (issue #75); decisión explícita de release desde SHA exacto.
- **Hasta entonces, candidato en desarrollo: NO distribuir como versión final ni declarar 100% certificado.**
