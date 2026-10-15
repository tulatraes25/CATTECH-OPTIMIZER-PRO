# CATTECH OPTIMIZER PRO — v0.3 BSOD & Repair

## Objetivo

Agregar diagnóstico de crashes y reparaciones comunes manteniendo el principio CATTECH:

> diagnosticar primero, respaldar segundo, reparar tercero, optimizar cuarto y documentar al final.

La rama de desarrollo de v0.3 nace desde la Release certificada v0.2.2. Durante el desarrollo, `main` permanece como baseline estable.

## Principios

- Lectura antes que escritura.
- No atribuir causalidad a un driver sin evidencia suficiente.
- No ejecutar SFC, DISM, reset de Windows Update ni otras reparaciones sin confirmación explícita.
- Toda reparación debe registrar comando, timestamps, exit code y resumen interpretable.
- Nunca desactivar Defender, Windows Update o servicios de seguridad como “optimización”.
- Los datos de dumps y logs pueden contener información sensible; evitar persistir rutas de usuario innecesarias.

## Fases

### C.1 — Núcleo BSOD

1. Modelo `CrashInfo`.
2. Catálogo local versionado de Bug Check Codes.
3. `IBugCheckDecoder` + implementación determinista.
4. Tests de códigos conocidos, desconocidos, entradas inválidas y duplicadas.

### C.2 — Descubrimiento read-only de crashes

1. Enumerar `C:\Windows\Minidump\*.dmp` sin modificar archivos.
2. Consultar eventos BugCheck/WER como fuente complementaria.
3. Distinguir “dump encontrado”, “dump inaccesible”, “sin dumps”.
4. Persistir historial CATTECH sin copiar dumps por defecto.

### C.3 — Análisis de minidump

1. Definir estrategia soportada para extracción de Bug Check y módulos.
2. Separar hechos observados de heurísticas.
3. Mostrar nivel de confianza.
4. Nunca presentar “driver culpable” como certeza si solo es módulo presente en stack.

### C.4 — UI BSOD

1. Historial de crashes.
2. Detalle de código, descripción, parámetros y evidencia.
3. Estado neutral cuando no hay dumps.
4. Exportación a informe técnico.

### R.1 — Windows Update

1. Diagnóstico read-only.
2. Plan de reparación visible.
3. Confirmación.
4. Reset seguro de componentes estrictamente necesarios.
5. Verificación posterior.

### R.2 — SFC / DISM

1. Preflight de permisos.
2. `DISM /Online /Cleanup-Image /ScanHealth` read-only.
3. Acciones de reparación con confirmación.
4. `sfc /scannow` con captura e interpretación conservadora.
5. Historial de ejecuciones.

### R.3 — Historial técnico

1. Historial por cliente/equipo.
2. Comparación de diagnósticos y reparaciones.
3. Evidencia exportable.
4. Sin base cloud en v0.3.

## Gate de cada slice

- restore PASS
- build Release PASS
- suite completa PASS
- cero tests omitidos
- `git diff --check` limpio
- CI Windows manual en la rama de desarrollo
- revisión externa de GitHub antes de continuar
