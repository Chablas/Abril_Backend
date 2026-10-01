-- ============================================================================
-- ATS Grupal — firma ÚNICA del Capataz/Maestro de obra por cuadrilla, vía link
-- público (el capataz muchas veces no tiene cuenta en la plataforma).
--
-- capataz_adhesiones_al_firmar = cuántos trabajadores ya habían adherido cuando
-- el capataz firmó. Si después se suma alguien, el conteo actual supera ese
-- número y el panel marca la firma como "pendiente de re-firma".
--
-- Idempotente (ADD COLUMN IF NOT EXISTS).
-- ============================================================================

BEGIN;

ALTER TABLE ss_ats_grupo ADD COLUMN IF NOT EXISTS capataz_worker_id int REFERENCES workers(id);
ALTER TABLE ss_ats_grupo ADD COLUMN IF NOT EXISTS capataz_nombre text;
ALTER TABLE ss_ats_grupo ADD COLUMN IF NOT EXISTS capataz_cargo text;
ALTER TABLE ss_ats_grupo ADD COLUMN IF NOT EXISTS capataz_firma_url text;
ALTER TABLE ss_ats_grupo ADD COLUMN IF NOT EXISTS capataz_firma_hash text;
ALTER TABLE ss_ats_grupo ADD COLUMN IF NOT EXISTS capataz_hora_servidor timestamp;
ALTER TABLE ss_ats_grupo ADD COLUMN IF NOT EXISTS capataz_adhesiones_al_firmar int;

COMMIT;

-- Verificación (no modifica nada):
-- SELECT column_name FROM information_schema.columns
-- WHERE table_name = 'ss_ats_grupo' AND column_name LIKE 'capataz_%';
-- Esperado: 7 filas.
