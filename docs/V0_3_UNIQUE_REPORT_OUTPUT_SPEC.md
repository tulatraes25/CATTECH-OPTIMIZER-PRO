# SPEC Kiro - informes HTML/PDF sin sobrescritura accidental

## GOAL
Garantizar que dos informes del mismo cliente nunca se sobrescriban por compartir la marca temporal redondeada al segundo, manteniendo trazabilidad de salidas HTML y de los PDF derivados.

## ROOT CAUSE
HtmlReportService.GenerateHtmlReportAsync usaba Informe_Tecnico_CATTECH_<cliente>_yyyyMMdd-HHmmss.html con File.WriteAllTextAsync, que reemplaza un archivo ya existente. Generaciones paralelas para el mismo cliente pueden dejar una sola evidencia y metadatos apuntando al mismo archivo modificado. PdfExportService deriva su nombre de la ruta del HTML, por lo que el problema puede propagarse.

## REQUIREMENTS
1. Conservar prefijo CATTECH y nombre legible de cliente; añadir identificador aleatorio único después del timestamp.
2. Crear archivo de preparación con FileMode.CreateNew, escribir por flujo asíncrono, cerrar, y publicar mediante File.Move sin sobrescritura; si hay colisión, fallar de manera explícita en lugar de eliminar evidencia previa.
3. No publicar HTML parcialmente escrito; limpieza de temporales best-effort.
4. Sanitizar caracteres inválidos/control y limitar nombres largos por unidades de texto Unicode, no partir secuencias surrogate.
5. Test con 16 informes del mismo cliente simultáneos, cada uno con marcador de equipo distinto, comprobando archivos y contenido únicos.
6. Test con cliente de nombre extremo, caracteres inválidos y fallback SinCliente.
7. No alterar contenido técnico del informe ni ejecutar Edge/PDF, no usar clientes reales.
8. Desarrollo solo en develop/v0.3; main, release v0.2.2 y PR draft conservados; Windows CI exact SHA.

## RESIDUAL / NON-GOALS
- SaveReportInfoAsync maneja reports-info.json mediante lectura-modificación-escritura sin lock general y puede perder actualizaciones de metadatos si hay exportaciones concurrentes. Esto requiere un slice independiente para bloqueo, escritura atómica y control multiproceso; no afirmar que el historial ya es fully-concurrency-safe.
- La condición de carrera entre generación de HTML y exportación posterior por otras rutas debe tener pruebas integradas adicionales. No implica que se haya ejecutado Edge o un PDF real en CI.
- La aleatoriedad reduce colisiones, pero File.Move sin sobrescritura es el control final que garantiza integridad incluso en colisión.
