-- ============================================================================
-- Módulo Contabilidad — Facturas: importación desde Excel de órdenes de pago.
-- Campos extra del Excel + nombres como texto (fuente de verdad) + catálogo de
-- tipo de documento. Ejecutar en PRODUCCIÓN. Idempotente.
-- ============================================================================
BEGIN;

-- ── Catálogo: tipo de documento (FACTURAS, RECIBOS POR HONORARIOS, etc.) ──
CREATE TABLE IF NOT EXISTS invoice_document_type (
    invoice_document_type_id SERIAL PRIMARY KEY,
    description VARCHAR(150) NOT NULL,
    created_date_time TIMESTAMPTZ NOT NULL DEFAULT (now() AT TIME ZONE 'UTC'),
    created_user_id INT,
    updated_date_time TIMESTAMPTZ,
    updated_user_id INT,
    active  BOOLEAN NOT NULL DEFAULT TRUE,
    state   BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_invoice_document_type_desc_active
    ON invoice_document_type (lower(description))
    WHERE state = TRUE;

-- ── Columnas nuevas en invoice ──────────────────────────────────────
-- El proveedor del Excel no trae RUC: se guarda el nombre como texto y el
-- enlace a contributor queda opcional (best-effort por coincidencia de nombre).
ALTER TABLE invoice ALTER COLUMN contributor_id DROP NOT NULL;
-- El Excel no trae forma de pago.
ALTER TABLE invoice ALTER COLUMN invoice_payment_form_id DROP NOT NULL;

ALTER TABLE invoice ADD COLUMN IF NOT EXISTS proveedor_name           VARCHAR(300);
ALTER TABLE invoice ADD COLUMN IF NOT EXISTS abril_name               VARCHAR(300);
ALTER TABLE invoice ADD COLUMN IF NOT EXISTS payment_order_number     VARCHAR(50);
ALTER TABLE invoice ADD COLUMN IF NOT EXISTS invoice_document_type_id INT;
ALTER TABLE invoice ADD COLUMN IF NOT EXISTS authorized_amount        NUMERIC(14,2);
ALTER TABLE invoice ADD COLUMN IF NOT EXISTS observation              VARCHAR(2000);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_invoice_document_type') THEN
        ALTER TABLE invoice
            ADD CONSTRAINT fk_invoice_document_type
            FOREIGN KEY (invoice_document_type_id) REFERENCES invoice_document_type (invoice_document_type_id);
    END IF;
END $$;

-- El correlativo puede repetirse entre empresas/series en una importación masiva:
-- se quita el índice único estricto para no bloquear la carga del Excel.
DROP INDEX IF EXISTS ux_invoice_serie_corr_per_contributor_active;

COMMIT;
