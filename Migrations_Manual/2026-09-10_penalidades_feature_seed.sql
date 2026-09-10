-- ============================================================================
-- Penalidades — features + accesos por rol
--
-- Sigue el mismo patrón que los demás feature seed de este repo (ej.
-- 2026-08-20_evaluaciones_jefe_ssoma_feature_seed.sql): el module_id se reutiliza
-- del de una feature 'ssoma.gestion.rac.*' ya existente, así no depende de conocer
-- el module_id/module_name exacto.
--
-- Accesos:
--   ssoma.gestion.penalidades.lista             → roles 5 (Residente), 9 (Jefe SSOMA),
--     61 (Oficina Técnica), 70 (Coordinador SSOMA), 72 (Prevencionista), + los mismos
--     roles que hoy tienen 'evaluaciones.evaluar' (Gerencia Inmobiliaria) — todos
--     "involucrados" según lo pedido, con visibilidad de lectura sobre el módulo.
--   ssoma.gestion.penalidades.crear              → roles 9, 70, 72 (quien tipifica)
--   ssoma.gestion.penalidades.aprobar-residente   → rol 5 (Residente)
--   ssoma.gestion.penalidades.aprobar-gerencia    → los mismos roles que hoy tienen
--     'evaluaciones.evaluar' — NO existe un rol "GERENTE INMOBILIARIO" en el sistema;
--     la persona que hoy evalúa residentes en /evaluaciones/evaluar es la misma que
--     aprueba/decide penalidades (confirmado por el usuario), así que se copia el
--     acceso dinámicamente en vez de fijar un role_id.
--   ssoma.gestion.penalidades.evaluar             → roles 9, 70, 72 (SSOMA evalúa el descargo)
--   ssoma.gestion.penalidades.catalogos           → rol 9 (Jefe SSOMA, admin del catálogo)
--
-- Idempotente. Aplicar en dev y prod.
-- ============================================================================

-- 1) Features
INSERT INTO feature (feature_key, module_id)
SELECT 'ssoma.gestion.penalidades.lista', f.module_id
FROM feature f
WHERE f.feature_key = 'ssoma.gestion.rac.lista'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'ssoma.gestion.penalidades.lista');

INSERT INTO feature (feature_key, module_id)
SELECT 'ssoma.gestion.penalidades.crear', f.module_id
FROM feature f
WHERE f.feature_key = 'ssoma.gestion.rac.lista'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'ssoma.gestion.penalidades.crear');

INSERT INTO feature (feature_key, module_id)
SELECT 'ssoma.gestion.penalidades.aprobar-residente', f.module_id
FROM feature f
WHERE f.feature_key = 'ssoma.gestion.rac.lista'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'ssoma.gestion.penalidades.aprobar-residente');

INSERT INTO feature (feature_key, module_id)
SELECT 'ssoma.gestion.penalidades.aprobar-gerencia', f.module_id
FROM feature f
WHERE f.feature_key = 'ssoma.gestion.rac.lista'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'ssoma.gestion.penalidades.aprobar-gerencia');

INSERT INTO feature (feature_key, module_id)
SELECT 'ssoma.gestion.penalidades.evaluar', f.module_id
FROM feature f
WHERE f.feature_key = 'ssoma.gestion.rac.lista'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'ssoma.gestion.penalidades.evaluar');

INSERT INTO feature (feature_key, module_id)
SELECT 'ssoma.gestion.penalidades.catalogos', f.module_id
FROM feature f
WHERE f.feature_key = 'ssoma.gestion.rac.lista'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'ssoma.gestion.penalidades.catalogos');

-- 2) Accesos — roles fijos
INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM feature f
CROSS JOIN (VALUES (5), (9), (61), (70), (72)) AS r(role_id)
WHERE f.feature_key = 'ssoma.gestion.penalidades.lista'
  AND NOT EXISTS (SELECT 1 FROM role_feature rf WHERE rf.role_id = r.role_id AND rf.feature_id = f.feature_id);

INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM feature f
CROSS JOIN (VALUES (9), (70), (72)) AS r(role_id)
WHERE f.feature_key = 'ssoma.gestion.penalidades.crear'
  AND NOT EXISTS (SELECT 1 FROM role_feature rf WHERE rf.role_id = r.role_id AND rf.feature_id = f.feature_id);

INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM feature f
CROSS JOIN (VALUES (5)) AS r(role_id)
WHERE f.feature_key = 'ssoma.gestion.penalidades.aprobar-residente'
  AND NOT EXISTS (SELECT 1 FROM role_feature rf WHERE rf.role_id = r.role_id AND rf.feature_id = f.feature_id);

INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM feature f
CROSS JOIN (VALUES (9), (70), (72)) AS r(role_id)
WHERE f.feature_key = 'ssoma.gestion.penalidades.evaluar'
  AND NOT EXISTS (SELECT 1 FROM role_feature rf WHERE rf.role_id = r.role_id AND rf.feature_id = f.feature_id);

INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM feature f
CROSS JOIN (VALUES (9)) AS r(role_id)
WHERE f.feature_key = 'ssoma.gestion.penalidades.catalogos'
  AND NOT EXISTS (SELECT 1 FROM role_feature rf WHERE rf.role_id = r.role_id AND rf.feature_id = f.feature_id);

-- 3) Acceso dinámico — aprobar-gerencia: mismos roles que hoy tienen 'evaluaciones.evaluar'
-- (no existe un rol "GERENTE INMOBILIARIO" fijo; se copia el acceso del rol real que usa
-- esa persona hoy para evaluar residentes).
INSERT INTO role_feature (role_id, feature_id)
SELECT rf_eval.role_id, f_pen.feature_id
FROM role_feature rf_eval
JOIN feature f_eval ON f_eval.feature_id = rf_eval.feature_id AND f_eval.feature_key = 'evaluaciones.evaluar'
CROSS JOIN (SELECT feature_id FROM feature WHERE feature_key = 'ssoma.gestion.penalidades.aprobar-gerencia') f_pen
WHERE NOT EXISTS (
    SELECT 1 FROM role_feature rf2 WHERE rf2.role_id = rf_eval.role_id AND rf2.feature_id = f_pen.feature_id
);

-- Que Gerencia también pueda ver la lista/detalle (no solo aprobar):
INSERT INTO role_feature (role_id, feature_id)
SELECT rf_eval.role_id, f_pen.feature_id
FROM role_feature rf_eval
JOIN feature f_eval ON f_eval.feature_id = rf_eval.feature_id AND f_eval.feature_key = 'evaluaciones.evaluar'
CROSS JOIN (SELECT feature_id FROM feature WHERE feature_key = 'ssoma.gestion.penalidades.lista') f_pen
WHERE NOT EXISTS (
    SELECT 1 FROM role_feature rf2 WHERE rf2.role_id = rf_eval.role_id AND rf2.feature_id = f_pen.feature_id
);

-- ============================================================================
-- Verificación (correr después; no modifica nada)
-- ============================================================================
-- SELECT f.feature_key, array_agg(r.role_id ORDER BY r.role_id) AS roles_con_acceso
-- FROM feature f
-- JOIN role_feature rf ON rf.feature_id = f.feature_id
-- JOIN role r ON r.role_id = rf.role_id
-- WHERE f.feature_key LIKE 'ssoma.gestion.penalidades.%'
-- GROUP BY f.feature_key;
