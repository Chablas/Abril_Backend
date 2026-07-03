-- ============================================================================
-- Módulo Contabilidad — Facturas: añadir moneda (reusa la tabla currency).
-- Ejecutar en PRODUCCIÓN. Idempotente.
-- ============================================================================
BEGIN;

ALTER TABLE invoice ADD COLUMN IF NOT EXISTS currency_id INT;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_invoice_currency') THEN
        ALTER TABLE invoice
            ADD CONSTRAINT fk_invoice_currency
            FOREIGN KEY (currency_id) REFERENCES currency (currency_id);
    END IF;
END $$;

COMMIT;
