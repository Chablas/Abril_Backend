-- ============================================================================
-- Configuración → Proyectos — Quién edita proyectos y quién asigna el residente
-- ANTES DE DESPLEGAR EL BACKEND Y EL FRONTEND
-- Fecha: 2026-09-28
--
-- Con el código nuevo:
--   · Todos los roles ven los proyectos en solo lectura.
--   · Crean, editan y eliminan proyectos: ADMINISTRADOR DEL SISTEMA, JEFE DE PROYECTOS,
--     COORDINADOR DE PROYECTOS, GERENTE INMOBILIARIO y RESIDENTE.
--   · El residente del proyecto lo asignan solo los cuatro primeros, en Configuración →
--     Proyectos y en Habilitación → Gestión de responsables: da permisos en el Cronograma
--     de Hitos.
-- El código lo decide por rol (Roles.cs / roles.ts), así que los tres roles del cronograma
-- necesitan el MISMO role_id en todos los ambientes, y el sequence les dio IDs distintos
-- (dev 91-93, demo 86-88; en prod todavía no existen). Este paso:
--   1) Deja COORDINADOR DE PROYECTOS = 91, GERENTE INMOBILIARIO = 92 y JEFE DE PROYECTOS = 93.
--      Si el rol no existe, lo crea con ese id. Si existe con otro id, crea el fijo, le pasa
--      sus features y usuarios, y da de baja el viejo (state = false). Adelanta el sequence.
--   2) Da configuracion.proyectos a los cinco roles que editan, para que entren a la pantalla
--      aunque no tengan USUARIO DE ABRIL.
--   3) Crea project_residente_historial: la bitácora de cambios del residente que escribe el
--      backend (quién lo cambió y cuándo).
--
-- Idempotente. No depende del orden con 20260928_CronogramaHitosRoles.sql: si este corre
-- primero, aquel solo les agrega features y usuarios a los roles que ya encuentra. Aplicar en
-- dev, demo y prod. Los usuarios lo ven al volver a iniciar sesión (el role_id viaja en el token).
-- ============================================================================

BEGIN;

-- Guardas: si falta algo de lo que se asume, no se toca nada.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM role WHERE role_id = 1 AND role_description = 'ADMINISTRADOR DEL SISTEMA' AND state) THEN
        RAISE EXCEPTION 'El role_id 1 no es ADMINISTRADOR DEL SISTEMA vigente. Abortado.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM role WHERE role_id = 5 AND role_description = 'RESIDENTE' AND state) THEN
        RAISE EXCEPTION 'El role_id 5 no es RESIDENTE vigente. Abortado.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'configuracion.proyectos') THEN
        RAISE EXCEPTION 'No existe la feature configuracion.proyectos. Abortado.';
    END IF;
END $$;

-- 1) Los tres roles con id fijo.
DO $$
DECLARE
    r       record;
    viejo   integer;
    cuantos integer;
