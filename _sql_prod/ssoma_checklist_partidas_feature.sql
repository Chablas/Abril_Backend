-- ============================================================================
-- Checklist por Partida (etapa constructiva) + galería de fotos de referencia.
-- Ejecutar en PRODUCCIÓN. Idempotente.
-- ============================================================================
BEGIN;

-- ── Tabla de partidas ────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS ss_checklist_partida (
    id          SERIAL PRIMARY KEY,
    nombre      VARCHAR(150) NOT NULL,
    descripcion TEXT NULL,
    orden       INT NOT NULL DEFAULT 0,
    activo      BOOLEAN NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ NULL
);

-- ── Plantilla pertenece opcionalmente a una partida ──────────────────────────
ALTER TABLE ss_checklist_plantilla
    ADD COLUMN IF NOT EXISTS partida_id INT NULL REFERENCES ss_checklist_partida(id);

CREATE INDEX IF NOT EXISTS ix_ss_checklist_plantilla_partida_id
    ON ss_checklist_plantilla(partida_id);

-- ── Galería de fotos de referencia por item ("cómo debe quedar") ────────────
CREATE TABLE IF NOT EXISTS ss_checklist_plantilla_item_imagen (
    id                SERIAL PRIMARY KEY,
    plantilla_item_id INT NOT NULL REFERENCES ss_checklist_plantilla_item(id) ON DELETE CASCADE,
    url               VARCHAR(500) NOT NULL,
    orden             INT NOT NULL DEFAULT 0,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_ss_checklist_plantilla_item_imagen_item_id
    ON ss_checklist_plantilla_item_imagen(plantilla_item_id);

-- ── Seed de partidas iniciales (ejemplo, ajustar/ampliar según obra real) ────
INSERT INTO ss_checklist_partida (nombre, descripcion, orden, activo, created_at, updated_at)
SELECT v.nombre, v.descripcion, v.orden, true, NOW(), NOW()
FROM (VALUES
  ('Muro Anclado',        'Excavación y anclajes de muro pantalla / muro anclado',        1),
  ('Excavación Masiva',   'Movimiento de tierras y taludes',                              2),
  ('Estructuras',         'Encofrado, acero y vaciado de concreto',                       3),
  ('Instalaciones',       'Instalaciones sanitarias, eléctricas y mecánicas',             4),
  ('Acabados',            'Acabados arquitectónicos',                                     5)
) AS v(nombre, descripcion, orden)
WHERE NOT EXISTS (SELECT 1 FROM ss_checklist_partida WHERE nombre = v.nombre);

COMMIT;

-- ── Verificación ─────────────────────────────────────────────────────────────
SELECT id, nombre, orden, activo FROM ss_checklist_partida ORDER BY orden;
