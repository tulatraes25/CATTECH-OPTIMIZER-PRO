# SPEC Kiro — Pasaporte vinculado a sesión y consentimiento revocable

**Issue:** #50 · **Estado inicial:** IMPLEMENTED / Windows CI pendiente.
**Baseline:** PR #49 SHA `9bfb9970aa9af5e2e2889d177eab13f2d5680c13`.
**Integración:** stacked DRAFT sobre #49; base `main` solo para GitHub Actions, nunca para fusionar directamente.

## GOAL
Evitar la reutilización y exposición accidental de un pasaporte de otro
equipo cuando cambie la selección, sin utilizar el PR #31 incompatible.

## REQUIREMENTS
1. `TechnicalPassportArtifact` conserva un `ContextId` inmutable de Core,
   válido y nunca serializado (`[JsonIgnore]`). El JSON público, formato v1,
   hash SHA-256 y bytes del payload no incorporan el identificador privado.
2. `TechnicalPassportReviewViewModel` recibe obligatoriamente un
   `Func<DiagnosticContextId>` con la sesión activa. Se deniega el permiso
   si es distinto, si falla el proveedor o si la vista previa está corrupta.
3. Al seleccionar otro cliente/equipo/sesión, el futuro coordinador DEBE
   llamar `InvalidateForCaseSelectionChange`: revoca para siempre el
   consentimiento de ese snapshot (incluso si se vuelve al contexto inicial)
   y limpia el JSON y SHA mostrados en la interfaz.
4. Al invocar copiar se valida otra vez el contexto y el hash; el comando
   también aplica estas condiciones cuando un llamador omite `CanExecute`.
5. Un callback reentrante no puede efectuar copias adicionales. Si el
   clipboard cambia de sesión durante la llamada, no declarar una copia
   segura; el contenido podría estar en el portapapeles.
6. Toda excepción del callback revoca la autorización y no incorpora detalles
   sensibles en el texto de estado. Debido al resultado ambiguo, no se
   intenta copiar otra vez sin nueva vista previa.
7. La UI del Pasaporte es **componente reutilizable no vinculado a datos
   reales**. Ninguna lectura de hardware, reporte de usuario, red ni
   escritura de archivo se crea desde este corte.

## NON-GOALS
No prueba identidad física, no borra el portapapeles del SO, no firma
digitalmente, no migra registros históricos, no conecta automáticamente
el selector real, no genera informes de clientes reales ni publica release.

## RIESGOS RESIDUALES
- La igualdad de GUID no autentica físicamente al equipo. El mapeo
  confiable entre equipo/sesión y sus sensores requiere trabajo posterior.
- Una navegación que cambia y vuelve sin avisar al coordinador no puede
  detectarse retroactivamente si el callback vuelve a entregar el mismo GUID.
- Revalidar antes y después de un callback externo no garantiza ausencia
  de exposición si el cambio ocurre DURANTE `writeClipboard`. La interfaz
  avisa expresamente que es posible que el JSON antiguo se haya copiado.
- La limpieza de `PreviewJson` no implica borrado del clipboard ni de
  otras capturas de pantalla / memoria del proceso.
- El snapshot sintético y la UI de revisión no constituyen una feature
  productiva habilitada. Mantener cerradas las rutas a datos reales.

## DESIGN / TASKS
1. Proteger `TechnicalPassportArtifact.ContextId` fuera de la carga pública.
2. Requerir selección activa explícita y revocación irreversible en la
   vista previa; limpiar pantalla al invalidar.
3. Guardar estado de copia en progreso para bloquear reentrancia.
4. Migrar fixtures del constructor antiguo de dos argumentos.
5. Tests adversariales sin clipboard real: proveedor nulo o defectuoso,
   contexto incorrecto al abrir/copiar, cambio antes/durante la copia,
   navegación que vuelve a la sesión previa, callback anidado, excepción
   tras efecto externo, privacidad de JSON y checksum.
6. Revisar CI SHA exacto, diff y logs; no marcar listo para release.

## ACCEPTANCE
- Ningún permiso antiguo puede copiar en una sesión distinta.
- Ningún preview antiguo permanece visible una vez revocado.
- No hay constructor público que omita el proveedor de contexto.
- En falla de clipboard, no se informa éxito ni se reusa consentimiento.
- `TechnicalPassportPublicPayload` no incluye GUID de sesión, hash sin
  cambio respecto a bytes públicos.
- Windows Restore + Release build + suite completa 0 fallos/0 omitidos
  sobre commit exacto; gate interactivo Windows pendiente.

## EVIDENCE / DISPOSITION
Issue #50 permanece abierto hasta que el coordinador de selección real
aplique la revocación a toda navegación relevante y QA Windows 10/11
valide la interfaz. La ejecución de Windows Actions verifica únicamente
este modelo y fixtures sintéticos, no la seguridad física del producto.
