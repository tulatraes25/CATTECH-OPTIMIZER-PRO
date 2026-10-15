# SPEC Kiro — Repair Outcome Integrity en rama SMART

Fecha: 2026-10-15. Issue P1 #33. Rama independiente apilada sobre el PR #38; NO MERGE a main.

## GOAL
Impedir que un consumidor externo de Core cree un resultado `ObservedImprovement`
arbitrario y lo incorpore en el pasaporte tecnico como si hubiese sido
producido por una comparacion legitima.

## REQUIREMENTS
- Sustituir el `public record` de `RepairOutcomeAssessment`, que exponia
  constructor publico, por clase inmutable con constructor `internal`.
- Solo `RepairOutcomeEvaluator.Compare` puede crear normalmente resultados.
  Validar las correspondencias status/reason y exigir contexto, sujeto e IDs
  validos para estados no indeterminados.
- Ocultar los IDs de evidencia, contexto y sujeto de la serializacion JSON
  generica del resultado; el pasaporte publico sigue usando un esquema
  minimizado y sus propias restricciones.
- Conservar resultados `Indeterminate` para conflictos de contexto/sujeto
  sin falsear comparaciones positivas.
- Sustituir fixtures que forjaban estados por resultados reales del evaluador
  y pruebas de reuso malicioso de referencias a otro disco/caso.

## NON-GOALS
No firma digital, attestation fisica, cifrado, limpieza de datos de los
metadatos arbitrarios de reglas, ni validacion de reparacion en Windows real.
Un constructor interno protege la API publica, no los procesos maliciosos con
reflection o control sobre el mismo ensamblado.

## RISKS
- Existen implementaciones paralelas del Issue #33 en PR #34 con firmas de
  contexto distintas. No mezclar ni mergear ambas ramas sin reconciliar
  `DiagnosticSubjectId` e IDs de sesion.
- El resultado puede seguir indicando una mejora de condicion, NO causalidad.
- Serializar modelos internos directamente aun puede revelar texto libre de
  `RuleId` o `FactKey`; usar solo `TechnicalPassportBuilder` para compartir
  agregados.
- No se altera ninguna API ya publicada: Evidence Engine permanece en PRs draft.

## DESIGN / TASKS
1. Clase con propiedades solo-lectura y constructor no publico.
2. Validacion de estado y razon y rechazo de scope invalido para estados positivos.
3. Privacidad por defecto de IDs linkeables en JSON.
4. Tests de constructor no publico, serializacion, evidencia cruzada real y
   sustitucion de evidencias referenciadas.

## ACCEPTANCE
Restore/Release Build/full xUnit Windows, 0 fallidos y 0 omitidos sobre SHA
exacto; diff restringido, log revisado. Los PRs siguen draft y la QA fisica
permanece pendiente. CI verde no certifica integracion con WPF ni Windows real.

## EVIDENCE
Source PR #34 se inspecciono como antecedente de diseno, pero no se
fusiona directamente debido a su contexto incompatible con PR #32.

## DISPOSITION
Estado hasta ver CI: IMPLEMENTED/PENDING. Mantener Issue #33 abierto hasta
acabar gates de integracion, validacion de dispositivos reales y unificacion
segura de ramas.
