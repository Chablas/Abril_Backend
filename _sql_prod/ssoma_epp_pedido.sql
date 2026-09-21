-- ============================================================================
-- EPP — Pedidos generados (código correlativo, proyecto, fecha, quién lo generó).
-- Cada línea es un snapshot del catálogo al momento del pedido.
-- Ejecutar DESPUÉS de ssoma_epp_ficha_tecnica.sql. Idempotente.
-- ============================================================================
BEGIN;

CREATE TABLE IF NOT EXISTS ss_epp_pedido (
    id               SERIAL PRIMARY KEY,
    codigo           VARCHAR(40) NOT NULL,
    project_id       INT NOT NULL REFERENCES project(project_id),
    generado_por_id  INT NULL,
    fecha            TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    estado           VARCHAR(30) NOT NULL DEFAULT 'Generado',
    observaciones    TEXT NULL,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_ss_epp_pedido_codigo ON ss_epp_pedido(codigo);
CREATE INDEX IF NOT EXISTS ix_ss_epp_pedido_project_id ON ss_epp_pedido(project_id);

CREATE TABLE IF NOT EXISTS ss_epp_pedido_linea (
    id                SERIAL PRIMARY KEY,
    pedido_id         INT NOT NULL REFERENCES ss_epp_pedido(id) ON DELETE CASCADE,
    epp_item_id       INT NULL REFERENCES ss_epp_item(id),
    epp_modelo_id     INT NULL REFERENCES ss_epp_modelo(id),
    nombre_tecnico    VARCHAR(200) NOT NULL,
    nombre_comercial  VARCHAR(200) NOT NULL,
    marca             VARCHAR(150) NULL,
    modelo            VARCHAR(150) NULL,
    talla             VARCHAR(30) NULL,
    cantidad          INT NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_ss_epp_pedido_linea_pedido_id ON ss_epp_pedido_linea(pedido_id);

COMMIT;
