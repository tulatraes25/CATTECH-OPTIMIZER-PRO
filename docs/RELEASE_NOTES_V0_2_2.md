# CATTECH OPTIMIZER PRO v0.2.2 — Release Notes

## Motivo de esta actualización

v0.2.2 es una actualización correctiva posterior al smoke real de v0.2.1. Corrige dos problemas detectados en hardware real:

1. El informe técnico podía reutilizar datos antiguos al exportar PDF después de cambiar o recargar datos.
2. El inventario de GPU podía mostrar memoria dedicada incorrecta en GPUs con más de 4 GB de VRAM.

No introduce una nueva fase funcional.

## Correcciones

### Informes — SMOKE-B1R-006

- Los informes HTML y PDF siempre se generan desde los datos actualmente cargados; ya no reutilizan un HTML generado previamente con datos diferentes.
- Si se selecciona Cliente/equipo o Diagnóstico, esas secciones no pueden omitirse silenciosamente en el informe.

### Hardware — SMOKE-B1R-005

- La memoria dedicada de GPU se obtiene mediante DXGI, que no tiene el límite de ~4 GB de la fuente anterior (Win32_VideoController.AdapterRAM).
- La memoria compartida del sistema no se presenta como VRAM dedicada.
- Si DXGI no puede correlacionarse de forma fiable con el adaptador WMI, se muestra N/D en lugar de un valor potencialmente incorrecto.

## Calidad / prevención de regresiones

- 6 tests del pipeline ReportViewModel → HTML → PDF
- 10 tests de memoria GPU DXGI y correlación WMI↔DXGI
- Release-gate para detectar mojibake UTF-8 antes de publicar
- 965 tests en total (0 failed, 0 skipped)

## SMART

SMART no cambia en esta patch. smartctl continúa siendo una dependencia externa autodetectada, no bundled en el paquete.

## Hallazgos menores

Los ajustes visuales menores observados durante QA (truncamiento de texto en Informes, mensajes de estado en Limpieza/Optimización, feedback en Punto de restauración) quedan fuera del alcance de esta patch y no bloquean estas correcciones.

## Recomendación

v0.2.2 reemplazará a v0.2.1 como versión recomendada una vez publicada y verificada mediante smoke focalizado.
