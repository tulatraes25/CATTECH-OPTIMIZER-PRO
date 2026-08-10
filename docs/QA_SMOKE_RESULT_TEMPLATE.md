# CATTECH Smoke Test Result

**Version**: v0.2.0
**Fecha**: YYYY-MM-DD
**Perfil**: Desktop / Notebook / Otro
**Windows**: Windows 10/11 (build)
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
| Footer v0.2.0 / Home | | |
| Navegación (secciones abren) | | |
| Configuración (datos ficticios + persistencia) | | |
| Cliente/equipo (datos ficticios) | | |
| Diagnóstico (read-only) | | |
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
