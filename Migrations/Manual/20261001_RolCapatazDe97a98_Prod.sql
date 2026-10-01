-- ============================================================================
-- Prod: el rol CAPATAZ / MAESTRO DE OBRA pasa del 97 al 98, para que el 97 quede para PROPIETARIO
-- ============================================================================
-- SOLO PROD. Ahí Migrations_Manual/2026-09-30_ats_capataz_cuenta.sql (Samuel) creó CAPATAZ /
-- MAESTRO DE OBRA con la secuencia, que estaba en 96, y le tocó el 97: el id que Convivir usa fijo
-- para PROPIETARIO (Roles.Propietario, roles.ts y 20260929_ConvivirRolPropietario.sql, que aborta
-- si el 97 es otro rol). En demo CAPATAZ es el 99 y en dev no existe: ahí este script aborta.
--
-- El código de Samuel busca el rol por nombre (AtsRepository.GetRoleIdCapataz), así que cambiarle
-- el id no le cambia nada. Al 2026-10-01 no lo tenía ningún usuario y solo tenía ssoma.gestion.ats;
-- si al correrlo alguien lo tiene, pasa con él al 98 (lo ve en el siguiente refresh del token).
--
-- Pasa al 98 todo lo que apunta al 97 por FK (role_feature, user_role...), borra la fila 97, que
-- queda vacía (el rol es el mismo, con otro id), y adelanta la secuencia. Después va
-- 20260929_ConvivirRolPropietario.sql, que crea PROPIETARIO en el 97.
--
-- Decisión del usuario del 2026-10-01: mover CAPATAZ en prod y no PROPIETARIO en el código.
-- Idempotente: si CAPATAZ ya es el 98, no hace nada.
-- ============================================================================

SET client_encoding TO 'UTF8';

BEGIN;

SET LOCAL lock_timeout = '15s';

DO $$
DECLARE
    fk record;
    n  integer;
BEGIN
    IF EXISTS (SELECT 1 FROM role WHERE role_id = 98 AND upper(role_description) = 'CAPATAZ / MAESTRO DE OBRA')
       AND NOT EXISTS (SELECT 1 FROM role WHERE role_id = 97 AND upper(role_description) = 'CAPATAZ / MAESTRO DE OBRA') THEN
        RAISE NOTICE 'CAPATAZ / MAESTRO DE OBRA ya es el 98: nada que hacer.';
        RETURN;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM role WHERE role_id = 97 AND upper(role_description) = 'CAPATAZ / MAESTRO DE OBRA') THEN
        RAISE EXCEPTION 'El role_id 97 no es CAPATAZ / MAESTRO DE OBRA: este script es solo para prod. Abortado.';
    END IF;
    IF EXISTS (SELECT 1 FROM role WHERE role_id = 98) THEN
        RAISE EXCEPTION 'El role_id 98 ya lo usa otro rol. Revisar a mano. Abortado.';
    END IF;
    IF (SELECT count(*) FROM role WHERE upper(role_description) = 'CAPATAZ / MAESTRO DE OBRA') > 1 THEN
        RAISE EXCEPTION 'Hay más de un rol CAPATAZ / MAESTRO DE OBRA. Revisar a mano. Abortado.';
    END IF;

    INSERT INTO role (role_id, role_description, created_date_time, created_user_id,
                      updated_date_time, updated_user_id, active, state)
    SELECT 98, role_description, created_date_time, created_user_id, now(), 1, active, state
    FROM role
    WHERE role_id = 97;

    -- Todo lo que apunta al 97 por FK pasa al 98.
    FOR fk IN
        SELECT c.conrelid::regclass AS tabla, a.attname AS col
        FROM pg_constraint c
        JOIN unnest(c.conkey) AS k(attnum) ON true
        JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = k.attnum
        WHERE c.contype = 'f' AND c.confrelid = 'public.role'::regclass
    LOOP
        EXECUTE format('UPDATE %s SET %I = 98 WHERE %I = 97', fk.tabla, fk.col, fk.col);
        GET DIAGNOSTICS n = ROW_COUNT;
        IF n > 0 THEN
            RAISE NOTICE '%.%: % fila(s) del 97 al 98.', fk.tabla, fk.col, n;
        END IF;
    END LOOP;

    DELETE FROM role WHERE role_id = 97;

    PERFORM setval('role_role_id_seq',
                   GREATEST((SELECT max(role_id) FROM role), (SELECT last_value FROM role_role_id_seq)));
END $$;

COMMIT;

-- Verificación: CAPATAZ / MAESTRO DE OBRA en el 98 con ssoma.gestion.ats, y el 97 libre hasta correr
-- 20260929_ConvivirRolPropietario.sql.
SELECT r.role_id, r.role_description, r.state,
       (SELECT string_agg(f.feature_key, ', ')
        FROM role_feature rf JOIN feature f ON f.feature_id = rf.feature_id
        WHERE rf.role_id = r.role_id)                                       AS funcionalidades,
       (SELECT count(*) FROM user_role ur WHERE ur.role_id = r.role_id)     AS usuarios
FROM role r
WHERE r.role_id IN (97, 98)
ORDER BY r.role_id;
