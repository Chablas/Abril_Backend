-- ============================================================================
-- ATS Digital — tipo de notificación in-app (campanita) para avisar al Residente
-- y al Coordinador SSOMA que tienen un ATS pendiente de su firma.
--
-- Reemplaza el correo por evento (que satura la bandeja si hay varios ATS al
-- día) por la notificación in-app que ya usa el resto del sistema — un solo
-- canal, con su propio contador de no leídas en la campanita del encabezado.
--
-- Idempotente (ON CONFLICT DO UPDATE, mismo patrón que gth_aprobacion_gg).
-- ============================================================================
BEGIN;

INSERT INTO notificacion_tipo (codigo, nombre, orden, created_date_time, active, state)
VALUES ('ATS_PENDIENTE_FIRMA', 'ATS pendiente de tu firma', 50, now(), true, true)
ON CONFLICT (codigo) WHERE (state = true)
DO UPDATE SET nombre = EXCLUDED.nombre, orden = EXCLUDED.orden, active = true, updated_date_time = now();

COMMIT;
