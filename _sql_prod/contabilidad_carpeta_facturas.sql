-- ============================================================================
-- Módulo Contabilidad — Configuración → Carpeta de facturas (OneDrive)
-- + columnas nuevas en invoice (carpeta destino y razón social de Abril).
-- Ejecutar en PRODUCCIÓN. Idempotente (se puede correr más de una vez sin duplicar).
-- Requiere que ya exista la feature de facturas (contabilidad_facturas.sql).
-- ============================================================================
BEGIN;

-- ── Tabla: carpeta de facturas (OneDrive/SharePoint) ────────────────
CREATE TABLE IF NOT EXISTS invoice_folder (
    invoice_folder_id SERIAL PRIMARY KEY,
    name              VARCHAR(150) NOT NULL,
    link_url          VARCHAR(2000) NOT NULL,
    drive_id          VARCHAR(500) NOT NULL,
    folder_id         VARCHAR(500) NOT NULL,
    folder_name       VARCHAR(500),
    web_url           VARCHAR(2000),
    created_date_time TIMESTAMPTZ NOT NULL DEFAULT (now() AT TIME ZONE 'UTC'),
    created_user_id   INT NOT NULL,
    updated_date_time TIMESTAMPTZ,
    updated_user_id   INT,
    active  BOOLEAN NOT NULL DEFAULT TRUE,
    state   BOOLEAN NOT NULL DEFAULT TRUE
);

-- Un solo registro vigente (state=true) por nombre
CREATE UNIQUE INDEX IF NOT EXISTS ux_invoice_folder_name_active
    ON invoice_folder (lower(name))
    WHERE state = TRUE;

-- ── Columnas nuevas en invoice ──────────────────────────────────────
ALTER TABLE invoice ADD COLUMN IF NOT EXISTS invoice_folder_id    INT;
ALTER TABLE invoice ADD COLUMN IF NOT EXISTS abril_contributor_id INT;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_invoice_invoice_folder') THEN
        ALTER TABLE invoice
            ADD CONSTRAINT fk_invoice_invoice_folder
            FOREIGN KEY (invoice_folder_id) REFERENCES invoice_folder (invoice_folder_id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_invoice_abril_contributor') THEN
        ALTER TABLE invoice
            ADD CONSTRAINT fk_invoice_abril_contributor
            FOREIGN KEY (abril_contributor_id) REFERENCES contributor (contributor_id);
    END IF;
END $$;

-- ── Feature de configuración + permisos ─────────────────────────────
INSERT INTO feature (feature_key, module_id)
SELECT 'accounting.configuration', m.module_id
FROM module m
WHERE m.module_name = 'Contabilidad'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'accounting.configuration');

INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM role r
CROSS JOIN feature f
WHERE f.feature_key = 'accounting.configuration'
  AND r.role_description IN ('USUARIO DE CONTABILIDAD', 'ADMINISTRADOR DEL SISTEMA')
  AND NOT EXISTS (
      SELECT 1 FROM role_feature rf WHERE rf.role_id = r.role_id AND rf.feature_id = f.feature_id
  );

COMMIT;
