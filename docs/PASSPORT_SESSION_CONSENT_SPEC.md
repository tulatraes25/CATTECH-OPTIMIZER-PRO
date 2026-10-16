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

## Segundo corte — autoridad de seleccion y revocacion por epoca

**GOAL**: corregir el problema ABA (seleccionar A -> B -> A) sin confiar
solamente en la igualdad del GUID del caso. El pasaporte conserva su
`ContextId` privado; la revision obtiene ahora una capacidad temporal
`DiagnosticSessionLease` emitida por `DiagnosticSessionCoordinator`.

**REQUIREMENTS**:
- `StartNewCase` genera un contexto logico nuevo. `SelectCase` solo
  recibe un contexto ya validado por el adaptador dueño; su invocacion
  renueva SIEMPRE una epoca opaca, incluso si se selecciona el mismo GUID.
- `CaptureLeaseFor(context)` devuelve `null` salvo que el caso sea
  el actualmente seleccionado; la capacidad no tiene constructores
  publicos ni serializa contexto o epoca, y `IsCurrent` hace la
  comprobacion en memoria sobre la autoridad que la emitio.
- La API PUBLICA de la vista y ViewModel del Pasaporte requiere la
  `DiagnosticSessionLease`. El antiguo callback
  `Func<DiagnosticContextId>` queda solo como constructor interno
  para tests sinteticos: ya no es una via publica hacia exportaciones.
- El ViewModel utiliza `DiagnosticSessionLease.SubscribeRevocation`:
  la vista NO obtiene el coordinador mutable ni puede seleccionar casos
  a traves de la capacidad. La notificacion se emite una sola vez aunque
  haya multiples transiciones. `Dispose` revoca y cancela la suscripcion;
  `Unloaded` del UserControl lo ejecuta.
- Se revalida la capacidad inmediatamente al invocar la copia, y al
  volver del callback; una navegacion que cambia y vuelve en medio
  de un callback ya no puede rehabilitar el permiso.
- La seleccion es sincronica y protegida con `lock`; un listener
  defectuoso no bloquea los siguientes. En WPF el dueño debe producir
  eventos en el dispatcher de UI; si una seleccion llega desde otro
  hilo el ViewModel despacha la limpieza visual al UI, mientras la
  validacion de la epoca falla inmediatamente en todo hilo.

**NON-GOALS**: no autenticar el hardware fisico, no decidir por si
solo que una seleccion de cliente/equipo editable representa un caso
valido; no copiar archivos, no leer sensores, no habilitar el pasaporte
productivo ni publicar release.

**RISKS**:
- La capacidad protege una seleccion logica, NO identifica por si
  sola al equipo real ni la procedencia de sensores.
- El futuro adaptador de seleccion DEBE llamar a `SelectCase` o
  `ClearSelection` ante TODA apertura/cambio/reapertura de cliente
  o equipo, incluso si el GUID seleccionado es el mismo.
- No convertir un formulario editable de cliente en autoridad de caso.
  La aplicacion todavia carece de un coordinador conectado a un
  identificador de servicio comprobado; esa integracion queda
  bloqueada por #43 y requiere una SPEC separada.
- Los datos del clipboard previamente copiados no se borran. El
  cambio concurrente mientras el callback ejecuta la escritura
  puede producir una exposicion que el codigo no puede deshacer.
- Capturas visuales en Windows real, DPI, accesibilidad, cambios
  de ventana y errores de clipboard deben validarse por separado.

**TASKS / ACCEPTANCE**:
- Nueva capacidad de seleccion con pruebas de epoca ABA, mismo
  GUID reabierto, contexto vacio, limpieza, aislamiento de listeners,
  privacidad JSON, constructor no publico y lease obsoleta.
- Contratos publicos de Review VM y WPF View requieren lease.
- Pruebas de auto-revocacion, callback que cambia A/B/A durante
  escritura, dispose, y permanencia del resultado.
- CI Windows SHA exacto: Restore + Release Build + suite xUnit
  completa, cero fallos y cero omitidos. Ninguna operacion con
  equipos reales o portapapeles del OS durante los tests.

**DISPOSITION**: implementation CI pendiente al abrir esta etapa;
#50 y #43 siguen OPEN hasta integrar un dueño confiable del caso,
navegacion real y QA Windows. No mergear el PR apilado a main.
