-- ============================================================================
-- Módulo Contabilidad — Feature Facturas
-- Ejecutar en PRODUCCIÓN. Idempotente (se puede correr más de una vez sin duplicar).
-- ============================================================================
BEGIN;

-- ── Catálogo: forma de pago de facturas ─────────────────────────────
CREATE TABLE IF NOT EXISTS invoice_payment_form (
    invoice_payment_form_id           SERIAL PRIMARY KEY,
    invoice_payment_form_description  VARCHAR(100) NOT NULL,
    created_date_time   TIMESTAMPTZ NOT NULL DEFAULT (now() AT TIME ZONE 'UTC'),
    created_user_id     INT,
    updated_date_time   TIMESTAMPTZ,
    updated_user_id     INT,
    active  BOOLEAN NOT NULL DEFAULT TRUE,
    state   BOOLEAN NOT NULL DEFAULT TRUE
);

-- Solo un registro vigente (state=true) por descripción
CREATE UNIQUE INDEX IF NOT EXISTS ux_invoice_payment_form_desc_active
    ON invoice_payment_form (lower(invoice_payment_form_description))
    WHERE state = TRUE;

INSERT INTO invoice_payment_form (invoice_payment_form_description, created_user_id)
SELECT v.descr, 1
FROM (VALUES ('A crédito'), ('Al contado')) AS v(descr)
WHERE NOT EXISTS (
    SELECT 1 FROM invoice_payment_form ipf
    WHERE lower(ipf.invoice_payment_form_description) = lower(v.descr) AND ipf.state = TRUE
);

-- ── Tabla principal: factura ────────────────────────────────────────
CREATE TABLE IF NOT EXISTS invoice (
    invoice_id        SERIAL PRIMARY KEY,
    issue_date        DATE NOT NULL,
    invoice_number    VARCHAR(50) NOT NULL,
    contributor_id    INT NOT NULL REFERENCES contributor (contributor_id),
    description       VARCHAR(1000) NOT NULL,
    invoice_payment_form_id INT NOT NULL REFERENCES invoice_payment_form (invoice_payment_form_id),
    total             NUMERIC(14,2) NOT NULL,
    document_url      VARCHAR(1000),
    created_date_time TIMESTAMPTZ NOT NULL DEFAULT (now() AT TIME ZONE 'UTC'),
    created_user_id   INT,
    updated_date_time TIMESTAMPTZ,
    updated_user_id   INT,
    active  BOOLEAN NOT NULL DEFAULT TRUE,
    state   BOOLEAN NOT NULL DEFAULT TRUE
);

-- Un número de factura único por proveedor entre registros vigentes
CREATE UNIQUE INDEX IF NOT EXISTS ux_invoice_number_per_contributor_active
    ON invoice (contributor_id, lower(invoice_number))
    WHERE state = TRUE;

-- ── Rol, módulo, feature y permisos ─────────────────────────────────
INSERT INTO role (role_description, created_date_time, created_user_id, active, state)
SELECT 'USUARIO DE CONTABILIDAD', now() AT TIME ZONE 'UTC', 1, TRUE, TRUE
WHERE NOT EXISTS (SELECT 1 FROM role WHERE role_description = 'USUARIO DE CONTABILIDAD');

INSERT INTO module (module_name)
SELECT 'Contabilidad'
WHERE NOT EXISTS (SELECT 1 FROM module WHERE module_name = 'Contabilidad');

INSERT INTO feature (feature_key, module_id)
SELECT 'accounting.invoices', m.module_id
FROM module m
WHERE m.module_name = 'Contabilidad'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'accounting.invoices');

-- Permisos: nuevo rol + administrador del sistema
INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM role r
CROSS JOIN feature f
WHERE f.feature_key = 'accounting.invoices'
  AND r.role_description IN ('USUARIO DE CONTABILIDAD', 'ADMINISTRADOR DEL SISTEMA')
  AND NOT EXISTS (
      SELECT 1 FROM role_feature rf WHERE rf.role_id = r.role_id AND rf.feature_id = f.feature_id
  );

COMMIT;

-- Recordatorio: para que un usuario vea el módulo, asignarle el rol en user_role:
--   INSERT INTO user_role (user_id, role_id)
--   SELECT <USER_ID>, role_id FROM role WHERE role_description = 'USUARIO DE CONTABILIDAD';
