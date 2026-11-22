# CATTECH Smoke Test Result

**Version**: v0.3.0
**Fecha**: YYYY-MM-DD
**Perfil**: Desktop / Notebook / Otro
**Windows**: Windows 11 / Windows 10 LTSC (build)
**Admin smoke**: Sí / No / NO EJECUTADO
**smartctl**: Disponible / No disponible
**Evidence schema**: 2

## Automatic evidence

- JSON: `output/qa-smoke/smoke-evidence-{label}-{timestamp}.json`
- Markdown: `output/qa-smoke/smoke-evidence-{label}-{timestamp}.md`
- Package baseline: PASS / FAIL

## Checklist

| Área | Resultado | Observaciones |
|------|-----------|---------------|
| Arranque normal (sin elevar) | | |
| Footer v0.3.0 / Home | | |
| Navegación (secciones abren) | | |
| Configuración (datos ficticios + persistencia) | | |
| Cliente/equipo (datos ficticios) | | |
| Diagnóstico (read-only) | | |
| Pantallas azules (read-only, sin dumps) | | |
| Reparaciones (vista previa, sin ejecutar) | | |
| Decisiones · DEMO (sin diagnóstico real) | | |
| Programas de inicio (solo análisis) | | |
| Limpieza (solo escaneo) | | |
| Optimización (solo análisis) | | |
| Punto de restauración (solo estado) | | |
| Hardware: Temperaturas | | |
| Hardware: CPU/GPU | | |
| Hardware: Memoria GPU | | |
| Hardware: Batería | | |
| Hardware: RAM SPD | | |
| Hardware: Inventario | | |
| Hardware: monitoreo/detención/sesión única | | |
| SMART sin smartctl (degradación) | | |
| SMART con smartctl (análisis read-only) | | |
| Informe HTML | | |
| Informe PDF | | |
| Pasaporte técnico (revisión y consentimiento) | | |
| Reinicio de app | | |
| Admin (solo read-only) | | |
| Escalado/UI (100% y otros si aplica) | | |

Resultados válidos: PASS / FAIL / N/D / NO EJECUTADO

## Hallazgos

### SMOKE-001

- **Severity**: Blocker / High / Medium / Low / Cosmetic
- **Área**: (ej: Hardware — Batería)
- **Pasos para reproducir**:
  1.
  2.
  3.
- **Esperado**: (qué debería pasar)
- **Obtenido**: (qué pasó realmente)
- **Reproducible**: Sí / No
- **Screenshot**: (nombre/ruta local opcional)

### SMOKE-002

...

## Resultado global

- [ ] PASS
- [ ] PASS WITH FINDINGS
- [ ] FAIL

## Notas

- Evidencia generada solo local; no subir automáticamente (PII accidental).
- No usar datos reales de clientes.
- No ejecutar acciones mutantes (limpieza, optimización, reparaciones, self-tests) durante el smoke base.
