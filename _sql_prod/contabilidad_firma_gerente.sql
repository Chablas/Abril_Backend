-- ============================================================================
-- Módulo Contabilidad — Firma del Gerente General + documento firmado de facturas.
-- - Tabla singleton manager_signature (imagen PNG de la firma).
-- - Columna invoice.signed_document_url para el PDF firmado (no toca el original).
-- Ejecutar en PRODUCCIÓN. Idempotente. No requiere feature/permisos nuevos
-- (la sección vive dentro de 'accounting.configuration' y firmar dentro de 'accounting.invoices').
-- ============================================================================
BEGIN;

-- ── Firma del Gerente General (singleton) ───────────────────────────
CREATE TABLE IF NOT EXISTS manager_signature (
    manager_signature_id SERIAL PRIMARY KEY,
    image_bytes  BYTEA NOT NULL,
    mime         VARCHAR(100) NOT NULL DEFAULT 'image/png',
    created_date_time TIMESTAMPTZ NOT NULL DEFAULT (now() AT TIME ZONE 'UTC'),
    created_user_id   INT NOT NULL,
    updated_date_time TIMESTAMPTZ,
    updated_user_id   INT,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    state  BOOLEAN NOT NULL DEFAULT TRUE
);

-- A lo sumo una fila vigente (state = true).
CREATE UNIQUE INDEX IF NOT EXISTS ux_manager_signature_singleton
    ON manager_signature ((TRUE)) WHERE state = TRUE;

-- ── Documento firmado de la factura (aparte del original) ───────────
ALTER TABLE invoice ADD COLUMN IF NOT EXISTS signed_document_url VARCHAR(2000);

COMMIT;
