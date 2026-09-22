-- ============================================================================
-- Adjudicaciones — paso 4 "Por enviar al SC": permitir omitir el envío.
--
-- Oficina Técnica puede saltar del paso 4 al 5 sin que el sistema mande el correo
-- al subcontratista, para los casos en que el contrato completo ya se envió por
-- correo fuera del sistema.
--
-- La columna marca ese salto para que después no se confunda con un envío hecho
-- desde la aplicación. Las adjudicaciones existentes quedan en false, que es lo
-- correcto: todas las que pasaron el paso 4 hasta hoy lo hicieron enviando el correo.
--
-- Ejecutar en PRODUCCIÓN ANTES del deploy (el backend selecciona esta columna en el
-- listado de adjudicaciones; sin ella la pantalla responde 42703). Idempotente.
-- ============================================================================
BEGIN;

ALTER TABLE project_sub_contractor
    ADD COLUMN IF NOT EXISTS sc_notification_skipped BOOLEAN NOT NULL DEFAULT FALSE;

COMMIT;
