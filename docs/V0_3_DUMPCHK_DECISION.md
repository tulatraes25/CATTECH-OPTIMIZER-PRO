# v0.3 — Decisión de análisis de minidumps (C.3)

## Diagnóstico técnico

Windows genera volcados **de kernel** por errores BugCheck y volcados **de usuario** para ciertas aplicaciones. Ambos pueden utilizar la extensión `.dmp`, pero no se debe asumir que comparten el mismo formato ni que tienen la misma semántica.

CATTECH no construirá un lector binario propio de volcados del kernel a partir de `MINIDUMP_HEADER`/streams de minidumps de usuario: ese supuesto no está validado y podría producir interpretaciones falsas.

## Base técnica oficial

- Microsoft: Small Memory Dump (Kernel), con BugCheck, parámetros y módulos:
  https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/small-memory-dump
- Microsoft: Minidump Files (Win32, user-mode):
  https://learn.microsoft.com/en-us/windows/win32/debug/minidump-files
- Microsoft: DumpChk:
  https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/dumpchk
- Microsoft: Use Dumpchk.exe to check a memory dump:
  https://learn.microsoft.com/en-us/troubleshoot/windows-server/performance/use-dumpchk-to-check-memory-dump-file

## Estrategia aprobada

1. **C.2 completado**: archivos .dmp (solo metadatos), Event Log 1001/41 (solo evidencia validada).
2. **C.3.1**: parser puro `DumpCheckOutputParser` con fixtures sintéticos, sin ejecución externa.
3. **C.3.2**: localizador seguro y opt-in de `dumpchk.exe` instalado mediante Microsoft Debugging Tools; no incluir binarios de terceros en la Release.
4. **C.3.3**: runner aislado, timeout estricto, ejecución explícita del técnico, sin `-y` ni descargas automáticas de símbolos; argumentos separados (`ProcessStartInfo.ArgumentList`), sin shell, stdout/stderr limitados; no registrar salida completa.
5. **C.3.4**: correlación conservadora con eventos del mismo equipo, mostrando la fuente y el nivel de confianza, sin afirmar causalidad del módulo citado.
6. **C.4**: UI de evidencia primero; análisis del dump solo si la herramienta confiable está disponible y el técnico lo solicita.

## Criterios de seguridad

- Nunca copiar, subir o compartir volcados automáticamente (pueden contener datos personales).
- No ejecutar herramientas descargadas automáticamente.
- No enviar símbolos ni dumps a servicios externos sin consentimiento explícito (preferencia inicial: sin red).
- Ausencia de dumpchk o error de acceso = N/D, no FAIL general ni causa inventada.
- `Finished dump check` indica archivo **probablemente legible**, no integridad absoluta.
- `MODULE_NAME`, `IMAGE_NAME`, `SYMBOL_NAME` son hechos del debugger, **no prueba de driver culpable**.
- Una pantalla azul no debe confundirse con un reinicio por pérdida de energía.
- No ejecutar SFC/DISM ni reset de Windows Update durante un análisis de evidencia.

## Gate

- Tests de parser con salida sintética Win32/Kernel, corrupción, ausencia de marcador, parámetros, UTF-8 y privacidad.
- CI Windows verde por commit.
- Sin tests ni cambios destructivos en máquinas reales hasta completar desarrollo y auditoría en GitHub.
