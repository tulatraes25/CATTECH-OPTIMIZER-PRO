# CATTECH v0.3 — SFC/DISM SPEC/Kiro

## Alcance R.2.1

Se dispone de un catálogo **fijo y no ejecutable** de cinco operaciones.

| Operación | Acción | Impacto | Red posible |
|---|---|---|---|
| DISM /CheckHealth | Comprobar estado registrado del almacén | Inspección | No |
| DISM /ScanHealth | Analizar almacén de componentes | Inspección | No |
| SFC /verifyonly | Verificar archivos protegidos | Inspección | No |
| DISM /RestoreHealth | Reparar almacén | Modifica Windows | Sí |
| SFC /scannow | Verificar **y reparar** archivos protegidos | Modifica Windows | No prevista |

Los cinco planes exigen elevación y confirmación explícita. **ExecutionAvailable=false** hasta completar servicio de ejecución, registros, backup/evidencia y QA.

## Plan de siguientes fases

- R.2.2: preflight de plataforma y rol administrador, herramienta confiable `System32`, no PATH, argumentos en `ProcessStartInfo.ArgumentList`.
- R.2.3: proceso con timeout y cancelación segura, registros mínimos sin PII; no retener todo el output en archivos.
- R.2.4: acciones modificadoras tras backup y segunda confirmación explícita, etiquetadas como de alto impacto.
- R.2.5: verificar resultado posterior, salida/código; no anunciar reparado solo por exit=0.
- R.2.6: UI y pruebas WPF/CI.
- R.2.7: certificación real sólo al terminar la fase completa en GitHub.

## Prohibiciones

No ejecutamos `SFC /scannow` como si fuera solo lectura. `DISM /RestoreHealth` podría descargar archivos según configuración, por eso requiere aviso. No ejecutar scripts PowerShell de reparaciones genéricas, no desactivar protección de Windows, no bajar ejecutables.
