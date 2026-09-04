-- ============================================================================
-- Mis Rendiciones — nueva funcionalidad de Gestión Administrativa
--
-- Registra la feature y le da acceso a los mismos roles que ya tienen
-- "Solicitud de Salidas": es la continuación de esa pantalla (la rendición se
-- crea ahí y todo lo que sigue —Consolidado del S10, aviso al revisor y
-- seguimiento del reembolso— se hace acá).
--
--   12 = USUARIO DE ABRIL
--   52 = USUARIO DE RECEPCIÓN
--   76 = ADMINISTRADOR DE SOLICITUD DE SALIDAS
--
-- No hay cambios de esquema: la pantalla lee ga_rendicion, ga_solicitud_salida
-- y ga_consolidado_s10, que ya existen.
--
-- Re-ejecutable: los ON CONFLICT dejan la sentencia sin efecto si ya se corrió.
-- El feature_id sale de la secuencia, no se fija a mano.
-- ============================================================================

BEGIN;

INSERT INTO feature (feature_key, module_id)
VALUES ('gestion-administrativa.rendiciones', 10)   -- 10 = Gestión Administrativa
ON CONFLICT (feature_key) DO NOTHING;

INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM feature f
CROSS JOIN (VALUES (12), (52), (76)) AS r(role_id)
WHERE f.feature_key = 'gestion-administrativa.rendiciones'
ON CONFLICT DO NOTHING;

COMMIT;

-- Verificación
-- SELECT f.feature_id, f.feature_key, rf.role_id, ro.role_description
-- FROM feature f
-- LEFT JOIN role_feature rf ON rf.feature_id = f.feature_id
-- LEFT JOIN role ro         ON ro.role_id    = rf.role_id
-- WHERE f.feature_key = 'gestion-administrativa.rendiciones'
-- ORDER BY rf.role_id;
