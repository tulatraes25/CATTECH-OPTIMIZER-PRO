# SPEC Kiro — Identidad de reportes y bloqueo de IDs ambiguos

**Issue:** #53 · **Base:** PR #52 SHA `89c649a99795e99c6f9717b821e5711cfceeb0ce`, CI Windows #141 1449/0/0. **DRAFT / NO MERGE**.

## GOAL
Evitar seleccionar o borrar el expediente de otro cliente/equipo por una
coincidencia en un identificador lógico abreviado. Esto es requisito previo
para un futuro mapeo real de sesiones, pero NO lo implementa todavía.

## HALLAZGO
`ServiceReport.Id` generaba `Guid.NewGuid().ToString("N")[..8]`,
equivalente a 32 bits de aleatoriedad. El nombre del archivo de
`JsonServiceReportService` sí usa un GUID largo, por lo que DOS archivos
diferentes pueden contener un mismo Id. Antes de este corte, `LoadReportAsync`
y `DeleteReportAsync` usaban la PRIMERA coincidencia enumerada por Windows.
Esto puede seleccionar o borrar el reporte de otro cliente al colisionar
las claves lógicas, aunque los archivos estén guardados correctamente.

## REQUIREMENTS
1. Los nuevos `ServiceReport.Id` serán GUID aleatorios completos en
   formato `N` de 32 caracteres hexadecimales. Los IDs históricos de
   8 dígitos seguirán siendo aceptados cuando se pueda demostrar unicidad.
2. Para cargar/borrar por Id debe recorrerse todo el conjunto local y
   obtenerse EXACTAMENTE una coincidencia. Cero o dos+ retornan null/false,
   sin usar orden arbitrario de archivos.
3. Si existe cualquier documento ilegible, corrupto, con Id ausente o con
   claves `id` duplicadas, no se puede garantizar unicidad del conjunto.
   Cargar/borrar por ID falla cerrado hasta revisar ese documento.
4. Un Id ausente en JSON no debe regenerarse implícitamente mediante el
   inicializador por defecto de `ServiceReport.Id`.
5. Preservar el contrato `IServiceReportService`, los archivos existentes,
   el formato JSON y los nombres aleatorios únicos de salida. Guardar una
   segunda versión del mismo reporte sigue produciendo dos snapshots
   independientes, pero ya no autoriza seleccionar/borrar uno por Id ambiguo.
6. Los tests deben usar solo directorios temporales aislados y nombres
   ficticios. Ningún expediente real se modifica.

## NON-GOALS
No migración automática de Id históricos, no sustitución de la identidad
física, no firma, no renombrar archivos existentes, no import/export remoto
ni publicación de release. No se usa report.Id para autenticar un equipo.

## RISKS
- DOS expedientes idénticos al guardar varias versiones con el mismo Id
  siguen siendo legítimos como snapshots; las operaciones por Id quedan
  bloqueadas. La UI necesitará una vía explícita para seleccionar un archivo
  único con verificación de contenido/contexto.
- Un JSON corrupto puede bloquear otras operaciones, falso negativo
  conservador que requerirá recuperación manual segura.
- La comprobación de unicidad usa SemaphoreSlim solo POR INSTANCIA; no
  bloquea escrituras de otros procesos ni evita TOCTOU entre revisar el
  archivo y borrarlo por ruta. Esta ronda NO certifica borrado libre de
  carreras. El control posterior requiere almacenamiento transaccional o
  identidad de handle verificada, y pruebas de concurrencia interproceso.
- Los IDs alargados pueden requerir ajustes visuales y de exportación;
  versiones históricas de ocho dígitos seguirán visibles pero no garantizan
  unicidad.
- Se rechaza sin excepciones privadas al usuario pero no se implementa UX
  de recuperación del expediente en esta fase.

## DESIGN / TASKS
1. Cambiar el generador de ID para nuevos reportes.
2. Introducir `FindUniqueReportAsync` con parsing JSON estricto y
   validación de exactamente una propiedad `id` string no vacía.
3. Usar ese control tanto en `LoadReportAsync` como en
   `DeleteReportAsync`.
4. Pruebas de colisión actual e histórica, re-guardado, corrupción,
   campo ausente, propiedad duplicada y compatibilidad con legacy.
5. Verificar fuente/diff y GitHub Actions Windows sobre SHA exacto:
   Restore, Release Build, suite completa xUnit 0 fallidas/0 omitidas.

## DISPOSITION
Implementation PENDING CI; Issue #53 abierto mientras falte el gate
de identidad fuerte de archivos y la integración UI/Windows real.
Nunca conectar a `DiagnosticSessionCoordinator` hasta que el expediente
de servicio y el hardware seleccionado estén vinculados y validados
independientemente. `main` y releases no se tocan.
