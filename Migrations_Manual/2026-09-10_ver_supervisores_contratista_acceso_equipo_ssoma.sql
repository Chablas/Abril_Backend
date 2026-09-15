-- ============================================================================
-- Evaluaciones · Supervisores de Contratista (Flujo A) — ampliar acceso a
-- "Ver evaluaciones" (GET /supervisores-contratista/ver)
--
-- Hasta ahora solo el Jefe SSOMA (rol 9) podía ver el consolidado. Coordinador
-- SSOMA (70) y Prevencionista (72) son quienes REALIZAN esta evaluación —
-- deben poder ver lo que ellos mismos evaluaron, no solo evaluar a ciegas.
-- El backend ya se amplió (EvSupervisorContratistaController.GetVer ahora usa
-- PuedeEvaluarSupervisoresAsync en vez de EsJefeSsomaAsync); este script solo
-- agrega los accesos que faltan en role_feature para que la pestaña se vea.
--
-- Idempotente. Aplicar en dev y prod.
-- ============================================================================

INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM feature f
CROSS JOIN (VALUES (70), (72)) AS r(role_id)
WHERE f.feature_key = 'evaluaciones.ver-supervisores-contratista'
  AND NOT EXISTS (SELECT 1 FROM role_feature rf WHERE rf.role_id = r.role_id AND rf.feature_id = f.feature_id);

-- ============================================================================
-- Verificación (correr después; no modifica nada)
-- ============================================================================
-- SELECT f.feature_key, array_agg(r.role_id ORDER BY r.role_id) AS roles_con_acceso
-- FROM feature f
-- JOIN role_feature rf ON rf.feature_id = f.feature_id
-- JOIN role r ON r.role_id = rf.role_id
-- WHERE f.feature_key = 'evaluaciones.ver-supervisores-contratista'
-- GROUP BY f.feature_key;
-- Esperado: {9,70,72}.
