-- ============================================================================
-- Evaluaciones · "Mis resultados" de Gestión SSOMA (Flujo D) — feature + accesos
--
-- Hasta ahora, GET /gestion-ssoma/resultados (el consolidado de todo el equipo)
-- estaba candado solo a Jefe SSOMA — Coordinador SSOMA y Prevencionista podían
-- EVALUAR (D1-D4) pero no tenían dónde ver su propio promedio/comentarios
-- recibidos. Este seed habilita la pestaña de la nueva pantalla de resultados
-- personales (GET /gestion-ssoma/mis-resultados, filtrada por evaluado_user_id
-- en el servidor — nunca expone quién evaluó).
--
-- Sigue el mismo patrón que 2026-08-20_evaluaciones_jefe_ssoma_feature_seed.sql.
--
-- Accesos:
--   evaluaciones.mis-resultados-gestion-ssoma → roles 70 (Coordinador SSOMA) y
--     72 (Prevencionista) — mismos roles que EsEquipoSsomaAsync/
--     ParticipaDeGestionSsomaAsync exigen en el backend.
--
-- Idempotente. Aplicar en dev y prod.
-- ============================================================================

-- 1) Feature
INSERT INTO feature (feature_key, module_id)
SELECT 'evaluaciones.mis-resultados-gestion-ssoma', f.module_id
FROM feature f
WHERE f.feature_key = 'evaluaciones.evaluar'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'evaluaciones.mis-resultados-gestion-ssoma');

-- 2) Accesos
INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM feature f
CROSS JOIN (VALUES (70), (72)) AS r(role_id)
WHERE f.feature_key = 'evaluaciones.mis-resultados-gestion-ssoma'
  AND NOT EXISTS (SELECT 1 FROM role_feature rf WHERE rf.role_id = r.role_id AND rf.feature_id = f.feature_id);

-- ============================================================================
-- Verificación (correr después; no modifica nada)
-- ============================================================================
-- SELECT f.feature_key, array_agg(r.role_id ORDER BY r.role_id) AS roles_con_acceso
-- FROM feature f
-- JOIN role_feature rf ON rf.feature_id = f.feature_id
-- JOIN role r ON r.role_id = rf.role_id
-- WHERE f.feature_key = 'evaluaciones.mis-resultados-gestion-ssoma'
-- GROUP BY f.feature_key;
-- Esperado: {70,72}.
