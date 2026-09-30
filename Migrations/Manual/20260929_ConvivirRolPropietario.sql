-- ============================================================================
-- Convivir Abril (app móvil de propietarios): rol PROPIETARIO
-- ============================================================================
-- Solo los usuarios con este rol entran a la app (ConvivirModule). Se asigna solo desde el módulo
-- Propietarios de la intranet, o a mano desde Seguridad → Usuarios → «Crear usuario para persona
-- externa». En los dos casos la invitación lleva a la app (crear contraseña), no a la intranet.
--
-- Reemplaza a 20260929_ConvivirRolVecino.sql (el rol se llamaba VECINO): en un ambiente donde ese
-- script ya corrió (dev), este renombra el 97 a PROPIETARIO; donde no corrió (prod), lo crea.
-- Correr SOLO este.
--
-- No confundir con USUARIO DE VECINOS (62): ese es el personal de Abril que atiende a los vecinos
-- de una obra desde la intranet.
--
-- Id fijo 97: el código lo nombra por id (Roles.Propietario). No lleva role_feature: la app no usa
-- el catálogo de features de la intranet.
--
-- Idempotente. Si el 97 lo usa otro rol, o ya hay un PROPIETARIO con otro id, aborta sin tocar nada.
-- Correr ANTES del deploy del backend (sin el rol, la app no deja entrar a nadie; no rompe nada más).
-- ============================================================================

BEGIN;

DO $$
DECLARE
    otro_id integer;
BEGIN
    IF EXISTS (SELECT 1 FROM role
               WHERE role_id = 97 AND upper(role_description) NOT IN ('VECINO', 'PROPIETARIO')) THEN
        RAISE EXCEPTION 'El role_id 97 ya lo usa otro rol. Hay que elegir otro id y cambiarlo en Roles.cs y roles.ts. Abortado.';
    END IF;

    SELECT min(role_id) INTO otro_id
    FROM role
    WHERE upper(role_description) = 'PROPIETARIO' AND role_id <> 97;

    IF otro_id IS NOT NULL THEN
        RAISE EXCEPTION 'Ya existe un rol PROPIETARIO con id % (creado desde Seguridad?). Revisar a mano. Abortado.', otro_id;
    END IF;

    INSERT INTO role (role_id, role_description, created_user_id)
    VALUES (97, 'PROPIETARIO', 1)
    ON CONFLICT (role_id) DO NOTHING;

    UPDATE role
    SET role_description = 'PROPIETARIO',
        updated_date_time = now(),
        updated_user_id   = 1
    WHERE role_id = 97 AND role_description <> 'PROPIETARIO';

    PERFORM setval('role_role_id_seq',
                   GREATEST((SELECT max(role_id) FROM role), (SELECT last_value FROM role_role_id_seq)));
END $$;

COMMIT;

-- Verificación
SELECT role_id, role_description, active, state FROM role WHERE role_id = 97;

-- ── Operación (no correr como parte del script) ─────────────────────────────
-- Los plazos viven en la base, no en el token: se alargan o cortan con un UPDATE.
--
-- Alargar el enlace de invitación de un propietario que no lo abrió a tiempo:
--   UPDATE user_password_token SET expires_at = now() + interval '7 days'
--    WHERE user_id = <id> AND NOT used;
--
-- Alargar la sesión de la app de un propietario (expires_at es UTC, sin zona):
--   UPDATE user_session SET expires_at = (now() AT TIME ZONE 'UTC') + interval '90 days'
--    WHERE user_id = <id> AND NOT revoked;
--
-- Sacarlo de la app ya (en ≤ 2 minutos, al siguiente refresh):
--   UPDATE user_session SET revoked = true WHERE user_id = <id> AND NOT revoked;
