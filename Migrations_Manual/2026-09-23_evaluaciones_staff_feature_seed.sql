-- ============================================================================
-- Evaluaciones · Staff 360° (Residente evalúa a su staff de proyecto) — feature + accesos
--
-- Nuevas pantallas evaluar-staff / resultados-staff (ver
-- Migrations_Manual/2026-09-23_evaluacion_360_staff.sql para el esquema de
-- datos). Sin este seed, roleGuard bloquea la ruta porque el featureKey no
-- existe todavía en ningún rol (allowed_features en localStorage viene de
-- role_feature).
--
-- Accesos:
--   evaluaciones.evaluar-staff      → role 5 (RESIDENTE)
--   evaluaciones.resultados-staff   → role 5 (RESIDENTE)
--
-- Sigue el mismo patrón que 2026-09-10_evaluaciones_mis_resultados_gestion_ssoma_feature_seed.sql.
-- Idempotente. Aplicar en dev y prod.
-- ============================================================================

-- 1) Features
INSERT INTO feature (feature_key, module_id)
SELECT 'evaluaciones.evaluar-staff', f.module_id
FROM feature f
WHERE f.feature_key = 'evaluaciones.evaluar'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'evaluaciones.evaluar-staff');

INSERT INTO feature (feature_key, module_id)
SELECT 'evaluaciones.resultados-staff', f.module_id
FROM feature f
WHERE f.feature_key = 'evaluaciones.evaluar'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'evaluaciones.resultados-staff');

-- 2) Accesos (role_id 5 = RESIDENTE)
INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM feature f
CROSS JOIN (VALUES (5)) AS r(role_id)
WHERE f.feature_key IN ('evaluaciones.evaluar-staff', 'evaluaciones.resultados-staff')
  AND NOT EXISTS (SELECT 1 FROM role_feature rf WHERE rf.role_id = r.role_id AND rf.feature_id = f.feature_id);

-- ============================================================================
-- Verificación (correr después; no modifica nada)
-- ============================================================================
-- SELECT f.feature_key, array_agg(r.role_id ORDER BY r.role_id) AS roles_con_acceso
-- FROM feature f
-- JOIN role_feature rf ON rf.feature_id = f.feature_id
-- JOIN role r ON r.role_id = rf.role_id
-- WHERE f.feature_key IN ('evaluaciones.evaluar-staff', 'evaluaciones.resultados-staff')
-- GROUP BY f.feature_key;
-- Esperado: ambas filas con {5}.
