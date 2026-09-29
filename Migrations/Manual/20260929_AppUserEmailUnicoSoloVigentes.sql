-- ============================================================================
-- app_user.email: único solo entre los usuarios VIGENTES (state = true)
-- ============================================================================
-- La constraint uq_app_user_email era UNIQUE (email) sobre TODA la tabla, incluidos los usuarios
-- eliminados (state = false). Un correo de un usuario eliminado ya no se podía volver a usar:
-- Seguridad → Usuarios → «Crear usuario» caía con 23505 y el front mostraba «Error del servidor».
-- Regla del proyecto (ARCHITECTURE.md): varios registros con state = false, uno solo con true.
--
-- Queda sobre email exacto, igual que antes (solo cambia el alcance). No se pasa a lower(email):
-- ya hay correos vigentes que solo difieren en mayúsculas y el índice no se podría crear.
--
-- Va junto con el deploy del backend que filtra state en las búsquedas por correo: desde ahora un
-- correo puede tener una fila eliminada y otra vigente, y esas consultas tienen que ver solo la
-- vigente. Correr ANTES del deploy: el código nuevo ya no reusa la fila eliminada (antes el alta
-- de contratistas le ponía contraseña a un usuario con state = false, que igual no podía entrar),
-- sino que crea una nueva, y eso necesita este índice.
-- Idempotente.
-- ============================================================================

BEGIN;

-- Guarda: la constraint vieja lo garantizaba, pero si hubiera dos vigentes con el mismo correo el
-- índice no se podría crear. Mejor abortar con un mensaje claro.
DO $$
DECLARE
    repetidos integer;
BEGIN
    SELECT count(*) INTO repetidos
    FROM (SELECT email FROM app_user WHERE state GROUP BY email HAVING count(*) > 1) x;

    IF repetidos > 0 THEN
        RAISE EXCEPTION 'Hay % correos repetidos entre usuarios vigentes. Revisar a mano. Abortado.', repetidos;
    END IF;
END $$;

ALTER TABLE app_user DROP CONSTRAINT IF EXISTS uq_app_user_email;
DROP INDEX IF EXISTS uq_app_user_email;

CREATE UNIQUE INDEX uq_app_user_email ON app_user (email) WHERE state;

COMMIT;

-- Verificación: tiene que decir "... (email) WHERE state"
SELECT indexdef FROM pg_indexes WHERE indexname = 'uq_app_user_email';
