-- ============================================================================
-- ATS Grupal — QR fijo por proyecto: a diferencia del QR de adhesión (que nace
-- por jornada cuando alguien CON cuenta crea un grupo, ver ss_ats_grupo), este
-- token es permanente por proyecto. Se pega/imprime una vez en la obra para
-- que CUALQUIER integrante de una cuadrilla, incluso sin cuenta en la
-- plataforma, pueda escanearlo y entrar a la página pública
-- /ats-grupal/crear/:token a llenar el contenido de un ATS Grupal nuevo.
--
-- Candado: la página pública exige elegir el nombre de una lista de
-- trabajadores YA registrados en ese proyecto y confirmar los últimos dígitos
-- de su DNI (mismo patrón que la adhesión) — nunca datos libres.
--
-- Idempotente (CREATE TABLE IF NOT EXISTS).
-- ============================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS ss_ats_proyecto_qr (
    id          SERIAL PRIMARY KEY,
    proyecto_id INT NOT NULL UNIQUE REFERENCES project(project_id),
    token       UUID NOT NULL UNIQUE,
    created_at  TIMESTAMP NOT NULL DEFAULT now()
);

COMMIT;

-- ============================================================================
-- Verificación (correr después; no modifica nada)
-- ============================================================================
-- SELECT * FROM ss_ats_proyecto_qr;
