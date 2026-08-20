-- ============================================================================
-- Reclutamiento (Gestión GTH) — correos que faltaban en «Configuración»
--
-- Contexto: todos los correos SALIENTES de Reclutamiento pasan a enviarse desde
-- gth@abril.pe (buzón «Gth» de Email:Senders). Eso ya está en el código; este
-- script solo agrega a la pantalla de Configuración los correos que salían sin
-- poder administrarse:
--   • FORMULARIO_ENVIO      — el formulario que GTH le manda al postulante.
--   • FORMULARIO_CORRECCION — las observaciones cuando GTH rechaza un formulario.
--   • AGRADECIMIENTO        — el "gracias por participar" a quien no continúa.
--
-- Los tres tienen principal_automatico = true: el destinatario principal (el
-- postulante / el candidato) lo pone el sistema al enviar, así que la pantalla
-- solo agrega principales adicionales y copias. Por eso NO se les inserta
-- ningún destinatario: GTH es el emisor de estos correos, no el destinatario.
--
-- Idempotente: se puede correr más de una vez sin duplicar nada.
-- ============================================================================

BEGIN;

-- 1) Hueco en el orden para que la pantalla siga el flujo real del proceso:
--    long list → formulario → completado → correcciones → entrevista →
--    decisión de finalista → agradecimiento.
UPDATE gth_correo_tipo SET orden =  9 WHERE codigo = 'ENTREVISTA'         AND state = true AND orden <> 9;
UPDATE gth_correo_tipo SET orden = 10 WHERE codigo = 'FINALISTA_DECISION' AND state = true AND orden <> 10;

-- 2) Los tres correos nuevos.
INSERT INTO gth_correo_tipo (codigo, nombre, descripcion, principal_automatico, orden, active, state, created_date_time)
SELECT v.codigo, v.nombre, v.descripcion, true, v.orden, true, true, now()
FROM (VALUES
    ('FORMULARIO_ENVIO', 6, 'Formulario enviado al postulante',
     'Se envía al postulante cuando GTH le manda el formulario de información, uno por uno o en lote, con el enlace para llenarlo. El destinatario principal es SIEMPRE el postulante; acá solo se definen principales adicionales y copias.'),
    ('FORMULARIO_CORRECCION', 8, 'Correcciones del formulario al postulante',
     'Se envía al postulante cuando GTH rechaza su formulario: lleva las observaciones y el mismo enlace del envío original para que lo corrija. El destinatario principal es SIEMPRE el postulante; acá solo se definen principales adicionales y copias.'),
    ('AGRADECIMIENTO', 11, 'Agradecimiento a quien no continúa',
     'Se envía al candidato que no sigue en el proceso, ya sea porque GTH lo marca como "no continúa" tras la entrevista o porque el solicitante rechaza a un finalista. Es el mismo correo en ambos casos. El destinatario principal es SIEMPRE el candidato; acá solo se definen principales adicionales y copias.')
) AS v(codigo, orden, nombre, descripcion)
WHERE NOT EXISTS (
    SELECT 1 FROM gth_correo_tipo t WHERE t.codigo = v.codigo AND t.state = true
);

COMMIT;

-- Verificación
-- SELECT codigo, nombre, orden, principal_automatico, active FROM gth_correo_tipo WHERE state ORDER BY orden;
