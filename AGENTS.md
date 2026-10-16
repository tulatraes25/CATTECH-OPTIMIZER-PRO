# CATTECH OPTIMIZER PRO — instrucciones para agentes

Estas instrucciones aplican a **toda ronda de ingeniería CATTECH**: investigar, auditar, programar, corregir, testear, certificar, publicar o simplemente "seguir", "continuar", "SPEC Kiro" y "modo brutal".

## Inicio obligatorio en cada ronda

1. **Cargar el skill de proyecto `cattech-engineering` con la herramienta nativa `skill` de OpenCode**. Ubicación: `.opencode/skills/cattech-engineering/SKILL.md`. Si la herramienta no está disponible, leer ese archivo directamente y consultar sus `references/` pertinentes. No confiar únicamente en instrucciones recordadas de rondas anteriores.
2. Antes de elegir tareas, comprobar GitHub: `main`, rama de desarrollo actual, PR abierto, SHA exacto, issues, roadmap, diff y CI del SHA. Una cifra o commit anterior no es prueba de estado vigente.
3. Trabajar con **SPEC Kiro feedback-first**: objetivos, requisitos, restricciones, riesgos, diseño, criterios de aceptación y evidencia. Priorizar la siguiente tarea segura no bloqueada.
4. Todo cambio debe ser atómico y versionado en una rama de desarrollo; comprobar el HEAD antes de escribir y no sobrescribir trabajo concurrente. `main`, tags y releases oficiales permanecen intactos sin un gate de integración/publicación explícitamente autorizado.
5. Cerrar cada slice con comprobación del diff remoto, tests focalizados y suite completa, CI Windows sobre el **SHA exacto**, y auditoría de riesgos. CI verde no equivale a validación física o funcional de reparaciones reales.
6. Informar estado verificable, commit, PR, CI/run, cantidad de tests y hallazgos. Si el CI está corriendo, declarar `PENDING`; si falla un gate, detener desarrollo dependiente y corregirlo antes de avanzar.

## Límites de seguridad

- Nunca ejecutar operaciones destructivas, `SFC /scannow`, `DISM /RestoreHealth`, reparaciones de Windows Update, ni cambios de servicios por una orden genérica "seguir".
- Diagnosticar antes de reparar; exigir autorización y controles específicos para cualquier reparación real.
- No desactivar antivirus, firewall ni servicios de seguridad como optimización.
- No subir minidumps, nombres reales de usuarios, seriales o logs privados a GitHub.
- No mergear el PR draft ni publicar releases para forzar avances o porcentajes.
- OpenCode/DeepSeek no deben declarar PASS por su propio relato: exponer evidencia que pueda contrastarse independientemente en GitHub.

## Guía

- Skill: `.opencode/skills/cattech-engineering/SKILL.md`
- GitHub/CI: `.opencode/skills/cattech-engineering/references/git-and-ci.md`
- Seguridad/reparaciones: `.opencode/skills/cattech-engineering/references/security-and-repair.md`
- Reportes/handoff: `.opencode/skills/cattech-engineering/references/report-and-handoff.md`

El repositorio versiona el skill para OpenCode y para lectura de otros agentes. **Esto no instala automáticamente el skill en la biblioteca de ChatGPT**: allí se importa el ZIP validado por separado.
