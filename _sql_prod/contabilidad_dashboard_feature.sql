-- ============================================================================
-- Módulo Contabilidad — Feature del Dashboard de facturas + permisos.
-- Ejecutar en PRODUCCIÓN. Idempotente.
-- ============================================================================
BEGIN;

INSERT INTO feature (feature_key, module_id)
SELECT 'accounting.dashboard', m.module_id
FROM module m
WHERE m.module_name = 'Contabilidad'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'accounting.dashboard');

INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM role r
CROSS JOIN feature f
WHERE f.feature_key = 'accounting.dashboard'
  AND r.role_description IN ('USUARIO DE CONTABILIDAD', 'ADMINISTRADOR DEL SISTEMA')
  AND NOT EXISTS (
      SELECT 1 FROM role_feature rf WHERE rf.role_id = r.role_id AND rf.feature_id = f.feature_id
  );

COMMIT;
