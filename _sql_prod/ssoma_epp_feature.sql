-- ============================================================================
-- Módulo SSOMA — Catálogo Autorizado de EPP (Equipo de Protección Personal)
-- Visible también para Logística (mismo feature/permiso, asignar el rol de
-- Logística a este feature desde /security/roles si el rol ya existe, o
-- crearlo ahí primero). Cada ítem tiene nombre técnico + nombre comercial,
-- imagen, y modelos/marcas autorizadas. Toda alta/edición/baja queda
-- registrada en ss_epp_auditoria (quién y cuándo).
-- Ejecutar en PRODUCCIÓN. Idempotente.
-- ============================================================================
BEGIN;

-- ── Feature + permisos ───────────────────────────────────────────────────────
INSERT INTO feature (feature_key, module_id)
SELECT 'ssoma.gestion.epp', m.module_id
FROM module m
WHERE m.module_name = 'SSOMA'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'ssoma.gestion.epp');

INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM role r
CROSS JOIN feature f
WHERE f.feature_key = 'ssoma.gestion.epp'
  AND r.role_description IN ('USUARIO DE ABRIL', 'ADMINISTRADOR DEL SISTEMA')
  AND NOT EXISTS (
      SELECT 1 FROM role_feature rf WHERE rf.role_id = r.role_id AND rf.feature_id = f.feature_id
  );

-- ── Categorías de EPP ─────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS ss_epp_categoria (
    id         SERIAL PRIMARY KEY,
    nombre     VARCHAR(150) NOT NULL,
    orden      INT NOT NULL DEFAULT 0,
    activo     BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NULL
);

-- ── Ítems de EPP (catálogo autorizado) ────────────────────────────────────────
CREATE TABLE IF NOT EXISTS ss_epp_item (
    id                SERIAL PRIMARY KEY,
    nombre_tecnico    VARCHAR(200) NOT NULL,
    nombre_comercial  VARCHAR(200) NOT NULL,
    categoria_id      INT NOT NULL REFERENCES ss_epp_categoria(id),
    descripcion       TEXT NULL,
    imagen_url        VARCHAR(500) NULL,
    activo            BOOLEAN NOT NULL DEFAULT TRUE,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by_id     INT NULL,
    updated_at        TIMESTAMPTZ NULL,
    updated_by_id     INT NULL
);

CREATE INDEX IF NOT EXISTS ix_ss_epp_item_categoria_id ON ss_epp_item(categoria_id);

-- ── Modelos / marcas autorizadas por ítem ─────────────────────────────────────
CREATE TABLE IF NOT EXISTS ss_epp_modelo (
    id                 SERIAL PRIMARY KEY,
    epp_item_id        INT NOT NULL REFERENCES ss_epp_item(id) ON DELETE CASCADE,
    marca              VARCHAR(150) NOT NULL,
    modelo             VARCHAR(150) NOT NULL,
    codigo_referencia  VARCHAR(100) NULL,
    imagen_url         VARCHAR(500) NULL,
    activo             BOOLEAN NOT NULL DEFAULT TRUE,
    created_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by_id      INT NULL,
    updated_at         TIMESTAMPTZ NULL,
    updated_by_id      INT NULL
);

CREATE INDEX IF NOT EXISTS ix_ss_epp_modelo_epp_item_id ON ss_epp_modelo(epp_item_id);

-- ── Auditoría (quién/cuándo creó, editó o dio de baja un ítem o modelo) ───────
CREATE TABLE IF NOT EXISTS ss_epp_auditoria (
    id            SERIAL PRIMARY KEY,
    entidad_tipo  VARCHAR(20) NOT NULL,   -- 'Item' | 'Modelo'
    entidad_id    INT NOT NULL,
    accion        VARCHAR(20) NOT NULL,   -- 'Creado' | 'Editado' | 'Activado' | 'Desactivado'
    detalle       VARCHAR(500) NULL,
    usuario_id    INT NULL,
    fecha         TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_ss_epp_auditoria_entidad ON ss_epp_auditoria(entidad_tipo, entidad_id);

-- ── Seed de categorías iniciales (ejemplo, ajustar/ampliar según lo real) ────
INSERT INTO ss_epp_categoria (nombre, orden, activo, created_at)
SELECT v.nombre, v.orden, true, NOW()
FROM (VALUES
  ('Cabeza',            1),
  ('Ojos y Rostro',     2),
  ('Auditiva',          3),
  ('Manos',             4),
  ('Pies',              5),
  ('Cuerpo',            6),
  ('Altura',            7),
  ('Respiratoria',      8)
) AS v(nombre, orden)
WHERE NOT EXISTS (SELECT 1 FROM ss_epp_categoria WHERE nombre = v.nombre);

COMMIT;

-- ── Verificación ─────────────────────────────────────────────────────────────
SELECT id, nombre, orden, activo FROM ss_epp_categoria ORDER BY orden;
