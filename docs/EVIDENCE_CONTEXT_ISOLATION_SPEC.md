# SPEC Kiro #27 — Aislamiento de evidencias por sesión/equipo

**Estado:** implementación aislada, CI pendiente antes de aprobar. Base exacta: `f7751da272cbedc31de5c170c5db73d4dc33edc4` (PR #26), apilado sobre los PR #25, #24, #23 y #1. Nunca fusionar esta rama directamente en main.

## GOAL
Evitar atribuir observaciones, hipótesis, verificaciones antes/después y pasaportes de otro equipo a la sesión bajo análisis. Una mejora observada no prueba causalidad.

## REQUISITOS
- `DiagnosticContextId`: identificador opaco GUID, generado aleatoriamente por sesión de servicio y no derivado de serial, MAC, IP, nombre de computadora, usuario o cliente.
- `EvidenceRecord.Observed` y `Missing` exigen el contexto de forma explícita; `default`/Guid.Empty se rechazan.
- `DeterministicDiagnosticEvaluator.Evaluate` requiere contexto elegido y rechaza **cualquier** evidencia de otro contexto, incluso irrelevante para la regla. Un conjunto vacío produce solo una evaluación insuficiente, acotada al contexto declarado.
- `RepairOutcomeEvaluator.Compare`: ante dos contextos distintos, devuelve `Indeterminate/IncompatibleContext` con contexto inválido; no permite una mejora positiva.
- `TechnicalPassportBuilder.Build` requiere contexto: evidencia, diagnósticos y resultados deben coincidir; incluso evaluaciones vacías y resultados sintéticos directos se rechazan si pertenecen a otro contexto. No emite ese identificador en el JSON público y rechaza usarlo como ID de exportación.

## NON-GOALS
No autentica automáticamente un hardware, no verifica una máquina física, no identifica discos dentro de una máquina, no detecta un técnico que asigna manualmente el contexto incorrecto, no crea base de datos de clientes ni firma digital. No se ejecutan operaciones sobre Windows, se suben dumps ni se exporta información real.

## RISKS Y LÍMITES
- El contexto representa una **sesión de atención de un equipo**; su asociación confiable a un cliente/equipo depende de infraestructura e interfaz aún pendientes. La fuente de observación no se autentica con solo un GUID.
- Antes de comparar un disco o componente a través de cambios de hardware, diseñar identidad adicional de *recurso/objeto medido* y pruebas de reemplazo; dos mediciones del mismo proveedor pueden referirse a dispositivos distintos incluso dentro de una sesión.
- La API de Core cambia las firmas **solo en código de desarrollo no publicado**. Los históricos de otras versiones sin contexto no deben recibir automáticamente uno inferido. Si luego existe persistencia, migrar esquema explícitamente, marcar registros anteriores como `unscoped` y exigir revisión o impedir comparaciones.
- La salida pública puede revelar condiciones de equipo como recuentos; sigue exigiendo revisión/consentimiento de PR #26 y evaluación de privacidad de metadatos. No almacenar el identificador de contexto en el payload público.

## DESIGN / TASKS
1. Tipar `DiagnosticContextId` con constructor de no-vacío y función `NewSession`.
2. Pasar contexto a fábricas de evidencia y bloquear contextos incompatibles en evaluador de hipótesis.
3. Marcar como indeterminadas las comparaciones cruzadas y rechazar anexarlas a un pasaporte.
4. Exigir contexto uniforme en `TechnicalPassportBuilder` sin incluirlo en su salida.
5. Actualizar exclusivamente tests sintéticos existentes y agregar pruebas adversariales cruzadas; no cambiar UI, servicios, datos reales ni rutas de reparación.

## ACCEPTANCE
- Mismo `SourceId` y `FactKey`, equipos A/B: ningún `ObservedImprovement`.
- Datos de A y B en una regla (aun con valores idénticos o ajenos a la regla): rechazo.
- Pasaporte de A con evidencia o evaluaciones de B: rechazo, incluso diagnóstico sin evidencia enlazada.
- Falta de contexto, reutilización de ID de exportación igual al contexto y evidencia con ID duplicado: rechazo.
- Pasaporte público sin IDs de contexto ni evidencia.
- Restore, Release Build, xUnit total sin fallos ni omitidos, Windows CI en SHA exacto y diff revisado. CI no sustituye QA de Windows 10/11 ni validación de fuentes reales.

## EVIDENCE / DISPOSITION
- Issue: https://github.com/tulatraes25/CATTECH-OPTIMIZER-PRO/issues/27
- Los tests utilizan exclusivamente GUIDs y valores sintéticos. Mantener estado `PENDING` hasta ver logs de la ejecución exacta.
- No cerrar #27 hasta integrar el contexto en adaptadores/selección de equipo, añadir QA y verificar migración cuando corresponda.
