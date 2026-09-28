-- ============================================================================
-- ATS Digital — featureKey usada por AtsController ([RequireFeature]) y por
-- ats.routes.ts (frontend).
--
-- A diferencia de la mayoría de features SSOMA, esta se concede a TODOS los
-- roles existentes: el ATS lo llena cada miembro del staff de Abril para sí
-- mismo (no es una herramienta exclusiva del equipo SSOMA), así que cualquier
-- rol necesita poder acceder a su propio formulario.
--
-- Idempotente (se puede re-correr sin duplicar filas).
-- ============================================================================
BEGIN;

INSERT INTO feature (feature_key, module_id)
SELECT 'ssoma.gestion.ats', m.module_id
FROM module m
WHERE m.module_name = 'SSOMA'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'ssoma.gestion.ats');

INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM role r
CROSS JOIN feature f
WHERE f.feature_key = 'ssoma.gestion.ats'
  AND NOT EXISTS (
    SELECT 1 FROM role_feature rf WHERE rf.role_id = r.role_id AND rf.feature_id = f.feature_id
  );

COMMIT;

-- ============================================================================
-- Verificación (correr después; no modifica nada)
-- ============================================================================
-- SELECT count(*) FROM role_feature rf
-- JOIN feature f ON f.feature_id = rf.feature_id
-- WHERE f.feature_key = 'ssoma.gestion.ats';
-- Esperado: igual al total de roles en la tabla role.
