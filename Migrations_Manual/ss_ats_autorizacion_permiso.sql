-- ============================================================================
-- Autorización física (permiso de trabajo firmado) — gate para poder hacer ATS digitales
-- ============================================================================
-- El trabajador firma en físico su permiso de trabajo, el Coordinador SSOMA escanea y sube
-- la evidencia acá. Crear/editar un ATS (ss_ats) queda BLOQUEADO hasta que exista una fila
-- para ese worker_id — ver AtsService.ExigirAutorizacionPermiso (Crear/Editar).
-- Mismo patrón que ac_tareo_autorizacion (SSO-FO-150 de Arquitectura Comercial), pero es un
-- concepto propio de SSOMA/ATS — tabla independiente, no se reutiliza esa.
-- Idempotente: se puede correr más de una vez sin duplicar.
-- ============================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS ss_ats_autorizacion_permiso (
    id                  SERIAL PRIMARY KEY,
    worker_id           INTEGER NOT NULL UNIQUE REFERENCES workers (id),
    archivo_url         TEXT NOT NULL,
    subido_por_user_id  INTEGER REFERENCES app_user (user_id),
    subido_en           TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMIT;
