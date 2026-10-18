# SPEC Kiro #92: Asociacion del informe a un caso de servicio

## GOAL
Evitar que un informe de un cliente atribuya resultados historicos de otra maquina por ser los mas recientes.

## REQUIREMENTS
- R1: Identificador de caso GUID no vacio, distinto de identificadores de archivo, generado al abrir un nuevo servicio.
- R2: Compatibilidad. Los cinco tipos de registros previos sin campo ServiceCaseId permanecen sin asociar (null) y no se reescriben.
- R3: La coincidencia del identificador es necesaria, no suficiente: tambien verificar origen, integridad del registro y aceptacion explicita del tecnico.
- R4: Un serial, fecha, nombre de computadora, ruta de disco o ID corto no permite asociacion automatica.
- R5: El generador de informe y la interfaz deben rechazar toda fuente sin enlace verificado, sin omision silenciosa y sin eliminar archivos existentes.

## DESIGN / IMPLEMENTATION ROUNDS
A. Base no destructiva: Guid? ServiceCaseId nullable en ServiceReport, DiagnosticReport, StartupAnalysis, SmartAnalysisResult y SmartTestSession; igualdad pura en ReportSourceCasePolicy. Sin cambios en atribucion de exportaciones. Esta etapa **NO** resuelve el gate.
B. Report options + HtmlReportService fail-closed. Matriz cliente A/B, disco A/B, mismo horario/serial, campos manipulados y revalidacion asincrona; mostrar 'Sin asociar al equipo' en UI.
C. Workflow de caso, persistencia de vinculos con auditoria, identidad de origen/integridad, tratamiento de legacy sin migracion adivinada.
D. CI completo con SHA exacto, candidato Windows y test en equipo fisico.

## NON-GOALS
Sin borrar historia, sin inferir claves por heuristicas, sin publicar release ni mezclar datos ajenos como una sola evaluacion.

## RISKS
La propiedad Guid? es mutable y NO constituye autentificacion, attestation ni prueba de un dispositivo real. No se puede certificar la atribucion hasta completar B-C y QA interactivo. El sistema actual sigue requiriendo proteccion adicional.

## ACCEPTANCE
Compatibilidad JSON sin serializar null, coincidir solo GUIDs no vacios, xUnit completo y gate ZIP independiente. Estado del issue #92: OPEN hasta B-D.

## ETAPA C1 — ID de caso en guardado nuevo (2026-10-17)
- El formulario Cliente/equipo genera un GUID no vacío al crear un registro nuevo y lo persiste en `ServiceReport.ServiceCaseId`, separado del ID de archivo.
- Las actualizaciones de datos no identitarios del mismo servicio conservan el vínculo; «Nuevo registro» y cambios de identidad de cliente/equipo tras un guardado confirmado generan otro GUID.
- «Cancelar» restaura el caso confirmado; un guardado anterior que termina tarde no puede imponer su GUID al formulario nuevo.
- Ninguna fuente diagnóstica/SMART/Inicio se enlaza automáticamente. El GUID no demuestra que un equipo físico pertenezca al cliente. Los registros históricos sin asociación siguen con `null`.
- **PENDIENTE C2:** asociar fuentes individuales mediante revisión explícita/auditoría e integridad de origen antes de permitir un informe integral. Una propiedad mutable no sirve como certificación de procedencia.

## ETAPA C1b — trazabilidad del historial generado (2026-10-17)
- `GeneratedReportInfo.ServiceCaseId` registra el identificador del caso sólo cuando el técnico incluyó efectivamente la sección Cliente y el ServiceReport tiene GUID válido, en HTML y PDF bajo las mismas opciones verificadas.
- Informes antiguos sin la propiedad permanecen con `null`. Exportaciones sin cliente no guardan ese GUID ni lo publican por defecto; no hay inferencia por nombre/fecha/serial.
- Este índice no constituye una prueba de procedencia ni un recibo auditado que permita enlazar diagnósticos externos. El bloqueo P0 #92 continúa hasta el workflow C2 y la validación de archivos en tiempo de uso.
