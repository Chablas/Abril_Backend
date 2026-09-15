-- ============================================================================
-- Seguridad — Funcionalidades
-- Fecha: 2026-09-14
--
-- Tercera pestaña de Seguridad (/security/features), junto a Usuarios y Roles:
-- lista las funcionalidades del sistema y, en su detalle, qué roles las tienen
-- y qué usuarios acceden por ellos. Es solo lectura: las funcionalidades se
-- siguen dando de alta por base de datos.
--
-- Se otorga a los mismos roles que ya tienen Seguridad → Roles. Para verla sin
-- cerrar sesión basta con recargar la página: el refresh del token trae las
-- funcionalidades nuevas.
--
-- Idempotente: se puede correr varias veces. Aplicar en dev y prod.
-- ============================================================================

BEGIN;

-- Mismo módulo que la pantalla de Roles (Seguridad), leído de la fila y no asumido.
INSERT INTO feature (feature_key, module_id)
SELECT 'security.features', module_id
FROM feature
WHERE feature_key = 'security.roles'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'security.features');

INSERT INTO role_feature (role_id, feature_id)
SELECT rf.role_id, nueva.feature_id
FROM role_feature rf
JOIN feature roles ON roles.feature_id = rf.feature_id
                  AND roles.feature_key = 'security.roles'
JOIN feature nueva ON nueva.feature_key = 'security.features'
WHERE NOT EXISTS (
    SELECT 1
    FROM role_feature x
    WHERE x.role_id = rf.role_id
      AND x.feature_id = nueva.feature_id
);

COMMIT;
