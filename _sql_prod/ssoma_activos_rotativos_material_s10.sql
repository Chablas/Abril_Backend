-- ============================================================================
-- Vincula cada Material de Activos Rotativos a su ítem real del catálogo de
-- Presupuesto Materiales (S10), para poder comparar cuánto se compró/despachó
-- (Kardex de egresos, acumulado global) contra cuántos activos hay registrados.
-- Ejecutar en PRODUCCIÓN. Idempotente.
-- ============================================================================
BEGIN;

ALTER TABLE ss_activo_rotativo_material
    ADD COLUMN IF NOT EXISTS presupuesto_item_id INT NULL REFERENCES ss_material_item(id);

COMMIT;
