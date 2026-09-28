-- ============================================================================
-- PETAR — tipo de notificación in-app para avisar al Residente y al Coordinador
-- SSOMA que un PETAR fue cerrado (el trabajo de alto riesgo finalizó).
-- Mismo patrón que ATS_PENDIENTE_FIRMA (2026-09-29): campanita, no correo.
--
-- Idempotente.
-- ============================================================================
BEGIN;

INSERT INTO notificacion_tipo (codigo, nombre, orden, created_date_time, active, state)
VALUES ('PETAR_CERRADO', 'PETAR cerrado', 51, now(), true, true)
ON CONFLICT (codigo) WHERE (state = true)
DO UPDATE SET nombre = EXCLUDED.nombre, orden = EXCLUDED.orden, active = true, updated_date_time = now();

COMMIT;
