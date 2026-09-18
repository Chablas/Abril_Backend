-- ============================================================================
-- EPP — Ficha técnica en PDF por ítem.
-- Ejecutar DESPUÉS de ssoma_epp_familia.sql. Idempotente.
-- ============================================================================
BEGIN;

ALTER TABLE ss_epp_item ADD COLUMN IF NOT EXISTS ficha_tecnica_url VARCHAR(500) NULL;
ALTER TABLE ss_epp_item ADD COLUMN IF NOT EXISTS ficha_tecnica_nombre_archivo VARCHAR(300) NULL;

COMMIT;
