-- ============================================================================
-- Gestión GTH · Reclutamiento — Rol USUARIO DE TI (solo lectura)
-- PASO 1 de 2 · ANTES DE DESPLEGAR EL BACKEND Y EL FRONTEND
-- Fecha: 2026-09-24
--
-- Reclutamiento queda con dos features:
--   · gestion-gth.reclutamiento            → VER: entrar, la bandeja y el detalle.
--   · gestion-gth.reclutamiento.gestionar  → GESTIONAR (nueva): todo lo que mueve el
--     proceso — prioridad, asignación interna, publicación, long list, formulario del
--     postulante, Multitest, entrevistas, finalistas y carta oferta.
-- Un rol con solo la primera ve todo en solo lectura. Las features se suman entre los
-- roles del usuario: quien además tenga un rol con la segunda (USUARIO DE GTH) gestiona.
--
-- Este paso:
--   1) Crea la feature de gestionar y se la da a los roles que HOY tienen la de ver
--      (USUARIO DE GTH y ADMINISTRADOR DEL SISTEMA): siguen gestionando como antes.
--   2) Crea el rol USUARIO DE TI y se lo asigna a Manuel Abreu y Wilmer Montero, SIN
--      features todavía: con el código viejo la feature de ver alcanza para gestionar,
--      así que se la da el paso 2, después del deploy.
--
-- El role_id no se fija: lo asigna el sequence, porque el código no nombra el rol en
-- ninguna parte (todo se decide por feature). Los usuarios se buscan por correo.
--
-- Idempotente: se puede correr más de una vez. Aplicar en dev y prod.
-- ============================================================================

BEGIN;

-- Guarda: los dos usuarios tienen que existir y estar vigentes. Si falta alguno, no se
-- toca nada.
DO $$
DECLARE
    faltan text;
BEGIN
    SELECT string_agg(e.email, ', ')
    INTO faltan
    FROM (VALUES ('mabreu@abril.pe'), ('wmontero@abril.pe')) AS e(email)
    WHERE NOT EXISTS (SELECT 1 FROM app_user u WHERE lower(u.email) = e.email AND u.state);

    IF faltan IS NOT NULL THEN
        RAISE EXCEPTION 'No hay un usuario vigente con el correo: %. Abortado.', faltan;
    END IF;
END $$;

-- 1) La feature de gestionar, en el mismo módulo que la de ver (leído de la fila).
INSERT INTO feature (feature_key, module_id)
SELECT 'gestion-gth.reclutamiento.gestionar', module_id
FROM feature
WHERE feature_key = 'gestion-gth.reclutamiento'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'gestion-gth.reclutamiento.gestionar');

-- ... para los roles que hoy tienen la de ver. USUARIO DE TI queda fuera a propósito: si
-- este paso se vuelve a correr después del paso 2, ese rol ya tiene la de ver y no puede
-- ganar la de gestionar.
INSERT INTO role_feature (role_id, feature_id)
SELECT rf.role_id, gestionar.feature_id
FROM role_feature rf
JOIN feature ver ON ver.feature_id = rf.feature_id
                AND ver.feature_key = 'gestion-gth.reclutamiento'
JOIN role r      ON r.role_id = rf.role_id
CROSS JOIN feature gestionar
WHERE gestionar.feature_key = 'gestion-gth.reclutamiento.gestionar'
  AND r.role_description <> 'USUARIO DE TI'
ON CONFLICT (role_id, feature_id) DO NOTHING;

-- 2) El rol, con el sequence normal.
INSERT INTO role (role_description, created_user_id)
SELECT 'USUARIO DE TI', 1
WHERE NOT EXISTS (SELECT 1 FROM role WHERE role_description = 'USUARIO DE TI' AND state);

-- ... asignado a los dos. Si alguno lo tuvo y se le quitó, la fila se revive.
INSERT INTO user_role (user_id, role_id, created_user_id)
SELECT u.user_id, r.role_id, 1
FROM app_user u
CROSS JOIN role r
WHERE lower(u.email) IN ('mabreu@abril.pe', 'wmontero@abril.pe')
  AND u.state
  AND r.role_description = 'USUARIO DE TI'
  AND r.state
ON CONFLICT (user_id, role_id) DO UPDATE
   SET state             = true,
       active            = true,
       updated_date_time = now(),
       updated_user_id   = 1
 WHERE NOT user_role.state OR NOT user_role.active;

COMMIT;

-- ── Verificación ────────────────────────────────────────────────────────────
-- a) Qué roles tienen cada feature de Reclutamiento. Tras este paso, gestionar la tienen
--    los mismos roles que ver (1 y 77) y USUARIO DE TI no aparece en ninguna:
-- SELECT f.feature_key, r.role_id, r.role_description
-- FROM feature f
-- JOIN role_feature rf ON rf.feature_id = f.feature_id
-- JOIN role r ON r.role_id = rf.role_id
-- WHERE f.feature_key IN ('gestion-gth.reclutamiento', 'gestion-gth.reclutamiento.gestionar')
-- ORDER BY 1, 2;
--
-- b) El rol y sus dos usuarios:
-- SELECT r.role_id, r.role_description, u.user_id, u.email
-- FROM role r
-- LEFT JOIN user_role ur ON ur.role_id = r.role_id AND ur.state
-- LEFT JOIN app_user u ON u.user_id = ur.user_id
-- WHERE r.role_description = 'USUARIO DE TI'
-- ORDER BY u.email;
