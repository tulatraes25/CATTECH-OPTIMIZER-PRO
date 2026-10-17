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
