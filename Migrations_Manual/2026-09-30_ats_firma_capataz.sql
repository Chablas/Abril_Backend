-- ============================================================================
-- ATS Digital — firma de Capataz/Maestro de obra: tercer nivel de la cadena de
-- aprobación, ANTERIOR a Autoriza/Visto Bueno SSOMA (ya existentes, ver
-- 2026-09-28_ats_firma_multiple.sql):
--   - "Capataz": Capataz / Maestro de obra de la cuadrilla — firma de campo,
--     el primer nivel que valida el análisis antes de escalar.
--
-- Solo se pide cuando el EJECUTANTE del ATS es obrero de obra (Worker no está
-- en Staff/Oficina Central, ver worker.obra_oficina_staff_id) — si el propio
-- ejecutante ya es Staff (ej. un capataz haciendo su propio ATS), su firma de
-- ejecutante cubre este nivel y no se pide de nuevo (evita autovalidación).
-- Decisión de Samuel 2026-09-30.
--
-- No reemplaza ninguna firma existente ni el estado "Firmado" — es adicional y
-- puede quedar pendiente sin bloquear la generación del PDF (se imprime
-- "PENDIENTE" en el espacio del firmante que falte, igual que Autoriza/SSOMA).
--
-- Idempotente (ADD COLUMN IF NOT EXISTS).
-- ============================================================================

BEGIN;

ALTER TABLE ss_ats ADD COLUMN IF NOT EXISTS capataz_worker_id int REFERENCES workers(id);
ALTER TABLE ss_ats ADD COLUMN IF NOT EXISTS capataz_nombre text;
ALTER TABLE ss_ats ADD COLUMN IF NOT EXISTS capataz_cargo text;
ALTER TABLE ss_ats ADD COLUMN IF NOT EXISTS capataz_firma_url text;
ALTER TABLE ss_ats ADD COLUMN IF NOT EXISTS capataz_firma_hash text;
ALTER TABLE ss_ats ADD COLUMN IF NOT EXISTS capataz_hora_servidor timestamp;

COMMIT;

-- ============================================================================
-- Verificación (correr después; no modifica nada)
-- ============================================================================
-- SELECT column_name FROM information_schema.columns
-- WHERE table_name = 'ss_ats' AND column_name LIKE 'capataz_%';
-- Esperado: 6 filas.
