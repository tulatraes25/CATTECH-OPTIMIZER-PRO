# SPEC Kiro - inventario asíncrono y privacidad

## GOAL
Evitar bloquear un hilo de trabajo esperando tareas asíncronas y proteger la interfaz frente a detalles privados en errores de proveedores.

## ROOT CAUSE
HardwareViewModel.ReadInventory empleaba .GetAwaiter().GetResult() cuatro veces dentro de Task.Run. Aunque estaba fuera de WPF, consumía un hilo completo durante esperas asíncronas y podía perjudicar la concurrencia. Los catch mostraban ex.Message en colecciones enlazadas a la UI.

## REQUIREMENTS
1. Mantener el despacho externo con Task.Run(ReadInventoryAsync), por si un proveedor devuelve Task<T> recién después de trabajo sincrónico.
2. Reemplazar los cuatro bloqueos CPU, GPU, RAM y placa por await y ConfigureAwait(false) en código exclusivamente de datos; ApplyInventory sigue ejecutándose tras await en contexto de UI.
3. Conservar captura de fallos por sección: CPU no impide actualizar GPU/RAM; la pantalla no muestra detalles privados de excepciones.
4. Mantener la lógica de IsInventoryBusy, evitar lecturas simultáneas y preservar timestamps.
5. No hacer operaciones de reparación, elevar privilegios ni copiar datos reales a CI.
6. Verificar tests previos y regresión de redacción de rutas privadas, suite Windows completa exact SHA.

## NON-GOALS
No cambia el modelo SMART, no añade timeouts WMI no soportados, no elimina toda la deuda de CA1416/xUnit1031, no prueba el rendimiento de sensores reales.

## ACCEPTANCE
- Cero llamadas GetAwaiter().GetResult en ReadInventoryAsync.
- Se conserva el estado parcial si falla una sola sección, sin ex.Message.
- Pruebas exact SHA, 0 fallos/omisiones, main v0.2.2 y PR draft sin alteración.
