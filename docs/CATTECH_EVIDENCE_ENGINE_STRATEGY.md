# CATTECH — estrategia diferencial y arquitectura de Evidence Engine

Estado: **SPEC / propuesta, no implementado**. Fecha: 2026-10-14. Issue: #21.

## Decisión de producto

CATTECH no competirá por cantidad de sensores con HWiNFO, benchmarks con OCCT o cantidad de reparaciones con Tweaking.com Windows Repair. Su propuesta será un **expediente de diagnóstico y reparación basado en evidencia**, integrando: hipótesis explicables → decisión informada → previsualización de cambios → consentimiento → comprobación posterior → informe técnico verificable.

No afirmar que esta combinación es exclusiva mundialmente sin estudio competitivo y de patentes.

## Análisis de capacidades (no atribuir ausencia de funciones sin evidencia)

| Producto | Fortaleza conocida | Oportunidad CATTECH |
| --- | --- | --- |
| HWiNFO | Telemetría de sensores y alertas | Interpretación contextual multifuente |
| AIDA64 | Hardware, pruebas e informes | Historial explicable de acciones |
| OCCT | Estabilidad y certificación de pruebas | Asociación de resultados con diagnóstico y post-verificación |
| WhoCrashed | Dump/BSOD | Correlación conservadora entre fuentes |
| Windows Repair | Herramientas de reparación y restauración | Previsualización específica, seguimiento de efectos |

## Fases y dependencias

### Gate P0: seguridad antes de innovación
Revisar y cerrar con evidencia de CI + QA real los P1 conocidos #17, #18, #19 y #20, especialmente la equivalencia entre exclusiones de escaneo y exclusiones de borrado de caché. **No adelantar cambios de reparación o limpieza reales en esta SPEC.**

### EE-01: vocabulario de evidencia (Core, sin I/O)
Tipos inmutables: `EvidenceRecord` (`SourceId`, `ObservedAtUtc`, `FactKey`, `TypedValue`, `Availability`, `Provenance`, `PrivacyClass`, `Limitations`); `DiagnosticHypothesis` (versión de regla, corroboraciones, contradicciones, gaps, criterio de falsación); `DiagnosticAssessment` (estado `supported/unsupported/insufficient/contradictory`, nunca porcentajes injustificados).
- Tests: orden aleatorio, relojes inconsistentes, datos faltantes, códigos desconocidos, contradicción explícita, persistencia de identidad y sanitización.
- Sin inventar información al fallar un proveedor.

### EE-02: reglas explicables
Catálogo de reglas versionadas, deterministas y sin efectos secundarios. Separar observación, correlación temporal e inferencia causal. No sugerir driver culpable por presencia aislada en dump. Tests metamórficos para evitar cambio de conclusión por orden de entrada o evidencia irrelevante.

### EE-03: Repair Preview
`RepairPlan` *read-only* con scope, acciones previstas, precondiciones, backup opcional/obligatorio según riesgo, requisito de elevación, reinicio, reversibilidad real y señales de éxito. Preview nunca puede invocar proceso, servicio de Windows, delete, elevación ni registros. Tests con puertos falsos que cuentan cero mutaciones.

### EE-04: Repair Proof
Comparación de baseline antes/después bajo condiciones compatibles. Resultado `verified / unresolved / indeterminate`, con ventana temporal y criterios definidos antes de reparar. Éxito de proceso != resolución del problema.

### EE-05: Technical Passport / historial local
Informe exportable reproducible (esquema versionado) con redacción por defecto de seriales, nombres, rutas, dumps y datos personales. Identificador aleatorio no reversible de expediente; firma/hash con descripción precisa de sus garantías, sin prometer anti-manipulación si no hay cadena de confianza. Retención configurable y export opt-in.

## Criterios de aceptación transversales
- `main`, releases y tags protegidos; cambios solo en ramas/PR.
- Sin servicios externos obligatorios ni transferencia de telemetría sin consentimiento.
- Sin borrado, modificación de registros o reparaciones en test que usen sistemas reales.
- Tests focalizados + suite completa Windows Release, cero fallos/omitidos, diff check, CI SHA exacto y revisión de logs.
- Un PASS en CI de código no sustituye pruebas físicas Windows 10/11.
- Interfaz dirigida al técnico: *qué sabemos / qué no sabemos / por qué / qué hacer / cómo comprobar*.
- Beta con técnicos: tiempo hasta diagnóstico, error de hipótesis, tasa de intervenciones innecesarias, éxito verificable, satisfacción y fallos de privacidad; comparativas reproducibles contra flujo habitual.

## Fuentes iniciales de benchmarking (verificar fechas en cada revisión)
- https://www.hwinfo.com/
- https://www.aida64.com/
- https://www.ocbase.com/
- https://www.resplendence.com/whocrashed
- https://www.tweaking.com/

## Estado y disposición
Esta entrega es documentación de arquitectura y diferenciación comercial. **No implica implementación, ejecución de pruebas, CI ni certificación de hardware.** Siguiente paso seguro: inventariar modelo de diagnóstico ya existente, corregir P1 abiertas y luego EE-01 en rama aislada.
