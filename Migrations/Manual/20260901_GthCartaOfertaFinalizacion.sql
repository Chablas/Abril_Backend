-- ============================================================================
-- Gestión GTH · Reclutamiento — Cierre de la carta oferta por el colaborador
-- Fecha: 2026-09-01
--
-- Tres cosas, todas alrededor del mismo cambio: la página pública de la carta
-- oferta gana un paso final explícito («Finalizar») después de firmar.
--
--   1. gth_carta_oferta.primera_apertura_date_time — la primera vez que el
--      colaborador abrió su enlace. Es la fecha de conformidad que imprime el
--      formato de aceptación del documento ({{FECHA_HOY_CONFORMIDAD_DE_COLABORADOR}}).
--      Se escribe una sola vez y no se vuelve a tocar.
--
--   2. gth_carta_oferta.finalizada_date_time — cuándo pulsó «Finalizar». Es su
--      cierre explícito del trámite: desde ahí el documento firmado es el
--      definitivo (ni él vuelve a firmar ni GTH lo reemplaza) y sale el aviso al
--      solicitante.
--
--   3. El tipo de correo CARTA_OFERTA_FINALIZADA, que es ese aviso. Va al
--      solicitante de la vacante, con principal automático, igual que sus dos
--      gemelos ENTREVISTA_CONFIRMADA_SOLICITANTE y CANDIDATO_RETOMADO. Sin
--      destinatarios configurados: los correos que van al solicitante no llevan
--      ninguno, solo su principal automático.
--
-- Las cartas ya firmadas quedan con las dos columnas en NULL, que es lo correcto:
-- se firmaron cuando el paso de finalizar no existía. Para GTH eso se lee como
-- «firmada pero no finalizada», y el detalle las sigue dejando aprobar igual.
--
-- Idempotente: ADD COLUMN IF NOT EXISTS y el INSERT del tipo guardado por
-- NOT EXISTS. Seguro de correr tal cual en dev, demo y producción.
-- ============================================================================

BEGIN;

SET client_encoding TO 'UTF8';

-- 1) Las dos marcas de tiempo de la página pública -------------------------
ALTER TABLE gth_carta_oferta
    ADD COLUMN IF NOT EXISTS primera_apertura_date_time timestamptz NULL,
    ADD COLUMN IF NOT EXISTS finalizada_date_time       timestamptz NULL;

COMMENT ON COLUMN gth_carta_oferta.primera_apertura_date_time IS
    'Primera vez que el colaborador abrio su enlace publico. Es la fecha de conformidad que imprime el formato de aceptacion de la carta. Se escribe una sola vez.';

COMMENT ON COLUMN gth_carta_oferta.finalizada_date_time IS
    'Cuando el colaborador pulso Finalizar en la pagina publica. Cierra el tramite: el documento firmado pasa a ser definitivo y sale el aviso al solicitante.';

-- 2) El tipo de correo nuevo -----------------------------------------------
-- orden 25: va justo despues de CARTA_OFERTA_FIRMADA (24), que es el otro aviso
-- que dispara el mismo boton.
INSERT INTO gth_correo_tipo (codigo, nombre, descripcion, orden,
                             principal_automatico, principal_automatico_active,
                             principal_automatico_nombre, active, state, created_date_time)
SELECT v.codigo, v.nombre, v.descripcion, v.orden,
       v.principal_automatico, true, v.principal_nombre, true, true, now()
FROM  (VALUES
    ('CARTA_OFERTA_FINALIZADA',
     'Carta oferta finalizada (al solicitante)',
     'Le avisa al solicitante que el colaborador ya firmó y finalizó su carta oferta, con la fecha de ingreso confirmada. Es informativo: no pide nada, cierra el círculo de la vacante que pidió.',
     25, true, 'Solicitante del requerimiento')
) AS v(codigo, nombre, descripcion, orden, principal_automatico, principal_nombre)
WHERE NOT EXISTS (
    SELECT 1 FROM gth_correo_tipo t WHERE t.codigo = v.codigo AND t.state = true
);

-- Nombre y descripción también en el rerun, por si el texto se afina después.
UPDATE gth_correo_tipo t
SET    nombre                      = v.nombre,
       descripcion                 = v.descripcion,
       principal_automatico        = v.principal_automatico,
       principal_automatico_nombre = v.principal_nombre,
       updated_date_time           = now()
FROM  (VALUES
    ('CARTA_OFERTA_FINALIZADA',
     'Carta oferta finalizada (al solicitante)',
     'Le avisa al solicitante que el colaborador ya firmó y finalizó su carta oferta, con la fecha de ingreso confirmada. Es informativo: no pide nada, cierra el círculo de la vacante que pidió.',
     true, 'Solicitante del requerimiento'::text)
) AS v(codigo, nombre, descripcion, principal_automatico, principal_nombre)
WHERE t.codigo = v.codigo AND t.state = true;

COMMIT;
