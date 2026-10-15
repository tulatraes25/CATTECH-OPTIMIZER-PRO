# CATTECH Technical Passport EE-05A — Public v1

**Estado:** Core implementado en rama aislada. NO hay exportación automática, UI integrada, firma digital, instalación publicada ni aprobación de QA real. Relacionado con Issue #21 y PR de Evidence Engine #23.

## Objetivo

Producir, **solo a pedido del técnico y con consentimiento para compartir**, un resumen estructurado del diagnóstico y las verificaciones antes/después, sin emitir por defecto valores sensibles ni datos que puedan identificar al cliente o al dispositivo.

## Alcance y contrato de salida

`TechnicalPassportBuilder.Build(freshExportId, issuedAt, evidence, diagnostics, outcomes)` genera un `TechnicalPassportArtifact` **solo en memoria**. El JSON contiene únicamente:
- Identificador nuevo de la exportación y hora de emisión en UTC;
- esquema versionado `cattech.technical-passport.public.v1`;
- recuentos de observaciones por disponibilidad para evidencia **no sensible**;
- recuentos de estados diagnósticos y de verificaciones posteriores;
- aviso explícito sobre limitaciones del checksum.

Las entradas se capturan con límites para evitar colecciones no acotadas. Se rechazan datos nulos, IDs repetidos, referencias a evidencias inexistentes y observaciones con fecha posterior a la emisión. Se ordenan los estados antes de serializar, de modo que un snapshot equivalente genera el mismo JSON.

## Privacidad y limitaciones

- **Nunca** se copian al payload los campos `EvidenceRecord.Value`, `SourceId`, `FactKey`, `Provenance`, `Limitations`, ID individual de la evidencia, fechas de medición, nombres, seriales, rutas o contenido de minidumps.
- Se excluyen enteramente las observaciones marcadas `Sensitive`; además se omiten los diagnósticos y resultados vinculados directamente con sus IDs.
- Se conservan agregados de diagnósticos y resultados, que **pueden seguir revelando información de la condición del equipo**. Por eso el JSON minimizado **no garantiza anonimato**.
- Reglas con carencias de datos que no tienen IDs de evidencia vinculables merecen revisión antes de cualquier exposición a un cliente; la UI deberá incluir un gate manual de aprobación.
- La vinculación a un expediente, cliente o equipo queda fuera del esquema público y no debe reutilizarse como `freshExportId`. Generar un GUID nuevo para cada exportación.
- No se almacena ni comparte nada automáticamente. Cualquier futura implementación de guardado deberá exigir ruta autorizada, elección consciente, retención, permisos apropiados y revisión de datos antes de enviar.
- Los recuentos tienen valor descriptivo. Un estado `ObservedImprovement` expresa una condición observada, **no** demuestra causalidad ni garantiza una reparación exitosa.

## Integridad

`Sha256Hex` es el SHA-256 calculado sobre los **bytes UTF-8 exactos** de `PayloadJson`; `MatchesDigest` compara el contenido contra el digest. Detecta errores/cambios si el valor esperado del digest se preserva por separado. **No aporta autenticidad, cadena de custodia, firma digital ni protección frente a un atacante capaz de reemplazar tanto JSON como hash**.

No se agregan claves privadas al código ni se inventan certificados. La autenticidad y la firma verificable requieren un diseño posterior de identidad, custodia de claves y revocación.

## Pruebas de aceptación

- JSON byte-estable entre entradas equivalentes en distinto orden.
- Digest reproducible y detección de manipulación del JSON.
- Marcadores de privacidad, rutas, fuentes, IDs individuales, códigos crudos y textos arbitrarios ausentes del payload.
- Evidencia inaccesible nunca presentada como observada.
- IDs duplicados, referencias ausentes, enums inválidos, timestamps futuros y listas excesivas rechazados.
- Build Release + suite completa GitHub Windows CI sobre SHA exacto, 0 errores y 0 tests omitidos.
- Sin ejecución de comandos, sin escritura a disco y sin envío externo.

## Próximo slice

EE-05B: revisión adversarial de datos derivados sensibles, correlación de casos entre exportaciones y UI de revisión/consentimiento; realizar QA con equipos Windows reales **antes** de declarar el pasaporte apto para clientes. Mantener `main`/releases intactos.


## EE-05B — Componente WPF de revisión y consentimiento (experimental)

La vista `TechnicalPassportReviewView` y su `TechnicalPassportReviewViewModel` aceptan
un `TechnicalPassportArtifact` ya preparado. No recolectan datos del dispositivo,
no crean expedientes, no inventan contenido de prueba y **no están registrados en
`MainViewModel`**, porque todavía falta el adaptador seguro de diagnósticos reales.

El técnico puede revisar el JSON exacto y su digest. Dos confirmaciones independientes,
inicialmente desmarcadas, son obligatorias: haber revisado el contenido y consentir
la copia **al portapapeles de Windows**. La orden de copia vuelve a verificar
el consentimiento y el digest, copia una sola vez por instancia y revoca las
confirmaciones tras el éxito. Un fallo de portapapeles no se informa como éxito
ni filtra mensajes de excepción. No hay envío de red ni almacenamiento en disco.

**Alcance explícito de consentimiento:** solo la copia manual de este snapshot;
no autoriza correo, nube, impresión, otro archivo ni otra exportación. Un nuevo
snapshot requiere otra instancia y confirmaciones nuevas. Copiar al portapapeles
puede exponer el JSON a otras aplicaciones; la interfaz lo informa.

**Límite importante:** esto es una defensa del flujo de interfaz propuesto, no
un control global de seguridad. La API Core `TechnicalPassportBuilder.Build`
todavía expone el JSON en memoria a sus consumidores. Todo futuro comando de
guardar, subir o compartir deberá implementar autorización explícita propia y
garantizar que el contenido revisado es **exactamente** el que se emitirá. Hasta
conectar los proveedores reales, no corresponde presentar esta pantalla como
funcionalidad terminada de la aplicación.

**Pruebas:** confirmar que abrir una vista no copia nada; impedir copias con
una sola casilla; inhabilitar después de copia; preservar bytes exactos; rechazar
capturas de snapshots nuevos sin autorización; tratar errores del portapapeles
sin falsos positivos. La QA interactiva WPF queda pendiente.
