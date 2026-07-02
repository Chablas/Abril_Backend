-- ============================================================================
-- Módulo Contabilidad — "Carpeta facturas" pasa a ser un ÚNICO registro (singleton).
-- - Se conserva una sola fila de invoice_folder (la de menor id); las demás se
--   eliminan (HARD DELETE) tras repuntar las facturas a la fila superviviente.
-- - Se elimina la columna `name` (ya no hay nombres: solo hay una carpeta) y su
--   índice único por nombre.
-- - Se fuerza el singleton con un índice único parcial (a lo sumo una fila state=true).
-- Ejecutar en PRODUCCIÓN. Idempotente. No requiere feature/permиsos nuevos
-- (la feature 'accounting.configuration' ya existe).
--
-- IMPORTANTE: tras desplegar, entrar a Contabilidad → Configuración → "Carpeta
-- facturas" y pegar el link de la biblioteca de destino, p. ej.
-- https://abrilinmob.sharepoint.com/sites/bibliotecanm/Facturas/Forms/AllItems.aspx
-- El sistema lo detecta y deja el singleton apuntando a esa carpeta.
-- ============================================================================
BEGIN;

-- 1) Repuntar todas las facturas a la fila superviviente (la de menor id) y borrar las demás.
UPDATE invoice
   SET invoice_folder_id = (SELECT min(invoice_folder_id) FROM invoice_folder)
 WHERE invoice_folder_id IS NOT NULL
   AND (SELECT min(invoice_folder_id) FROM invoice_folder) IS NOT NULL;

DELETE FROM invoice_folder
 WHERE invoice_folder_id <> (SELECT min(invoice_folder_id) FROM invoice_folder);

-- 2) Asegurar que la fila superviviente (si existe) quede vigente y activa.
UPDATE invoice_folder
   SET state = TRUE, active = TRUE
 WHERE invoice_folder_id = (SELECT min(invoice_folder_id) FROM invoice_folder);

-- 3) Quitar la columna name y su índice único por nombre.
DROP INDEX IF EXISTS ux_invoice_folder_name_active;
ALTER TABLE invoice_folder DROP COLUMN IF EXISTS name;

-- 4) Forzar singleton: a lo sumo una fila vigente (state = true).
CREATE UNIQUE INDEX IF NOT EXISTS ux_invoice_folder_singleton
    ON invoice_folder ((TRUE))
    WHERE state = TRUE;

COMMIT;
