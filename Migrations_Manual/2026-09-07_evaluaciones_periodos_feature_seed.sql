-- ============================================================================
-- Evaluaciones · Períodos (administración) — feature + acceso
--
-- Sigue el mismo patrón que 2026-08-20_evaluaciones_supervisores_contratista_feature_seed.sql:
-- el module_id se reutiliza del de una feature 'evaluaciones.*' ya existente
-- ('evaluaciones.evaluar').
--
-- Acceso: se otorga a TODOS los roles que hoy tiene el usuario
-- sjustiniani@abril.pe (no un role_id fijo, para no depender de conocerlo).
--
-- Idempotente. Aplicar en dev y prod.
-- ============================================================================

-- 1) Feature
INSERT INTO feature (feature_key, module_id)
SELECT 'evaluaciones.periodos', f.module_id
FROM feature f
WHERE f.feature_key = 'evaluaciones.evaluar'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'evaluaciones.periodos');

-- 2) Acceso: todos los roles activos del usuario por email
INSERT INTO role_feature (role_id, feature_id)
SELECT ur.role_id, f.feature_id
FROM feature f
JOIN app_user u   ON u.email = 'sjustiniani@abril.pe'
JOIN user_role ur ON ur.user_id = u.user_id
WHERE f.feature_key = 'evaluaciones.periodos'
  AND NOT EXISTS (
    SELECT 1 FROM role_feature rf WHERE rf.role_id = ur.role_id AND rf.feature_id = f.feature_id
  );

-- ============================================================================
-- Verificación (correr después; no modifica nada)
-- ============================================================================
-- SELECT f.feature_key, array_agg(r.role_id ORDER BY r.role_id) AS roles_con_acceso
-- FROM feature f
-- JOIN role_feature rf ON rf.feature_id = f.feature_id
-- JOIN role r ON r.role_id = rf.role_id
-- WHERE f.feature_key = 'evaluaciones.periodos'
-- GROUP BY f.feature_key;
