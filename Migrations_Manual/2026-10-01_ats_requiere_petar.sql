-- ============================================================================
-- ATS Digital — marca qué riesgos del catálogo exigen PETAR (Trabajo en altura,
-- izaje, eléctrico, etc.). Si un ATS firmado tiene algún riesgo marcado así y no
-- tiene ningún PETAR generado, la lista lo muestra con un badge de advertencia
-- — no se bloquea el ATS (ver razonamiento en el chat), es un aviso, no un gate.
--
-- Idempotente (ADD COLUMN IF NOT EXISTS + UPDATE por nombre, se puede re-correr).
-- ============================================================================

BEGIN;

ALTER TABLE ss_ats_riesgo ADD COLUMN IF NOT EXISTS requiere_petar boolean NOT NULL DEFAULT false;

UPDATE ss_ats_riesgo SET requiere_petar = true
WHERE nombre IN (
    'Caída de personas a distinto nivel',
    'Caída de objetos a distinto nivel',
    'Caída de objetos, herramientas a distinto nivel',
    'Contacto directo e indirecto con E.E.',
    'Aplastamiento',
    'Atrapamiento, aplastamiento'
);

COMMIT;

-- ============================================================================
-- Verificación (correr después; no modifica nada)
-- ============================================================================
-- SELECT nombre, requiere_petar FROM ss_ats_riesgo WHERE requiere_petar = true;
