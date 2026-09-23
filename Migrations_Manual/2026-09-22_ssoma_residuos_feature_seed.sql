-- ============================================================================
-- Módulo SSOMA — Gestión de Residuos de Obra (ResiduosFeature)
-- Da de alta las 8 featureKeys usadas por residuos.routes.ts (frontend) y
-- las concede a los roles del equipo SSOMA + administrador del sistema.
-- Ajustar la lista de role_id en el CROSS JOIN si se quiere sumar/quitar roles
-- (por ejemplo Administrador de Obra, id 60, si también debe declarar).
--
-- Roles concedidos por defecto:
--   1  ADMINISTRADOR DEL SISTEMA
--   9  JEFE SSOMA
--   70 COORDINADOR SSOMA
--   72 PREVENCIONISTA
--
-- Idempotente (se puede re-correr sin duplicar filas).
-- ============================================================================
BEGIN;

INSERT INTO feature (feature_key, module_id)
SELECT v.feature_key, m.module_id
FROM module m
CROSS JOIN (VALUES
  ('ssoma.gestion.residuos.tipos'),
  ('ssoma.gestion.residuos.eo-rs'),
  ('ssoma.gestion.residuos.autorizaciones-dme'),
  ('ssoma.gestion.residuos.viajes'),
  ('ssoma.gestion.residuos.declaraciones'),
  ('ssoma.gestion.residuos.constancias'),
  ('ssoma.gestion.residuos.constancias-finales'),
  ('ssoma.gestion.residuos.documentos-referencia')
) AS v(feature_key)
WHERE m.module_name = 'SSOMA'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = v.feature_key);

INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM feature f
CROSS JOIN (VALUES (1), (9), (70), (72)) AS r(role_id)
WHERE f.feature_key IN (
  'ssoma.gestion.residuos.tipos',
  'ssoma.gestion.residuos.eo-rs',
  'ssoma.gestion.residuos.autorizaciones-dme',
  'ssoma.gestion.residuos.viajes',
  'ssoma.gestion.residuos.declaraciones',
  'ssoma.gestion.residuos.constancias',
  'ssoma.gestion.residuos.constancias-finales',
  'ssoma.gestion.residuos.documentos-referencia'
)
AND NOT EXISTS (
  SELECT 1 FROM role_feature rf WHERE rf.role_id = r.role_id AND rf.feature_id = f.feature_id
);

COMMIT;

-- ============================================================================
-- Verificación (correr después; no modifica nada)
-- ============================================================================
-- SELECT f.feature_key, array_agg(rf.role_id ORDER BY rf.role_id) AS roles_con_acceso
-- FROM feature f
-- JOIN role_feature rf ON rf.feature_id = f.feature_id
-- WHERE f.feature_key LIKE 'ssoma.gestion.residuos.%'
-- GROUP BY f.feature_key
-- ORDER BY f.feature_key;
-- Esperado: {1,9,70,72} en cada fila.
