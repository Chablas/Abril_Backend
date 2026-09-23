-- ============================================================================
-- EPP — Agrega el nivel Familia entre Categoría e Ítem.
-- Antes: Categoría → Ítem → Modelo.
-- Ahora: Categoría → Familia (ej. "Barbiquejo") → Ítem/Variante (ej. "Barbiquejo
-- 2 puntas", "Barbiquejo 4 puntas", cada una con su propia ficha técnica) → Modelo/Marca.
-- Ejecutar DESPUÉS de ssoma_epp_feature.sql. Idempotente, migra ítems ya creados
-- (crea una familia 1:1 por cada ítem existente, para no perder datos).
-- ============================================================================
BEGIN;

CREATE TABLE IF NOT EXISTS ss_epp_familia (
    id            SERIAL PRIMARY KEY,
    nombre        VARCHAR(150) NOT NULL,
    categoria_id  INT NOT NULL REFERENCES ss_epp_categoria(id),
    orden         INT NOT NULL DEFAULT 0,
    activo        BOOLEAN NOT NULL DEFAULT TRUE,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at    TIMESTAMPTZ NULL
);

CREATE INDEX IF NOT EXISTS ix_ss_epp_familia_categoria_id ON ss_epp_familia(categoria_id);

ALTER TABLE ss_epp_item ADD COLUMN IF NOT EXISTS familia_id INT NULL REFERENCES ss_epp_familia(id);

-- Migra ítems ya creados antes de este cambio: una familia por cada ítem existente
-- (mismo nombre técnico), para no perder ni desordenar lo ya cargado.
INSERT INTO ss_epp_familia (nombre, categoria_id, orden, activo, created_at)
SELECT DISTINCT i.nombre_tecnico, i.categoria_id, 0, true, NOW()
FROM ss_epp_item i
WHERE i.familia_id IS NULL
  AND NOT EXISTS (
      SELECT 1 FROM ss_epp_familia f WHERE f.nombre = i.nombre_tecnico AND f.categoria_id = i.categoria_id
  );

UPDATE ss_epp_item i
SET familia_id = f.id
FROM ss_epp_familia f
WHERE i.familia_id IS NULL AND f.nombre = i.nombre_tecnico AND f.categoria_id = i.categoria_id;

ALTER TABLE ss_epp_item ALTER COLUMN familia_id SET NOT NULL;
ALTER TABLE ss_epp_item DROP COLUMN IF EXISTS categoria_id;

CREATE INDEX IF NOT EXISTS ix_ss_epp_item_familia_id ON ss_epp_item(familia_id);

COMMIT;

-- ── Verificación ─────────────────────────────────────────────────────────────
SELECT f.id, f.nombre, c.nombre AS categoria, f.orden
FROM ss_epp_familia f JOIN ss_epp_categoria c ON c.id = f.categoria_id
ORDER BY c.orden, f.orden;
