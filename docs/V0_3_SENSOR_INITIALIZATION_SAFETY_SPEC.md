# SPEC Kiro - monitor de sensores: sesión nula y privacidad

## GOAL
Eliminar el estado de error repetitivo cuando una fábrica devuelve una sesión nula y evitar que mensajes internos de proveedores de sensores publiquen rutas locales en la UI.

## SOURCE FINDING
LibreHardwareSensorService.WatchLiveSnapshotsAsync consultaba TryCreateSession(out error), cuyo error podía ser null aun cuando IHardwareMonitorFactory.Create() devolviera null. El código luego llamaba CaptureWithRefreshAsync(session=null), capturaba NullReferenceException y producía un snapshot no disponible en cada ciclo del watcher indefinidamente. El compiler avisaba CS8604 en ese llamado. Además los catch iniciales incluían ex.Message; algunos mensajes de terceros pueden contener nombres de usuario o rutas.

## REQUIREMENTS
1. Sesión creada nula => una sola muestra no disponible y termina el stream (sin iniciar refresh).
2. Lectura única con sesión nula => muestra no disponible, sin excepción al usuario.
3. Errores de apertura/refresco no deben mostrar ex.Message, rutas, seriales ni identificadores locales.
4. Conservar la política anterior: cuando existe una sesión válida y Refresh falla, se permite reintentar en el siguiente intervalo y recuperarse; no romper mediciones normales.
5. No ejecutar hardware real ni escribir datos privados en CI. Mantener las releases estables inalteradas.

## ACCEPTANCE
- Tests para factory nula en toma única y stream.
- Tests para error sintético con ruta privada en toma única y stream.
- Suite Windows completa, 0 errores/fallos/omisiones, run exacto sobre nuevo SHA.
- Documentar límites: los errores de nodos internos se tratan por separado, este slice no demuestra seguridad completa del proveedor externo.
