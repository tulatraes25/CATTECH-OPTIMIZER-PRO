# CATTECH Decision Center — SPEC Kiro

**Estado:** Core implementado en rama aislada. CI pendiente. Issue #44. Sin adquisición de hardware, reparación, exportación ni integración en el menú principal.

## GOAL
Convertir evidencias y verificaciones en un **dictamen explicable para una decisión técnica**: hechos consistentes o contradictorios, faltantes, mejoría observable, próxima acción recomendada y límites del diagnóstico. Enfocado en técnicos que necesitan justificar una intervención ante sus clientes.

## Estrategia competitiva
- HWiNFO: hardware y sensores, logs, alertas e informes (https://www.hwinfo.com/about-software/).
- AIDA64: inventario y cambios entre equipos, informes (https://www.aida64.com/products/aida64-network-audit).
- OCCT: estabilidad, benchmarks y ensayos (https://www.ocbase.com/).
- WhoCrashed: análisis de dumps, posibilidades y limitaciones de atribución causal (https://www.resplendence.com/whocrashed).
- Tweaking.com: Windows Repair y backups (https://www.tweaking.com/).

**Diferenciación de diseño**, no exclusividad demostrada: guiar al técnico de diagnóstico con datos/incompletitud explícita a planificación prudente, verificación objetiva y expediente redactado. No igualar la profundidad de sensores ni estrés de herramientas dedicadas en esta fase.

## REQUIREMENTS
1. `DecisionCenterBuilder.Build(context,diagnostics,postchecks)` no ejecuta I/O; resume solo evaluaciones creadas por Core.
2. Las entradas no pueden mezclar equipos, incluir nulos, superar 256 elementos por tipo ni duplicar reglas/outcomes.
3. Los textos públicos solo usan un vocabulario **cerrado y controlado por código** en español; nunca se copian hipótesis libres, códigos, nombres, observaciones crudas, fuentes o GUID.
4. Orden conservador: conflicto > insuficiente > refutado > consistente; postcheck sin resultado / mejora observada / no resuelto / indeterminado.
5. La recomendación nunca habilita o ejecuta reparaciones; `ObservedConditionImproved` no significa causalidad ni garantía de estabilidad.
6. El motor no agrega métricas arbitrarias de confianza ni se describe como IA causal.
7. Una eventual UI debe avisar que es una demostración con datos sintéticos hasta integrar con #27/#30/#33/#43.

## NON-GOALS
No lectura de sensores, SMART/dumps reales, no acciones Windows, no garantía de causa, no reemplazo del técnico, no prueba de identidad física, no firma digital, no exportación automática ni red. WPF sólo Windows.

## RIESGOS
El cliente podría interpretar una correlación positiva como reparación causal. Por eso el texto constante advierte los límites. Las cifras de recuento son locales y pueden revelar estado general del equipo: no enviar ni guardar sin review gate posterior.

## DESIGN
- `DecisionDiagnosticState`: No evaluado, consistente con regla, refutado, incompleto, contradictorio.
- `DecisionVerificationState`: no medido, mejoría observada, condición persistente, indeterminado.
- `DecisionNextAction`: capturar, revisar fuentes, completar datos, revisar hipótesis, inspeccionar plan, volver a medir, investigar persistencia, monitorear/documentar.
- `DecisionCenterSummary`: clase inmutable con constructor interno, etiquetas y recomendaciones locales, sin IDs.
- `DecisionCenterBuilder`: puro, determinista, bounded y fail-closed ante duplicados/contextos incorrectos.

## TASKS
- DC-01: modelo puro, pruebas sintéticas adversariales y CI Windows completo.
- DC-02: demostración WPF con 3-4 escenarios **claramente simulados**, botones que cambian el estado y sin servicios de hardware/red.
- DC-03: integrar a hardware real sólo después de cerrar el gate #43 y QA Windows local, con consentimientos separados y datos redactados.

## ACCEPTANCE
- Falta no implica salud; contradicción prioritaria; refutación no ejecuta plan; mejora medida no afirma causa; indeterminado repite prueba.
- Contextos y duplicaciones se rechazan; orden de entradas no modifica el dictamen.
- Sin PII/serial/dump/path/identificadores en `JsonSerializer.Serialize(DecisionCenterSummary)`.
- Restore, Build Release y xUnit completa Windows CI SHA exacto, sin tests fallidos/omitidos.
- No merge directo de PRs apilados en `main`, sin release ni QA física ficticia.

## EVIDENCE / DISPOSITION
Se abrió Issue #44 después de inspeccionar el estado GitHub y las páginas oficiales de herramientas competidoras. `main` estable en SHA `02586cc8dc8f6c83c9242582405291d90c55730c` al iniciar. Base del desarrollo: PR #42 SHA `0cfb395a19cf8c5a2f3025c98825494c52cf1a85`, CI #127 `1387/0/0`. No se presume integración certificada por la CI anterior. Estado real y nuevo SHA deben revalidarse en cada gate.
