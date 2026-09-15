-- ============================================================================
-- Módulo SSOMA — Control de Activos Rotativos entre Proyectos
-- (tambores retráctiles, frenos de cuerda, etc.). Solo visibilidad + contacto,
-- sin flujo de aprobación: cualquiera con acceso ve quién tiene cada activo y
-- a quién contactar para solicitarlo.
-- Ejecutar en PRODUCCIÓN. Idempotente.
-- ============================================================================
BEGIN;

-- ── Feature + permisos ───────────────────────────────────────────────────────
INSERT INTO feature (feature_key, module_id)
SELECT 'ssoma.gestion.activos-rotativos', m.module_id
FROM module m
WHERE m.module_name = 'SSOMA'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'ssoma.gestion.activos-rotativos');

INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM role r
CROSS JOIN feature f
WHERE f.feature_key = 'ssoma.gestion.activos-rotativos'
  AND r.role_description IN ('USUARIO DE ABRIL', 'ADMINISTRADOR DEL SISTEMA')
  AND NOT EXISTS (
      SELECT 1 FROM role_feature rf WHERE rf.role_id = r.role_id AND rf.feature_id = f.feature_id
  );

-- ── Categorías de activo rotativo ────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS ss_activo_rotativo_categoria (
    id         SERIAL PRIMARY KEY,
    nombre     VARCHAR(150) NOT NULL,
    orden      INT NOT NULL DEFAULT 0,
    activo     BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NULL
);

-- ── Activos ───────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS ss_activo_rotativo (
    id                   SERIAL PRIMARY KEY,
    nombre               VARCHAR(200) NOT NULL,
    categoria_id         INT NOT NULL REFERENCES ss_activo_rotativo_categoria(id),
    codigo               VARCHAR(100) NULL,
    estado               VARCHAR(30) NOT NULL DEFAULT 'disponible',
    proyecto_actual_id   INT NULL REFERENCES project(project_id),
    responsable_nombre   VARCHAR(200) NULL,
    responsable_telefono VARCHAR(30) NULL,
    observaciones        TEXT NULL,
    activo               BOOLEAN NOT NULL DEFAULT TRUE,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at           TIMESTAMPTZ NULL
);

CREATE INDEX IF NOT EXISTS ix_ss_activo_rotativo_categoria_id ON ss_activo_rotativo(categoria_id);
CREATE INDEX IF NOT EXISTS ix_ss_activo_rotativo_proyecto_actual_id ON ss_activo_rotativo(proyecto_actual_id);

-- ── Historial de traspasos ──────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS ss_activo_rotativo_movimiento (
    id                  SERIAL PRIMARY KEY,
    activo_id           INT NOT NULL REFERENCES ss_activo_rotativo(id) ON DELETE CASCADE,
    proyecto_origen_id  INT NULL REFERENCES project(project_id),
    proyecto_destino_id INT NULL REFERENCES project(project_id),
    fecha_movimiento    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    movido_por_id       INT NULL,
    observacion         TEXT NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_ss_activo_rotativo_movimiento_activo_id ON ss_activo_rotativo_movimiento(activo_id);

-- ── Seed de categorías iniciales (ejemplo, ajustar/ampliar según lo real) ────
INSERT INTO ss_activo_rotativo_categoria (nombre, orden, activo, created_at, updated_at)
SELECT v.nombre, v.orden, true, NOW(), NOW()
FROM (VALUES
  ('Tambor Retráctil',  1),
  ('Freno de Cuerda',   2),
  ('Línea de Vida',     3),
  ('Arnés/Eslinga',     4),
  ('Equipo de Izaje',   5)
) AS v(nombre, orden)
WHERE NOT EXISTS (SELECT 1 FROM ss_activo_rotativo_categoria WHERE nombre = v.nombre);

COMMIT;

-- ── Verificación ─────────────────────────────────────────────────────────────
SELECT id, nombre, orden, activo FROM ss_activo_rotativo_categoria ORDER BY orden;
