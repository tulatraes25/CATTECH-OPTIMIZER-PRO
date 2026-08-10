# Release Automation

## P.1 — CATTECH CI

Workflow: `.github/workflows/ci.yml`

- Se ejecuta en push a `main`, pull requests contra `main` y `workflow_dispatch`
- Pasos: restore, build Release, tests Release
- Resultados TRX conservados como artifact (`cattech-test-results`, retención 14 días)
- Permisos: `contents: read` — completamente read-only
- Runner: `windows-latest` (requerido por `net8.0-windows` + WPF)

## P.2.1 — CATTECH Release Package

Workflow: `.github/workflows/release-package.yml`

Workflow reutilizable que genera paquetes Release Candidate. NO publica Releases.

### Inputs

| Input | Tipo | Requerido | Descripción |
|-------|------|-----------|-------------|
| `version` | string | Sí | Semantic version sin `v` (ej: `0.3.0`) |
| `source_ref` | string | Sí | Commit, branch o tag a empaquetar (default `main` para dispatch) |

### Qué valida

- Versión semántica estricta `MAJOR.MINOR.PATCH`
- Consistencia de versión en `Cattech.Optimizer.Pro.UI.csproj` (Version / AssemblyVersion / FileVersion)
- Versiones visibles coherentes: README, MainWindow.xaml, MainViewModel.cs, HtmlReportService.cs, CHANGELOG
- Tests Release ejecutados DOS veces (detección de flakiness)
- EXE publicado con FileVersion `{version}.0` y ProductVersion comenzando en `{version}`
- `config/herramientas.json` presente, JSON válido, sin rutas personales ni secretos
- `smartctl.exe` AUSENTE (dependencia externa, no se empaqueta)
- ZIP completo (EXE, LHM, config, README, LICENSE; sin smartctl ni directorios personales)

### Qué genera

- `output/CATTECH-Optimizer-Pro-v{version}-win-x64.zip`
- `output/CATTECH-Optimizer-Pro-v{version}-win-x64.sha256.txt`
- Artifact `cattech-release-v{version}-win-x64` (ZIP + checksum, 14 días)
- Artifact `cattech-release-tests-v{version}` (TRX de ambas ejecuciones, 14 días)
- Outputs reutilizables: `artifact_name`, `zip_name`, `checksum_name`, `sha256`, `source_sha`

### Qué NO hace

- No crea tags ni Releases de GitHub
- No hace push ni commits
- No usa secrets ni `contents: write`
- No descarga smartctl/smartmontools

### Cómo ejecutarlo manualmente

```bash
gh workflow run release-package.yml \
  -f version=0.2.0 \
  -f source_ref=v0.2.0 \
  --ref main
```

Este comando genera un candidate artifact con la versión y el código indicados.
NO modifica la Release v0.2.0 existente.

## P.2.2

Estado: Pendiente.

Objetivo: publicación controlada de tag/GitHub Release reutilizando el workflow P.2.1
(descargar artifact, crear tag controlado, crear Release con ZIP + checksum, gate humano).
