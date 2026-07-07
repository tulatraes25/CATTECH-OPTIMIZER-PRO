# Release Notes - CATTECH OPTIMIZER PRO v0.1.1

**Fecha**: 2026  
**Tipo**: MVP Estabilizado  
**Tag**: v0.1.1-mvp

---

## Resumen

CATTECH OPTIMIZER PRO v0.1.1 es la primera versión estable del MVP. Incluye todas las funcionalidades core para diagnóstico, optimización y mantenimiento de equipos Windows, con generación de informes profesionales en HTML y PDF.

---

## Funcionalidades Incluidas

### 1. Configuración de Empresa/Técnico
- Formulario completo con 11 campos
- Selección de logo (PNG/JPG) con vista previa
- Color principal de la marca
- Persistencia en `config/empresa.json`

### 2. Cliente y Equipo
- Formulario de cliente (nombre, teléfono, email, empresa, dirección)
- Formulario de equipo (marca, modelo, serie, tipo, motivo)
- Detección automática de hardware via WMI
- Persistencia en `data/service-reports/`

### 3. Diagnóstico Rápido
- Análisis no invasivo del sistema
- Detección: SO, CPU, RAM, disco, inicio, seguridad, temporales
- Alertas automáticas por umbrales
- Persistencia en `data/diagnostics/`

### 4. Programas de Inicio
- Análisis de 6 fuentes (Registry Run/RunOnce, carpetas, tareas)
- Clasificación Microsoft vs terceros
- Filtros y búsqueda
- Persistencia en `data/startup-analysis/`

### 5. Desactivación Segura de Inicio
- Backup automático antes de desactivar
- Reversión desde backups
- Solo fuentes soportadas (Registry Run, carpetas)
- Microsoft bloqueado por seguridad

### 6. Limpieza de Temporales
- Escaneo previo con tamaño estimado
- 4 targets: %TEMP%, Windows\Temp, Miniaturas, Papelera
- Borrado seguro con protección de archivos
- Persistencia en `data/cleanup-results/`

### 7. Optimización Visual
- 8 ajustes predefinidos (animaciones, sombras, fuentes)
- Backup de cada valor antes de modificar
- Reversión desde backups
- NO modifica: resolución, drivers, servicios, accesibilidad

### 8. Punto de Restauración
- Verificación de permisos y protección del sistema
- Creación via PowerShell (Checkpoint-Computer) o WMI
- Manejo de errores (permisos, frecuencia, protección)
- Persistencia en `data/restore-points/`

### 9. Informe HTML Profesional
- 9 secciones: portada, cliente, equipo, diagnóstico, acciones, resultados, recomendaciones, observaciones, firma
- Logo embebido como base64 (portátil)
- CSS embebido (sin internet)
- Recomendaciones automáticas basadas en datos

### 10. Exportación a PDF
- Exportación via Microsoft Edge headless (`--print-to-pdf`)
- Verificación de disponibilidad de Runtime
- Fallback si no está disponible
- Persistencia en `reports/pdf/`

---

## Limitaciones Conocidas

1. **Microsoft Edge**: Requiere Microsoft Edge instalado para exportar PDF (pre-instalado en Win10/11)
2. **Permisos**: Algunas funciones requieren administrador (punto de restauración, Windows\Temp)
3. **Frecuencia**: Windows limita puntos de restauración a 1 por día
4. **Logo**: Se guarda como ruta absoluta (pendiente: copiar a carpeta interna)
5. **Sin DI**: Servicios instanciados directamente (pendiente implementar)
6. **Sin logging estructurado**: Serilog configurado pero no usado activamente
7. **Solo español**: No hay soporte multi-idioma

---

## Requisitos de Ejecución

| Componente | Requisito |
|------------|-----------|
| **SO** | Windows 10 (1809+) / Windows 11 |
| **.NET** | .NET 8 Desktop Runtime |
| **PDF** | Microsoft Edge instalado (pre-instalado en Win10/11) |
| **Permisos** | Administrador para algunas funciones |
| **RAM** | 512 MB mínimo, 1 GB recomendado |
| **Disco** | 100 MB para instalación + espacio para reportes |

---

## Qué queda para v0.2

- SMART completo de discos (via smartmontools)
- Monitoreo de temperaturas en tiempo real
- Reportes comparativos (antes/después)
- Exportación a JSON/CSV
- Modo oscuro completo
- Implementar Dependency Injection

---

## Commits incluidos

```
0543df0 chore: stabilize MVP v0.1.1
0d3ca73 feat: add PDF report export
a7cacc3 feat: add professional HTML report generation
1a7d9bb feat: add restore point module
968df95 feat: add safe visual optimization module
a65cf38 feat: add safe temporary files cleanup
2e9cf23 feat: add startup programs analysis module
79ae648 feat: add read-only quick diagnostic module
314e644 feat: add client and equipment module
4ebcbb1 feat: add company settings module
7b93540 feat: add safe startup disable and restore
e777abb docs: fix README commands and verify initial build
4ea6941 feat: initial project setup
```

---

## Agradecimientos

- ChrisTitusTech/winutil - Inspiración para organización de módulos
- LibreHardwareMonitor - Monitoreo de hardware
- smartmontools - Diagnóstico SMART
- Microsoft Edge - Exportación HTML a PDF
- Microsoft Edge - Exportación HTML a PDF

---

*Release Notes - CATTECH OPTIMIZER PRO v0.1.1*
