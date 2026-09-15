-- ============================================================================
-- Activos Rotativos: la categoría deja de ser obligatoria — se simplifica el
-- alta a solo el material (nombre, código, estado, proyecto, contacto).
-- Ejecutar en PRODUCCIÓN. Idempotente.
-- ============================================================================
BEGIN;

ALTER TABLE ss_activo_rotativo
    ALTER COLUMN categoria_id DROP NOT NULL;

COMMIT;
