# AGENTS.md — Guía para agentes e ingeniería CATTECH

Estas instrucciones aplican a cualquier agente o colaborador que modifique este repositorio.

## Estado actual

- Versión publicada: **v0.3.1** (2026-11-22), .NET 10 LTS, paquete `win-x64` autocontenido.
- Línea de trabajo: `main`. No trabajar directamente sobre `main` para cambios grandes: usar una rama corta y un PR.
- Única fuente de verdad del estado: `git log`, los workflows de `.github/workflows/` y `CHANGELOG.md`. No asumir cifras de tests o versiones de memoria.

## Comandos

```powershell
# Compilar (Release, sin warnings esperados)
dotnet build src/Cattech.Optimizer.Pro.sln --configuration Release

# Ejecutar la suite completa (debe quedar 100% en verde)
dotnet test src/Cattech.Optimizer.Pro.sln --configuration Release

# Paquete portable win-x64 (autocontenido)
dotnet publish src/Cattech.Optimizer.Pro.UI/Cattech.Optimizer.Pro.UI.csproj `
  --configuration Release --runtime win-x64 --self-contained true --output output/release
```

Requisito: **.NET 10 SDK** (10.0.x). La app es WPF: solo compila y se prueba en Windows.

## Mapa rápido del código

- `src/Cattech.Optimizer.Pro.Core/` — modelos, interfaces y reglas de dominio (sin dependencias de Windows).
- `src/Cattech.Optimizer.Pro.Infrastructure/` — WMI, SMART/smartctl, sensores, reportes, reparaciones, persistencia JSON.
- `src/Cattech.Optimizer.Pro.UI/` — WPF + MVVM (CommunityToolkit.Mvvm). Composición manual de servicios en `MainViewModel`.
- `tests/` — xUnit; incluye gates de release que leen README/CHANGELOG/RELEASE_NOTES y un smoke test XAML.
- `config/` — `herramientas.json` (smartctl externo) y `bugcheckcodes.json` (catálogo BSOD).
- `docs/` — QA, release notes, automatización y documentación técnica.

## Reglas no negociables

- **Diagnosticar antes de reparar.** Toda operación que modifique el sistema exige consentimiento explícito, vista previa y verificación posterior; no ejecutar reparaciones por una orden genérica.
- **Nunca** desactivar Defender, firewall ni servicios de seguridad como "optimización".
- **No** subir minidumps, nombres de usuarios reales, seriales, rutas privadas ni logs a GitHub.
- Mantener las operaciones destructivas fuera de la UI y de los tests automatizados: los tests no ejecutan reparaciones reales.
- CI en verde no equivale a validación física. La verificación en equipos reales se documenta con `docs/QA_REAL_SMOKE.md`.
- No declarar un resultado como PASS sin evidencia contrastable (runs de CI, tests, diff).

## Publicación

El release se publica únicamente con el workflow `.github/workflows/release.yml` (workflow_dispatch) desde un SHA exacto de `main`, con versión semántica, notas de release y `dry_run=false` + confirmación. El workflow crea el tag anotado, el paquete, el SHA-256 y la atestación de procedencia; no crear tags o releases a mano.

Antes de publicar:

1. Actualizar versión en `src/Cattech.Optimizer.Pro.UI/Cattech.Optimizer.Pro.UI.csproj` (`Version`, `AssemblyVersion`, `FileVersion`).
2. Actualizar `CHANGELOG.md`, `README.md` (`**Versión**: vX.Y.Z`) y `docs/RELEASE_NOTES_VX_Y.md`.
3. Ejecutar el gate de release en verde (`ReleaseGateTests` + suite completa).
4. Publicar en dry-run y verificar; luego publicar en modo live.
