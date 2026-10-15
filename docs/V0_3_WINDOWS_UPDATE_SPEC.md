# CATTECH v0.3 — Windows Update / SPEC Kiro

## R.1.1 Diagnóstico sin escritura

CATTECH lee únicamente la presencia, estado y modo de arranque de cuatro servicios:
`wuauserv`, `BITS`, `cryptsvc`, `UsoSvc` desde `Win32_Service`.

**Regla epistemológica:** un servicio detenido puede ser normal. Cuatro servicios presentes no prueban que Windows Update funcione, y un servicio ausente no autoriza a reparar Windows. No se presenta una calificación de salud inventada.

## R.1.2 Diagnóstico adicional (pendiente)

- Revisar eventos documentados de Windows Update y errores de instalación, sin mensajes de usuario ni rutas personales.
- Historial de actualizaciones si existe API de consulta confiable.
- Separar conectividad, Windows Update deshabilitado por política y reparación real.

## R.1.3 Reparación guiada (pendiente)

- Preflight de permisos y estado en Windows 10/11; exigir administrador.
- En caso de reparación, mostrar **plan exacto** y solicitar confirmación para cada paso.
- No desactivar Defender, cambiar StartMode de servicios ni borrar indiscriminadamente System32.
- No confundir vaciar caché con arreglar toda clase de error.
- Operaciones solo después de registrar evidencia, backup/capacidad de reversión cuando corresponda, y posibilidad de cancelación segura.
- Capturar códigos de salida sin persistir cadenas que contengan PII; verificar estado posterior.
- Tests con fakes que demuestren **cero escritura antes de confirmación**.
- Si estado desconocido, denegar reparación y describir causa, nunca ejecutar a ciegas.

## Gate

Build + tests Windows CI, interfaces aisladas, sin WMI real en tests, 0 modificaciones a sistema local y sin mezcla con la Release v0.2.2.
