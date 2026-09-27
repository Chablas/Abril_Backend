-- ============================================================================
-- ATS Digital — firma múltiple: además del ejecutante (ya existente), agrega
-- dos firmas adicionales sobre el mismo ATS ya firmado:
--   - "Autoriza": Residente / Ingeniero de Producción del proyecto — firma que
--     la actividad está planificada y el frente en condiciones.
--   - "Visto Bueno SSOMA": Prevencionista / Coordinador SSOMA — verificación
--     técnica del análisis de riesgo (Art. 76 Reglamento Ley 29783, DS-005-2012-TR).
--
-- Ninguna de las dos reemplaza la firma del ejecutante (worker_id/firma_url ya
-- existentes) ni el estado "Firmado" — son adicionales y pueden quedar
-- pendientes sin bloquear la generación del PDF (se imprime "PENDIENTE" en el
-- espacio del firmante que falte).
--
-- Idempotente (ADD COLUMN IF NOT EXISTS).
-- ============================================================================

BEGIN;

ALTER TABLE ss_ats ADD COLUMN IF NOT EXISTS autoriza_worker_id int REFERENCES workers(id);
ALTER TABLE ss_ats ADD COLUMN IF NOT EXISTS autoriza_nombre text;
ALTER TABLE ss_ats ADD COLUMN IF NOT EXISTS autoriza_cargo text;
ALTER TABLE ss_ats ADD COLUMN IF NOT EXISTS autoriza_firma_url text;
ALTER TABLE ss_ats ADD COLUMN IF NOT EXISTS autoriza_firma_hash text;
ALTER TABLE ss_ats ADD COLUMN IF NOT EXISTS autoriza_hora_servidor timestamp;

ALTER TABLE ss_ats ADD COLUMN IF NOT EXISTS ssoma_worker_id int REFERENCES workers(id);
ALTER TABLE ss_ats ADD COLUMN IF NOT EXISTS ssoma_nombre text;
ALTER TABLE ss_ats ADD COLUMN IF NOT EXISTS ssoma_cargo text;
ALTER TABLE ss_ats ADD COLUMN IF NOT EXISTS ssoma_firma_url text;
ALTER TABLE ss_ats ADD COLUMN IF NOT EXISTS ssoma_firma_hash text;
ALTER TABLE ss_ats ADD COLUMN IF NOT EXISTS ssoma_hora_servidor timestamp;

COMMIT;

-- ============================================================================
-- Verificación (correr después; no modifica nada)
-- ============================================================================
-- SELECT column_name FROM information_schema.columns
-- WHERE table_name = 'ss_ats' AND column_name LIKE 'autoriza_%' OR column_name LIKE 'ssoma_%';
-- Esperado: 12 filas (6 + 6).
