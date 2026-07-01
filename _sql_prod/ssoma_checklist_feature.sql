-- ============================================================================
-- Módulo SSOMA — Feature Checklists SSOMA
-- Ejecutar en PRODUCCIÓN. Idempotente.
-- ============================================================================
BEGIN;

INSERT INTO feature (feature_key, module_id)
SELECT 'ssoma.gestion.checklist', m.module_id
FROM module m
WHERE m.module_name = 'SSOMA'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'ssoma.gestion.checklist');

INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM role r
CROSS JOIN feature f
WHERE f.feature_key = 'ssoma.gestion.checklist'
  AND r.role_description IN ('USUARIO DE ABRIL', 'ADMINISTRADOR DEL SISTEMA')
  AND NOT EXISTS (
      SELECT 1 FROM role_feature rf WHERE rf.role_id = r.role_id AND rf.feature_id = f.feature_id
  );

COMMIT;
