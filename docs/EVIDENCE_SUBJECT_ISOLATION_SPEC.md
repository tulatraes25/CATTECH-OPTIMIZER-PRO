# SPEC Kiro — Aislamiento por componente medido (#30)

**Estado:** slice aislado en desarrollo (CI a certificar). Hereda PR #28 SHA `021b5066cb8812b91a86df3d9f848e511ba2bee5` sobre las ramas anteriores. NO MERGE a main ni release por continuidad.

## GOAL
Impedir falsos diagnósticos y mejoras ficticias cuando dos mediciones corresponden a diferentes discos, GPU, controladores o recursos aunque provengan de la misma sesión de servicio y del mismo proveedor.

## REQUIREMENTS
- Introducir `DiagnosticSubjectId`: GUID opaco generado para un *recurso lógico dentro del contexto*, no derivado de MAC, serial ni ruta privada.
- `EvidenceRecord` requiere contexto **y sujeto**, tanto para observaciones válidas como datos ausentes; los valores `default` se rechazan.
- Cada `EvidenceExpectation` selecciona explícitamente `(SubjectId, FactKey)`. Se permiten reglas con varios sujetos **enumerados intencionadamente**, pero no se combinan por simple coincidencia del nombre de la medición.
- `RepairOutcomeEvaluator` compara el mismo contexto, sujeto, fuente, clave, tipo de valor y cronología. Si el sujeto cambia, resultado `Indeterminate/IncompatibleSubject` (no recuperación).
- `TechnicalPassportBuilder` verifica que `RepairOutcomeAssessment.SubjectId` coincide con ambos registros vinculados; una evaluación falsificada o sin sujeto se rechaza.
- La salida pública JSON **nunca** incluye IDs de contexto, sujeto o evidencia.

## NON-GOALS
No atribuir identidad física de hardware mediante un GUID. No leer SMART real, confirmar serial, realizar reparación, exportar reportes de clientes, autenticar el origen de datos ni identificar automáticamente un componente cambiado. La asociación real entre sujeto y hardware depende de un futuro adaptador inventariado.

## RISKS
Un técnico o un adaptador defectuoso podría reutilizar el mismo SubjectId para dos discos; el modelo no lo detecta sin una fuente adicional confiable. Un recurso puede ser removido/reemplazado durante la sesión. Antes de comercializar la función deben añadirse inventario físico, control de cambios de componentes, expiración de identidades y pruebas Windows con dispositivos reales.

`DiagnosticAssessment.MissingFactKeys` sigue expresando claves de hecho sin identificador de sujeto para UI interna; eso puede resultar ambiguo si una regla espera la misma clave en varios recursos. No usar ese texto solo para demostrar cuál componente falló: vincular la lista de carencias con sujetos de modo seguro antes de una UI que permita decisiones automáticas.

## DESIGN / TASKS
1. `DiagnosticSubjectId` obligatorio; se crean sujetos host y componentes mediante GUIDs de sesión.
2. Match de expectativas exacto `(SubjectId, FactKey)`, duplicación prohibida para la pareja.
3. Reparaciones comparadas estrictamente en el mismo sujeto; exportación verifica referencias.
4. Actualización de fixtures sintéticos; pruebas adversariales con Disk A y Disk B, coincidencia de proveedor y clave, creación de reglas multidisco explícitas, outcomes falsificados e ID ausente.

## ACCEPTANCE
- Misma sesión, diferente disco: nunca mejora observada.
- Solo Disk B informa salud positiva: regla de Disk A no corroborada.
- Regla para A y B puede corroborar ambas si cada lectura coincide; con una ausente => insuficiente.
- Pasaporte rechaza outcome de dos recursos distintos aunque traiga `ObservedImprovement`.
- Ni ContextId ni SubjectId serializados al pasaporte público.
- Windows CI SHA exacto: restore, Release build, suite completa, 0 fails/skips. QA física Windows 10/11 permanece pendiente.

## DISPOSITION
Mantener Issue #30 abierto hasta certificar adaptación al hardware real; este slice solo puede certificar su modelo y pruebas. Mantener #27 abierto también porque falta la asociación efectiva a equipos y la política para históricos sin contexto.
