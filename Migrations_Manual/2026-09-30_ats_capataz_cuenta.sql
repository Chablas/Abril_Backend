-- ============================================================================
-- ATS — cuenta propia para Capataz / Maestro de obra.
--
-- Como firman por toda la cuadrilla, no basta DNI+selfie: se les crea usuario con
-- contraseña propia. El correo PERSONAL se registra en su autorización de uso de
-- firma digital (SSO-FO-151) junto con una declaración de que es de uso personal
-- y exclusivo. Al subirse el escaneado firmado, el sistema crea el usuario y le
-- envía el correo para generar su contraseña.
--
-- 1) ss_ats_autorizacion_permiso: correo + fecha de la declaración.
-- 2) Rol propio "CAPATAZ / MAESTRO DE OBRA" con acceso al módulo ATS.
--
-- Idempotente. El role_id lo asigna la secuencia (no se hardcodea, ver regla D3).
-- ============================================================================

BEGIN;

ALTER TABLE ss_ats_autorizacion_permiso ADD COLUMN IF NOT EXISTS email_personal text;
ALTER TABLE ss_ats_autorizacion_permiso ADD COLUMN IF NOT EXISTS email_declarado_en timestamp;

INSERT INTO role (role_description, created_user_id, active, state)
SELECT 'CAPATAZ / MAESTRO DE OBRA', (SELECT min(user_id) FROM app_user), true, true
WHERE NOT EXISTS (SELECT 1 FROM role WHERE upper(role_description) = 'CAPATAZ / MAESTRO DE OBRA');

INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM role r
CROSS JOIN feature f
WHERE upper(r.role_description) = 'CAPATAZ / MAESTRO DE OBRA'
  AND f.feature_key = 'ssoma.gestion.ats'
  AND NOT EXISTS (
    SELECT 1 FROM role_feature rf WHERE rf.role_id = r.role_id AND rf.feature_id = f.feature_id
  );

COMMIT;

-- Verificación (no modifica nada):
-- SELECT r.role_id, r.role_description, count(rf.*) AS features
-- FROM role r LEFT JOIN role_feature rf ON rf.role_id = r.role_id
-- WHERE upper(r.role_description) = 'CAPATAZ / MAESTRO DE OBRA' GROUP BY 1, 2;
-- Esperado: 1 fila con 1 feature.
