-- ============================================================================
-- Configuración → Proyectos — Tipo de proyecto y ciclo de vida
-- PASO 2 de 2 · SOLO DESPUÉS DE DESPLEGAR EL BACKEND Y EL FRONTEND
-- Fecha: 2026-09-28
--
-- Bota las tres columnas que reemplazó project.project_ciclo_vida_id:
--   · project.estado    («Estado del proyecto», texto libre)
--   · project.activo    («Ciclo de vida», texto)
--   · project.operativo (boolean del dashboard del PASO)
-- El código nuevo ya no las lee ni las escribe. Correrlo ANTES del deploy tumba Configuración →
-- Proyectos y toda consulta que cargue la entidad Project con el backend viejo (42703).
--
-- NO toca project.active (columna de sistema) ni project.state.
--
-- Requiere 20260928_ProyectosTipoYCicloVida.sql: aborta si falta. Idempotente.
-- ============================================================================

BEGIN;

SET LOCAL lock_timeout = '15s';

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema = 'public' AND table_name = 'project'
                     AND column_name = 'project_ciclo_vida_id' AND is_nullable = 'NO') THEN
        RAISE EXCEPTION 'Falta 20260928_ProyectosTipoYCicloVida.sql: project.project_ciclo_vida_id no existe o admite NULL. Abortado.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema = 'public' AND table_name = 'project'
                     AND column_name = 'project_tipo_id' AND is_nullable = 'NO') THEN
        RAISE EXCEPTION 'Falta 20260928_ProyectosTipoYCicloVida.sql: project.project_tipo_id no existe o admite NULL. Abortado.';
    END IF;
END $$;

ALTER TABLE project DROP COLUMN IF EXISTS estado;
ALTER TABLE project DROP COLUMN IF EXISTS activo;
ALTER TABLE project DROP COLUMN IF EXISTS operativo;

COMMIT;

-- ── Verificación ────────────────────────────────────────────────────────────
-- Debe devolver 0 filas:
-- SELECT column_name FROM information_schema.columns
-- WHERE table_schema = 'public' AND table_name = 'project'
--   AND column_name IN ('estado', 'activo', 'operativo');
