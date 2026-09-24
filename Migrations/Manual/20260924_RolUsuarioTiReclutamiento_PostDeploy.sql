-- ============================================================================
-- Gestión GTH · Reclutamiento — Rol USUARIO DE TI (solo lectura)
-- PASO 2 de 2 · SOLO DESPUÉS DE DESPLEGAR EL BACKEND Y EL FRONTEND
-- Fecha: 2026-09-24
--
-- Le da al rol USUARIO DE TI la feature de VER Reclutamiento. Con el código nuevo eso es
-- solo lectura: gestionar exige gestion-gth.reclutamiento.gestionar, que el rol no tiene.
-- Con el código viejo esa misma feature alcanzaba para gestionar todo; por eso va después
-- del deploy.
--
-- Requiere el paso 1 (20260924_RolUsuarioTiReclutamiento.sql): aborta si falta.
-- Idempotente. Los dos usuarios lo ven al recargar la página: el refresh del token trae
-- las features nuevas.
-- ============================================================================

BEGIN;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'gestion-gth.reclutamiento.gestionar') THEN
        RAISE EXCEPTION 'Falta el paso 1: no existe la feature gestion-gth.reclutamiento.gestionar. Abortado.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM role WHERE role_description = 'USUARIO DE TI' AND state) THEN
        RAISE EXCEPTION 'Falta el paso 1: no existe el rol USUARIO DE TI. Abortado.';
    END IF;
END $$;

INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM role r
CROSS JOIN feature f
WHERE r.role_description = 'USUARIO DE TI'
  AND r.state
  AND f.feature_key = 'gestion-gth.reclutamiento'
ON CONFLICT (role_id, feature_id) DO NOTHING;

COMMIT;

-- ── Verificación ────────────────────────────────────────────────────────────
-- USUARIO DE TI tiene la de ver y NO la de gestionar:
-- SELECT f.feature_key
-- FROM role r
-- JOIN role_feature rf ON rf.role_id = r.role_id
-- JOIN feature f ON f.feature_id = rf.feature_id
-- WHERE r.role_description = 'USUARIO DE TI'
-- ORDER BY 1;
-- Debe devolver una sola fila: gestion-gth.reclutamiento
