-- ============================================================================
-- OPT · GET /api/v1/ssoma-opt/{id} (GetDetalle) exige el feature base
-- 'ssoma.gestion.opt' (ver OptController.cs), distinto de 'ssoma.gestion.opt.lista'
-- y 'ssoma.gestion.opt.nuevo'. Los roles de contratista tienen lista/nuevo pero
-- no el base, así que "Continuar llenando" un borrador (o abrir el detalle de una
-- OPT finalizada) les da 403 aunque sí puedan crear/ver la lista.
--
-- Se otorga 'ssoma.gestion.opt' a cualquier rol que ya tenga lista o nuevo y
-- todavía no lo tenga — cubre al rol de contratista sin necesitar adivinar su
-- role_id, y a cualquier otro rol en el mismo caso.
--
-- Idempotente. Aplicar en dev y prod.
-- ============================================================================

INSERT INTO role_feature (role_id, feature_id)
SELECT DISTINCT rf_origen.role_id, f_base.feature_id
FROM role_feature rf_origen
JOIN feature f_origen ON f_origen.feature_id = rf_origen.feature_id
JOIN feature f_base ON f_base.feature_key = 'ssoma.gestion.opt'
WHERE f_origen.feature_key IN ('ssoma.gestion.opt.lista', 'ssoma.gestion.opt.nuevo')
  AND NOT EXISTS (
    SELECT 1 FROM role_feature rf
    WHERE rf.role_id = rf_origen.role_id AND rf.feature_id = f_base.feature_id
  );

-- ============================================================================
-- Verificación (correr después; no modifica nada)
-- ============================================================================
-- SELECT r.role_id, r.name,
--   bool_or(f.feature_key = 'ssoma.gestion.opt.lista') AS tiene_lista,
--   bool_or(f.feature_key = 'ssoma.gestion.opt.nuevo') AS tiene_nuevo,
--   bool_or(f.feature_key = 'ssoma.gestion.opt') AS tiene_base
-- FROM role r
-- JOIN role_feature rf ON rf.role_id = r.role_id
-- JOIN feature f ON f.feature_id = rf.feature_id
-- WHERE f.feature_key LIKE 'ssoma.gestion.opt%'
-- GROUP BY r.role_id, r.name
-- ORDER BY r.role_id;
-- Esperado: tiene_base = true en toda fila donde tiene_lista o tiene_nuevo sea true.
