-- ============================================================================
-- Checklist de proyecto: estado "no_aplica" con motivo obligatorio.
-- Para proyectos avanzados que ya pasaron esa etapa antes de que el checklist
-- existiera, o que genuinamente no les corresponde.
-- Ejecutar en PRODUCCIÓN. Idempotente.
-- ============================================================================
BEGIN;

ALTER TABLE ss_checklist_proyecto
    ADD COLUMN IF NOT EXISTS no_aplica_motivo TEXT NULL,
    ADD COLUMN IF NOT EXISTS no_aplica_por_id INT NULL,
    ADD COLUMN IF NOT EXISTS no_aplica_fecha TIMESTAMPTZ NULL;

COMMIT;
