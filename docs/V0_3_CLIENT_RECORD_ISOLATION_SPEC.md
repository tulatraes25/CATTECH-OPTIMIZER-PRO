# SPEC Kiro - aislamiento de detecciones por cliente

## GOAL
Impedir que un nuevo formulario conserve metadatos de almacenamiento y versión de Windows del cliente anterior; invalidar cualquier detección asíncrona que termine después de "Nuevo registro".

## SOURCE ROOT CAUSE
ClientEquipmentViewModel.NewRecord limpiaba propiedades observables, pero no los campos privados _detectedDiskFreeKnown, _detectedSystemVolumeCapacityGb, _detectedSystemVolumeFreeGb, ni la propiedad privada WindowsVersion. BuildReportFromForm utilizaba esos datos para crear un nuevo informe; una nueva ficha sin detección podía guardar la capacidad y espacio del cliente previo. Además, DetectHardwareAsync aguardaba una fuente asíncrona sin verificar si NewRecord se había ejecutado antes de aplicar el resultado.

## DESIGN / ACCEPTANCE
1. NewRecord invalida el identificador de generación, resetea todos los metadatos detectados (visibles y privados), elimina versión de Windows anterior y sale de IsDetecting.
2. Cada ejecución de DetectHardwareAsync captura su propia generación. Después del await, ignora silenciosamente datos de una generación invalidada. El finally del request viejo no cambia banderas del formulario nuevo.
3. Errores tardíos se descartan para el nuevo registro. Errores de detección actuales muestran un mensaje fijo sin ex.Message, que podría contener rutas privadas.
4. Cambia WindowsVersion a string inicializado: un formulario nuevo no debe asignar null a un campo string no anulable.
5. Tres tests sintéticos: guardar A y luego B sin nueva detección; detección tardía de A después de Nuevo registro; excepción tardía con ruta privada. No clientes reales ni escrituras de sistema.
6. Rama develop/v0.3, PR draft, main y v0.2.2 intactos. Windows CI exact SHA, suite completa 0 fallidas/omitidas.

## LIMITS
No se aborta una llamada WMI del sistema operativo ya iniciada; solo se impide que su resultado o error modifique el nuevo formulario. Cancelar consultas WMI bloqueadas requiere una especificación separada, no se promete aquí.
