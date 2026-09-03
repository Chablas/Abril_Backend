-- Migración manual (pgAdmin) — módulo nuevo e independiente de Almacén/Logística.
-- Ejecutar directamente contra la BD PostgreSQL. No usar dotnet ef.
--
-- Sin FK hacia tablas de Costos/Adjudicaciones (contratistas): almacen_ordenes_compra
-- guarda contratista_id como referencia libre, de solo lectura, sin constraint — así
-- este módulo no queda acoplado al esquema de Costos.

CREATE TABLE IF NOT EXISTS almacen_materiales (
    id             SERIAL PRIMARY KEY,
    codigo         TEXT NOT NULL,
    nombre         TEXT NOT NULL,
    unidad_medida  TEXT NOT NULL,
    activo         BOOLEAN NOT NULL DEFAULT TRUE,
    created_at     TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    CONSTRAINT uq_almacen_materiales_codigo UNIQUE (codigo)
);

CREATE TABLE IF NOT EXISTS almacen_movimientos (
    id            SERIAL PRIMARY KEY,
    proyecto_id   INTEGER NOT NULL REFERENCES project (project_id) ON DELETE CASCADE,
    material_id   INTEGER NOT NULL REFERENCES almacen_materiales (id) ON DELETE RESTRICT,
    fecha         TIMESTAMP NOT NULL,
    tipo          TEXT NOT NULL,   -- Ingreso | Salida (lista fija en código)
    cantidad      NUMERIC(14, 2) NOT NULL,
    origen        TEXT,
    comentario    TEXT,
    creado_por    TEXT,
    created_at    TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'utc')
);

CREATE INDEX IF NOT EXISTS ix_almacen_movimientos_proyecto_material ON almacen_movimientos (proyecto_id, material_id);
CREATE INDEX IF NOT EXISTS ix_almacen_movimientos_fecha             ON almacen_movimientos (fecha DESC);

CREATE TABLE IF NOT EXISTS almacen_ordenes_compra (
    id               SERIAL PRIMARY KEY,
    proyecto_id      INTEGER NOT NULL REFERENCES project (project_id) ON DELETE CASCADE,
    numero           TEXT NOT NULL,
    tipo             TEXT NOT NULL,   -- Orden de Compra | Contrato
    proveedor        TEXT NOT NULL,
    contratista_id   INTEGER,          -- referencia libre a Costos.Contractor, sin FK a propósito
    monto            NUMERIC(14, 2) NOT NULL DEFAULT 0,
    moneda           TEXT NOT NULL DEFAULT 'PEN',
    fecha            TIMESTAMP NOT NULL,
    archivo_url      TEXT NOT NULL,
    archivo_nombre   TEXT NOT NULL,
    subido_por       TEXT,
    created_at       TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'utc')
);

CREATE INDEX IF NOT EXISTS ix_almacen_ordenes_compra_proyecto_id ON almacen_ordenes_compra (proyecto_id);

-- Módulo nuevo "Almacén" en el catálogo de navegación/permisos.
INSERT INTO module (module_name)
SELECT 'Almacén'
WHERE NOT EXISTS (SELECT 1 FROM module WHERE module_name = 'Almacén');

INSERT INTO feature (feature_key, module_id)
SELECT 'almacen.materiales', m.module_id
FROM module m
WHERE m.module_name = 'Almacén'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'almacen.materiales');

INSERT INTO feature (feature_key, module_id)
SELECT 'almacen.ordenes-compra', m.module_id
FROM module m
WHERE m.module_name = 'Almacén'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'almacen.ordenes-compra');

-- Acceso inicial: rol 1 (ADMINISTRADOR DEL SISTEMA) para poder probar/registrar.
-- Definir después los roles reales (Logística/Almacén) desde Configuración > Roles y Permisos.
INSERT INTO role_feature (role_id, feature_id)
SELECT 1, f.feature_id
FROM feature f
WHERE f.feature_key IN ('almacen.materiales', 'almacen.ordenes-compra')
  AND NOT EXISTS (SELECT 1 FROM role_feature rf WHERE rf.role_id = 1 AND rf.feature_id = f.feature_id);
