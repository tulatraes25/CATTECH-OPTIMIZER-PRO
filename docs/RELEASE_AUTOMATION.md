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

### Provenance guard (P.2.3)

El workflow release-package:

1. resuelve `source_sha` después del checkout (`git rev-parse HEAD`)
2. construye el EXE
3. lee el `ProductVersion` real del EXE
4. exige exactitud: `{version}+{source_sha}` (comparación exacta, sin StartsWith)
5. falla antes de ZIP/artifact si hay mismatch

Evita publicar un binario construido desde un commit distinto del source/tag pretendido. `product_version` y `file_version` se exponen como outputs reutilizables.

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

## P.2.2 — CATTECH Release (publicación controlada)

Workflow: `.github/workflows/release.yml`

Publicación manual CONTROLADA de una versión futura. **Solo `workflow_dispatch`** (acción humana explícita).

### Inputs

| Input | Tipo | Requerido | Descripción |
|-------|------|-----------|-------------|
| `version` | string | Sí | Semver estable `MAJOR.MINOR.PATCH` sin `v` |
| `source_sha` | string | Sí | SHA Git completo (40 chars) a publicar |
| `release_notes_path` | string | Sí | Ruta relativa de las release notes dentro del commit (sin `..`, sin URL) |
| `dry_run` | boolean | Sí (default `true`) | `true`: valida todo, NO escribe nada |
| `publish_confirmation` | string | No | Para publicación real debe ser exactamente `PUBLISH v{version}` |

### Jobs

- **preflight** (`contents: read`): valida semver, SHA de 40 chars, release notes (existencia + `v{version}`), detecta tag/release existentes. En modo live exige `source_sha == HEAD de main`, ref `main`, `github.sha == source_sha`, confirmación exacta y que tag/release NO existan
- **package**: reutiliza `release-package.yml` (misma única fuente de verdad del empaquetado) con `contents: read`, sin secrets
- **dry-run-verification** (`dry_run=true`): descarga el artifact, revalida outputs, checksum triple (declarado / archivo / recalculado), contenido del ZIP. **Cero operaciones de escritura** — termina con resumen `DRY-RUN PASS`
- **publish** (`dry_run=false`): únicos permisos write (`contents: write`, `attestations: write`, `id-token: write`). Revalida pre-write (TOCTOU: main SHA, tag y release ausentes), checksum, genera build provenance (`actions/attest`) y la verifica (`gh attestation verify`), crea **tag anotado** vía API (git/tags + git/refs, sin force), verifica el tag, crea la GitHub Release estable con ZIP + SHA-256 como únicos assets manuales

### Dry-run (default seguro)

```bash
gh workflow run release.yml \
  -f version=0.2.0 \
  -f source_sha=8d54e8a527f0183314eb1812ffb226f7ac1d5255 \
  -f release_notes_path=docs/RELEASE_NOTES_V0_2.md \
  -f dry_run=true \
  --ref main
```

El dry-run no crea tag, ni Release, ni attestations, ni modifica assets.

### Publicación real (template, no ejecutar sin versión real)

```bash
gh workflow run release.yml \
  -f version=X.Y.Z \
  -f source_sha=<SHA de main> \
  -f release_notes_path=docs/RELEASE_NOTES_VX_Y_Z.md \
  -f dry_run=false \
  -f "publish_confirmation=PUBLISH vX.Y.Z" \
  --ref main
```

Requisitos: `source_sha` debe ser el HEAD actual de main; tag y release de `vX.Y.Z` no deben existir; la confirmación debe coincidir exactamente.

### Recuperación parcial

Si el tag se crea pero la Release falla: NO se borra ni mueve el tag automáticamente. Se reporta `PARTIAL` (tag creado, release no) y la recuperación es manual/controlada.

### Verificar provenance

```bash
gh attestation verify CATTECH-Optimizer-Pro-vX.Y.Z-win-x64.zip \
  --repo tulatraes25/CATTECH-OPTIMIZER-PRO
```

Solo disponible para releases publicadas por el camino real con attestation. El dry-run no genera attestations.

## P.2.2

Estado: Pendiente.

Objetivo: publicación controlada de tag/GitHub Release reutilizando el workflow P.2.1
(descargar artifact, crear tag controlado, crear Release con ZIP + checksum, gate humano).
