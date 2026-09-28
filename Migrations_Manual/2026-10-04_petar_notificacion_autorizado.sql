-- ============================================================================
-- PETAR — tipo de notificación in-app para avisar al EJECUTANTE que su PETAR
-- ya tiene las 3 firmas completas (Supervisor + SSOMA) y queda autorizado
-- para iniciar el trabajo de alto riesgo. Antes de esto, el trabajador no
-- tenía forma de saberlo salvo revisando manualmente la lista.
--
-- Idempotente.
-- ============================================================================
BEGIN;

INSERT INTO notificacion_tipo (codigo, nombre, orden, created_date_time, active, state)
VALUES ('PETAR_AUTORIZADO', 'PETAR autorizado para iniciar', 52, now(), true, true)
ON CONFLICT (codigo) WHERE (state = true)
DO UPDATE SET nombre = EXCLUDED.nombre, orden = EXCLUDED.orden, active = true, updated_date_time = now();

COMMIT;
