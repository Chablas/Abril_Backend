-- ============================================================================
-- Configuración → Proyectos — botar project.email_residente
-- SOLO DESPUÉS DE DESPLEGAR EL BACKEND
-- Fecha: 2026-09-29
--
-- project.email_residente era el correo del residente en texto suelto. Desde el 2026-08-07 el
-- residente es una FK a la ficha (project.residente_workers_id) y nadie mantiene ese texto. Su
-- último lector, el aviso de Interconsultas, pasó a leer la ficha el 2026-09-29 (Paso 5a de
-- PLAN-RESIDENTES.md), y con este deploy la entidad Project deja de mapear la columna. Es el mismo
-- camino que siguió email_coord_admin al reemplazarse por workers_coord_admin_id.
--
-- Correrlo ANTES del deploy tumba toda consulta que cargue la entidad Project con el backend viejo
-- (42703): Configuración → Proyectos y buena parte del sistema.
--
-- Requiere project.residente_workers_id: aborta si falta. Idempotente.
-- ============================================================================

BEGIN;

SET LOCAL lock_timeout = '15s';

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema = 'public' AND table_name = 'project'
                     AND column_name = 'residente_workers_id') THEN
        RAISE EXCEPTION 'project.residente_workers_id no existe: no es el esquema esperado. Abortado.';
    END IF;
END $$;

ALTER TABLE project DROP COLUMN IF EXISTS email_residente;

COMMIT;

-- ── Verificación ──────────────────────────────────────────────────────────────
-- Debe devolver 0 filas:
-- SELECT column_name FROM information_schema.columns
-- WHERE table_schema = 'public' AND table_name = 'project' AND column_name = 'email_residente';
