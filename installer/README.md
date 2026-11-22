# Instalador de CATTECH OPTIMIZER PRO

Instalador para Windows generado con **Inno Setup 7** a partir del publish autocontenido `win-x64`.
Produce un único `CATTECH-Optimizer-Pro-vX.Y.Z-Setup.exe` con asistente, accesos directos y desinstalador.

## Archivos

| Archivo | Rol |
|---|---|
| `CATTECH-Optimizer-Pro.iss` | Script del instalador (define versión, archivos, accesos directos, desinstalación) |
| `app.ico` | Icono usado por el instalador y los accesos directos |
| `LEEME-INSTALACION.txt` | Notas que el instalador muestra al finalizar (se copia dentro del paquete) |

## Requisitos de build

- **.NET 10 SDK** (10.0.x)
- **Inno Setup 7** (`ISCC.exe`). En GitHub Actions Windows runners viene Inno Setup 6 preinstalado; para Inno 7 hay que instalarlo o usar `choco install innosetup`.

## Cómo generar el instalador

Desde la raíz del repositorio:

```powershell
# 1) Payload autocontenido sin símbolos de depuración
dotnet publish src\Cattech.Optimizer.Pro.UI\Cattech.Optimizer.Pro.UI.csproj `
  --configuration Release --runtime win-x64 --self-contained true `
  -p:DebugType=None -p:DebugSymbols=false `
  --output output\installer-payload\win-x64

# 2) Copiar README y LICENSE al payload
Copy-Item README.md, LICENSE output\installer-payload\win-x64 -Force

# 3) Compilar el instalador
& 'C:\Program Files\Inno Setup 7\ISCC.exe' installer\CATTECH-Optimizer-Pro.iss
```

Resultado: `output\installer\CATTECH-Optimizer-Pro-v<version>-Setup.exe`.

## Modos de instalación

- **Solo para mí** (por defecto): `%LOCALAPPDATA%\Programs\CATTECH OPTIMIZER PRO`, sin UAC y con carpeta escribible.
- **Para todos los usuarios**: `C:\Program Files\CATTECH OPTIMIZER PRO`, requiere administrador.

Motivo del valor por defecto: la aplicación escribe `data\`, `reports\` y `backups\`
junto al ejecutable. Una instalación por usuario evita fallos de escritura cuando la app
se ejecuta sin elevar; si se instala para todos los usuarios, debe ejecutarse como
administrador para generar informes y respaldos.

## Actualizar la versión del instalador

1. Cambiar `MyAppVersion` en `CATTECH-Optimizer-Pro.iss`.
2. Actualizar `Version`/`AssemblyVersion`/`FileVersion` en `src/Cattech.Optimizer.Pro.UI/Cattech.Optimizer.Pro.UI.csproj`.
3. Actualizar `CHANGELOG.md`, `README.md` y `docs/RELEASE_NOTES_VX_Y.md`.
4. Regenerar payload e instalador, y verificar el EXE publicado.

## Verificación realizada

- Instalación silenciosa por usuario, comprobación de archivos, accesos directos y entrada de desinstalación.
- Arranque del EXE instalado (ventana visible y respondiendo).
- Desinstalación silenciosa y verificación de limpieza (conservando `data\`, `reports\` y `backups\`).