BEGIN
    FOR r IN
        SELECT * FROM (VALUES (91, 'COORDINADOR DE PROYECTOS'),
                              (92, 'GERENTE INMOBILIARIO'),
                              (93, 'JEFE DE PROYECTOS')) AS t(id, nombre)
    LOOP
        IF EXISTS (SELECT 1 FROM role WHERE role_id = r.id AND role_description <> r.nombre) THEN
            RAISE EXCEPTION 'El role_id % ya lo usa otro rol (no %). Abortado.', r.id, r.nombre;
        END IF;

        -- El rol vigente con ese nombre y otro id: el que creó el sequence.
        SELECT count(*), min(role_id) INTO cuantos, viejo
        FROM role
        WHERE role_description = r.nombre AND state AND role_id <> r.id;

        IF cuantos > 1 THEN
            RAISE EXCEPTION 'Hay % roles vigentes llamados % fuera del id %. Revisar a mano. Abortado.', cuantos, r.nombre, r.id;
        END IF;

        IF EXISTS (SELECT 1 FROM role WHERE role_id = r.id) THEN
            -- Ya está en su id. Si además hay otro vigente con el mismo nombre, no se adivina cuál vale.
            IF viejo IS NOT NULL THEN
                RAISE EXCEPTION 'El rol % existe como % y como %. Revisar a mano. Abortado.', r.nombre, r.id, viejo;
            END IF;
            CONTINUE;
        END IF;

        IF viejo IS NULL THEN
            INSERT INTO role (role_id, role_description, created_user_id)
            VALUES (r.id, r.nombre, 1);
        ELSE
            -- El id fijo es nuevo: nadie lo referencia todavía, así que mover las filas no choca
            -- con ninguna clave única.
            INSERT INTO role (role_id, role_description, created_date_time, created_user_id, active, state)
            SELECT r.id, role_description, created_date_time, created_user_id, active, state
            FROM role
            WHERE role_id = viejo;

            UPDATE role_feature           SET role_id = r.id WHERE role_id = viejo;
            UPDATE user_role              SET role_id = r.id WHERE role_id = viejo;
            UPDATE learning_category_role SET role_id = r.id WHERE role_id = viejo;
            UPDATE ga_correo_regla        SET role_id = r.id WHERE role_id = viejo;

            UPDATE role
               SET state = false, active = false, updated_date_time = now(), updated_user_id = 1
             WHERE role_id = viejo;
        END IF;
    END LOOP;

    -- Los ids se pusieron a mano: el sequence tiene que quedar por encima.
    PERFORM setval('role_role_id_seq',
                   GREATEST((SELECT max(role_id) FROM role), (SELECT last_value FROM role_role_id_seq)));
END $$;

-- 2) La pantalla, para los cinco roles que editan proyectos.
INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM role r
CROSS JOIN feature f
WHERE r.role_id IN (1, 5, 91, 92, 93)
  AND r.state
  AND f.feature_key = 'configuracion.proyectos'
ON CONFLICT (role_id, feature_id) DO NOTHING;

-- 3) Bitácora de cambios del residente.
CREATE TABLE IF NOT EXISTS project_residente_historial (
    id                  integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    project_id          integer NOT NULL,
    workers_id_anterior integer NULL,
    workers_id_nuevo    integer NULL,
    cambio_date_time    timestamp with time zone NOT NULL,
    cambio_user_id      integer NULL,
    created_date_time   timestamp with time zone NOT NULL DEFAULT now(),

    CONSTRAINT fk_project_residente_historial_project
        FOREIGN KEY (project_id) REFERENCES project (project_id),
    CONSTRAINT fk_project_residente_historial_workers_anterior
        FOREIGN KEY (workers_id_anterior) REFERENCES workers (id),
    CONSTRAINT fk_project_residente_historial_workers_nuevo
        FOREIGN KEY (workers_id_nuevo) REFERENCES workers (id)
);

CREATE INDEX IF NOT EXISTS ix_project_residente_historial_project_id
    ON project_residente_historial (project_id, cambio_date_time);

COMMIT;

-- ── Verificación ────────────────────────────────────────────────────────────
-- a) Los tres roles en su id y vigentes; los viejos (si había) de baja:
-- SELECT role_id, role_description, state
-- FROM role
-- WHERE role_description IN ('COORDINADOR DE PROYECTOS', 'GERENTE INMOBILIARIO', 'JEFE DE PROYECTOS')
-- ORDER BY 1;
--
-- b) Conservan sus features y sus usuarios:
-- SELECT rf.role_id, f.feature_key
-- FROM role_feature rf JOIN feature f ON f.feature_id = rf.feature_id
-- WHERE rf.role_id IN (91, 92, 93)
-- ORDER BY 1, 2;
-- SELECT ur.role_id, u.email
-- FROM user_role ur JOIN app_user u ON u.user_id = ur.user_id
-- WHERE ur.role_id IN (91, 92, 93) AND ur.state
-- ORDER BY 1;
--
-- c) Quién entra a Configuración → Proyectos (deben estar 1, 5, 91, 92 y 93):
-- SELECT rf.role_id, r.role_description
-- FROM role_feature rf
-- JOIN feature f ON f.feature_id = rf.feature_id
-- JOIN role r ON r.role_id = rf.role_id
-- WHERE f.feature_key = 'configuracion.proyectos'
-- ORDER BY 1;
--
-- d) La bitácora existe (vacía hasta el primer cambio de residente):
-- SELECT count(*) FROM project_residente_historial;
