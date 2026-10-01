-- ============================================================================
-- ATS Grupal — evidencia de presencia del Capataz/Maestro de obra al firmar por
-- link público (no tiene cuenta, así que se le exige selfie + geolocalización +
-- hora del dispositivo, igual que a los obreros que se adhieren).
-- Complementa 2026-09-30_ats_grupo_firma_capataz.sql. Idempotente.
-- ============================================================================

BEGIN;

ALTER TABLE ss_ats_grupo ADD COLUMN IF NOT EXISTS capataz_selfie_url text;
ALTER TABLE ss_ats_grupo ADD COLUMN IF NOT EXISTS capataz_selfie_hash text;
ALTER TABLE ss_ats_grupo ADD COLUMN IF NOT EXISTS capataz_lat numeric;
ALTER TABLE ss_ats_grupo ADD COLUMN IF NOT EXISTS capataz_lng numeric;
ALTER TABLE ss_ats_grupo ADD COLUMN IF NOT EXISTS capataz_precision_metros numeric;
ALTER TABLE ss_ats_grupo ADD COLUMN IF NOT EXISTS capataz_hora_dispositivo timestamp;

COMMIT;

-- Verificación (no modifica nada):
-- SELECT column_name FROM information_schema.columns
-- WHERE table_name = 'ss_ats_grupo' AND column_name LIKE 'capataz_%';
-- Esperado: 13 filas en total (7 anteriores + 6 de este script).
