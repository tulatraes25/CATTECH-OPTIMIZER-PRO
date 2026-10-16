# CATTECH v0.3 — Plataforma y advertencias CA1416 (SPEC Kiro)

**Estado:** documento de integración, NO certificación de hardware ni release.
**Punto de partida:** CI Windows #199, SHA `077872e46a57532f69c399bfcc33211602730a48`, 1610 xUnit PASS / 0 fallos / 0 omitidos, 157 advertencias de compilación CA1416.
**Alcance:** mantener la aplicación oficial orientada a Windows 10 (1809+) y Windows 11.

## GOAL
Explicitar en la arquitectura la dependencia del sistema operativo, en lugar de acumular advertencias del analizador de compatibilidad sobre APIs Win32/WMI/EventLog/Registry utilizadas deliberadamente por un producto Windows.

## REQUIREMENTS
1. `Core` conserva `net8.0`: modelos y lógica pura potencialmente reutilizables.
2. `Infrastructure` declara `net8.0-windows`: ejecuta consultas de hardware y operaciones de plataforma; **no** prometemos su funcionamiento en Linux/macOS.
3. `UI` continúa `net8.0-windows` + `UseWPF=true`; tests continúan `net8.0-windows`.
4. Se conservan las restricciones existentes: diagnóstico previo, confirmación para reparaciones, prohibición de desactivar seguridad por “optimización”, y fallos explícitos cuando hardware/driver no esté disponible.
5. No agregar `NoWarn`, `#pragma warning disable` ni desactivar analizadores para conseguir un resultado verde.
6. No modificar la versión pública `0.2.2`, `main`, tag ni assets publicados.

## WHY
Las llamadas de `Infrastructure` a WMI, registro, EventLog y algunos sensores Windows estaban analizadas desde un `TargetFramework=net8.0` independiente de plataforma, aunque su ejecutable y los tests ya son Windows. El SDK agrega el contrato de plataforma al ensamblado cuando se utiliza `net8.0-windows`. Esto resuelve advertencias de *contrato de destino* sin afirmar que el software es más robusto en hardware real.

Referencia oficial del análisis de compatibilidad: https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1416

## NON-GOALS
- Migrar CATTECH a Linux o macOS.
- Cambiar seguridad de servicios, Registry, DumpChk, WMI o reparación de Windows.
- Suprimir advertencias genuinas sobre APIs Windows no soportadas por el TFM declarado.
- Afirmar que un build con cero advertencias valida sensores, discos, Windows Update, SMART o compatibilidad real.

## ACCEPTANCE / EVIDENCE
- `dotnet restore src/Cattech.Optimizer.Pro.sln` en GitHub Actions Windows.
- `dotnet build src/Cattech.Optimizer.Pro.sln --configuration Release --no-restore`: registrar de manera literal warnings y errores.
- `dotnet test` Release completo: 0 failed / 0 skipped.
- Revisar el SHA exacto y diff; no archivos fuera de los permitidos.
- Antes de una futura publicación, ejecutar además el empaquetado reproducible win-x64, tests dobles, versión visible/ProductVersion, SHA256 y smoke test físico controlado.

## DISPOSITION
Source-only architecture change pending exact-SHA CI. Si aparece incompatibilidad NuGet, proyecto o paquete, revertir únicamente esta modificación en la rama de desarrollo, investigar el gate y no marcar CI PASS sin evidencia.
