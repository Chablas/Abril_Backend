-- ============================================================================
-- Cursos interactivos (capacitación tipo Genially con evaluación auditable) —
-- feature + acceso.
--
-- Sigue el mismo patrón que 2026-08-18_feature_programacion_inducciones.sql:
-- se cuelga del módulo 'SSOMA' ya existente.
--
-- Acceso: se concede a TODOS los roles activos, porque es una capacitación de
-- seguridad obligatoria tanto para staff de oficina como para personal de
-- obra (mismo criterio usado en 2026-08-17_actas_reunion_acceso_todos_los_roles.sql).
--
-- Idempotente. Aplicar en dev y prod.
-- ============================================================================

-- 1) Feature
INSERT INTO feature (feature_key, module_id)
SELECT 'cursos.lista', m.module_id
FROM module m
WHERE m.module_name = 'SSOMA'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'cursos.lista');

-- 2) Acceso: todos los roles activos
INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM feature f
CROSS JOIN role r
WHERE f.feature_key = 'cursos.lista'
  AND r.state
  AND NOT EXISTS (
      SELECT 1 FROM role_feature rf
      WHERE rf.role_id = r.role_id AND rf.feature_id = f.feature_id
  );

-- ============================================================================
-- Verificación (correr después; no modifica nada)
-- ============================================================================
-- SELECT ro.role_id, ro.role_description
-- FROM role_feature rf
-- JOIN feature f ON f.feature_id = rf.feature_id
-- JOIN role ro ON ro.role_id = rf.role_id
-- WHERE f.feature_key = 'cursos.lista'
-- ORDER BY ro.role_id;
