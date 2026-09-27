-- ============================================================================
-- Revierte el marcado automático que hice en 2026-10-01_ats_requiere_petar.sql.
-- El usuario fue claro: "requiere_petar" debe marcarlo el Coordinador SSOMA
-- desde el catálogo (opción disponible, no un valor por defecto que yo decida).
-- Deja la columna (ya construida) pero todos los riesgos vuelven a false.
--
-- Idempotente.
-- ============================================================================
BEGIN;

UPDATE ss_ats_riesgo SET requiere_petar = false WHERE requiere_petar = true;

COMMIT;

-- Verificación:
-- SELECT nombre, requiere_petar FROM ss_ats_riesgo WHERE requiere_petar = true; -- debe devolver 0 filas
