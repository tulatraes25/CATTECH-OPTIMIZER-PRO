# CATTECH OPTIMIZER PRO v0.2.1 — Release Notes

## Motivo de esta actualización

v0.2.1 es una actualización de estabilidad que reemplaza v0.2.0 como versión recomendada. No introduce funcionalidades nuevas; corrige un defecto reproducible de la versión anterior y endurece la verificación de las vistas antes de publicar.

## Correcciones

- **Cliente/equipo ya no debe cerrar la aplicación al abrir la sección**: corregido el crash causado por un `StaticResource` XAML no registrado (`InvertBoolConverter`).
- **Recursos XAML faltantes corregidos en vistas relacionadas**: la auditoría detectó el mismo defecto latente en Configuración, Diagnóstico y Optimización; los converters faltantes fueron registrados preventivamente.
- **Textos UTF-8/emojis verificados antes del release**: corregida una regresión de encoding detectada durante la preparación, sin afectar el funcionamiento de las vistas.

## Prevención de regresiones

- Smoke tests WPF STA que cargan las 10 vistas principales en CI.
- Navegación a Cliente/equipo cubierta por test automatizado.
- 948 tests (0 failed, 0 skipped) en CI Windows.

## SMART y Hardware

SMART y Hardware mantienen las funcionalidades de v0.2.0. No se introduce feature nueva ni se cambia comportamiento de sensores, inventario, informes o self-tests.

## Dependencia smartctl

smartctl sigue siendo una dependencia externa: no se distribuye dentro del paquete; se autodetecta desde ruta configurada (`config/herramientas.json`), instalación estándar, rutas junto a la app o PATH.

## Recomendación

Los usuarios de v0.2.0 deberían actualizar a v0.2.1 por el fix del crash de navegación a Cliente/equipo.
