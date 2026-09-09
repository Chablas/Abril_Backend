-- ============================================================================
-- Activos Rotativos: "categoría" pasa a ser "Material" — catálogo único y
-- obligatorio del que se elige el activo al crearlo (con opción de agregar uno
-- nuevo si no está en la lista). Reutiliza la tabla de categorías ya sembrada
-- (Tambor Retráctil, Freno de Cuerda, etc. — ya son materiales válidos).
-- Ejecutar en PRODUCCIÓN. Idempotente.
-- ============================================================================
BEGIN;

-- ── Renombrar tabla y columna ────────────────────────────────────────────────
ALTER TABLE IF EXISTS ss_activo_rotativo_categoria RENAME TO ss_activo_rotativo_material;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'ss_activo_rotativo' AND column_name = 'categoria_id'
    ) THEN
        ALTER TABLE ss_activo_rotativo RENAME COLUMN categoria_id TO material_id;
    END IF;
END $$;

-- ── Backfill: cualquier activo sin material (creado durante la etapa "simple"
--    sin categoría) recibe el primer material del catálogo como default ──────
UPDATE ss_activo_rotativo
SET material_id = (SELECT id FROM ss_activo_rotativo_material ORDER BY id LIMIT 1)
WHERE material_id IS NULL;

-- ── Ahora material_id es obligatorio ──────────────────────────────────────────
ALTER TABLE ss_activo_rotativo
    ALTER COLUMN material_id SET NOT NULL;

COMMIT;

-- ── Verificación ─────────────────────────────────────────────────────────────
SELECT m.nombre AS material, COUNT(a.id) AS total_activos
FROM ss_activo_rotativo_material m
LEFT JOIN ss_activo_rotativo a ON a.material_id = m.id
GROUP BY m.nombre
ORDER BY m.nombre;
