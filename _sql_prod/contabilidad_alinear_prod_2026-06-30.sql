-- ============================================================================
-- Módulo Contabilidad — ALINEAR PRODUCCIÓN con DESARROLLO (2026-06-30)
--
-- Estado verificado de PRODUCCIÓN antes de este script:
--   • Ya existen: invoice, invoice_payment_form, invoice_document_type,
--     invoice_folder, el module 'Contabilidad', las features
--     (accounting.invoices/configuration/dashboard), el rol
--     'USUARIO DE CONTABILIDAD' y los role_feature. NO se tocan.
--   • Dependencias externas presentes: contributor.es_abril y la tabla currency.
--
-- Lo ÚNICO que le falta a producción frente a desarrollo (2 migraciones):
--   1) Firma del Gerente General: tabla manager_signature + columna
--      invoice.signed_document_url.        (rompe Facturas y Configuración→Firma GG)
--   2) "Carpeta facturas" pasó a singleton: quitar invoice_folder.name y su índice
--      por nombre, y forzar el índice singleton.
--
-- Idempotente. Ejecutar en PRODUCCIÓN dentro de la VPS (túnel SSH).
-- ============================================================================
BEGIN;

-- ── (1) Firma del Gerente General (singleton) ───────────────────────────────
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

-- Documento firmado de la factura (aparte del original). ESTA es la columna que
-- falta y que hace fallar la página de Facturas (GetPaged la selecciona).
ALTER TABLE invoice ADD COLUMN IF NOT EXISTS signed_document_url VARCHAR(2000);

-- ── (2) "Carpeta facturas" como singleton ───────────────────────────────────
-- Repuntar todas las facturas a la fila superviviente (menor id) y borrar las demás.
-- (En producción invoice_folder está vacío hoy, así que esto es un no-op seguro.)
UPDATE invoice
   SET invoice_folder_id = (SELECT min(invoice_folder_id) FROM invoice_folder)
 WHERE invoice_folder_id IS NOT NULL
   AND (SELECT min(invoice_folder_id) FROM invoice_folder) IS NOT NULL;

DELETE FROM invoice_folder
 WHERE invoice_folder_id <> (SELECT min(invoice_folder_id) FROM invoice_folder);

UPDATE invoice_folder
   SET state = TRUE, active = TRUE
 WHERE invoice_folder_id = (SELECT min(invoice_folder_id) FROM invoice_folder);

-- Quitar la columna name y su índice único por nombre.
DROP INDEX IF EXISTS ux_invoice_folder_name_active;
ALTER TABLE invoice_folder DROP COLUMN IF EXISTS name;

-- Forzar singleton: a lo sumo una fila vigente (state = true).
CREATE UNIQUE INDEX IF NOT EXISTS ux_invoice_folder_singleton
    ON invoice_folder ((TRUE))
    WHERE state = TRUE;

COMMIT;

-- ============================================================================
-- VERIFICACIÓN (opcional, correr después; deben devolver lo indicado):
--   SELECT to_regclass('public.manager_signature');                 -- manager_signature
--   SELECT 1 FROM information_schema.columns
--     WHERE table_name='invoice' AND column_name='signed_document_url';   -- 1 fila
--   SELECT 1 FROM information_schema.columns
--     WHERE table_name='invoice_folder' AND column_name='name';           -- 0 filas
-- ============================================================================
