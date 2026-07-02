-- ============================================================================
-- Módulo Contabilidad — Facturas: separar invoice_number en serie + correlativo.
-- Ejecutar en PRODUCCIÓN. Idempotente.
-- ============================================================================
BEGIN;

-- ── Nuevas columnas ─────────────────────────────────────────────────
ALTER TABLE invoice ADD COLUMN IF NOT EXISTS serie       VARCHAR(10);
ALTER TABLE invoice ADD COLUMN IF NOT EXISTS correlativo VARCHAR(20);

-- ── Backfill desde el invoice_number existente (formato SERIE-CORRELATIVO) ──
UPDATE invoice
SET serie = split_part(invoice_number, '-', 1),
    correlativo = split_part(invoice_number, '-', 2)
WHERE invoice_number IS NOT NULL
  AND (serie IS NULL OR correlativo IS NULL);

-- A partir de ahora serie/correlativo son obligatorios; invoice_number pasa a ser opcional (legacy).
ALTER TABLE invoice ALTER COLUMN serie       SET NOT NULL;
ALTER TABLE invoice ALTER COLUMN correlativo SET NOT NULL;
ALTER TABLE invoice ALTER COLUMN invoice_number DROP NOT NULL;

-- ── Índice único: serie + correlativo por proveedor entre registros vigentes ──
DROP INDEX IF EXISTS ux_invoice_number_per_contributor_active;
CREATE UNIQUE INDEX IF NOT EXISTS ux_invoice_serie_corr_per_contributor_active
    ON invoice (contributor_id, lower(serie), lower(correlativo))
    WHERE state = TRUE;

COMMIT;
