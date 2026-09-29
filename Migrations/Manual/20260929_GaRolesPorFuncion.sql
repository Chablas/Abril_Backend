-- ============================================================================
-- Gestión Administrativa — el acceso sale de los roles de la FUNCIÓN
-- DESPUÉS DE DESPLEGAR EL BACKEND Y EL FRONTEND (correrlo antes tampoco rompe nada)
-- Fecha: 2026-09-29
--
-- Hasta hoy entrar a las bandejas dependía de dos roles «por pantalla»: ADMINISTRADOR DE SOLICITUD
-- DE SALIDAS (76, que además daba la configuración y editar Revisores de Áreas con vista global) y
-- USUARIO REVISOR DE SALIDAS (78, que el login de Microsoft asignaba solo a los aprobadores
-- designados). Con este paso:
--
--   Pantalla                  Roles
--   ------------------------  --------------------------------------------------------------
--   Solicitud de Salidas      USUARIO DE ABRIL
--   Mis Rendiciones           USUARIO DE ABRIL
--   Gestión de Salidas        JEFE, SUB GERENTE, GERENTE, RESIDENTE, ADMINISTRADOR DE OBRA,
--                             COORDINADOR DE ADMINISTRACIÓN DE OBRA
--   Gestión de Rendiciones    los mismos + CONSOLIDADOR
--   Consolidados              los mismos + CONSOLIDADOR
--   Correcciones S10          COORDINADOR ERP
--   Reembolsos                TESORERO
--   Delegación de Revisión    JEFE, SUB GERENTE, GERENTE, RESIDENTE, COORDINADOR DE ADM. DE OBRA
--   Revisores de Áreas        ADMINISTRADOR DEL SISTEMA y USUARIO DE GTH (todo), JEFE (su área:
--                             solo los consolidadores de oficina central)
--   Resto de Configuración    ADMINISTRADOR DEL SISTEMA (recibe lo que solo tenía el 76)
--   USUARIO DE RECEPCIÓN, USUARIO DE GTH y ADMINISTRADOR DEL SISTEMA conservan todo lo que tenían.
--
-- Pasos:
--   1) Crea con id fijo SUB GERENTE (94), CONSOLIDADOR (95) y COORDINADOR DE ADMINISTRACIÓN DE
--      OBRA (96) —el código los nombra por id (Roles.cs / roles.ts)— y adelanta el sequence.
--   2) Da a cada rol sus pantallas (ON CONFLICT DO NOTHING).
--   3) Da de baja 76 y 78: role.state = false y DELETE de sus user_role (como Seguridad: el token
--      no mira user_role.state). Sus role_feature quedan como registro.
--   4) Roles del PUESTO, solo agrega: JEFE (categoría JEFE), SUB GERENTE, GERENTE (categoría
--      GERENTE), RESIDENTE (categoría RESIDENTE), TESORERO (categoría TESORERO), ADMINISTRADOR DE
--      OBRA (administra una obra activa de tipo PROYECTO), COORDINADOR DE ADMINISTRACIÓN DE OBRA
--      (puesto COORDINADOR ADMINISTRATIVO DE OBRA) y COORDINADOR ERP (puesto COORDINADOR ERP).
--   4b) TESORERO solo para quien es tesorero(a): se le quita a quien no tiene un puesto de categoría
--      TESORERO (en prod, vpolo: su puesto es COORDINADOR ERP y recibe ese rol en el paso 4).
--   5) Quien figura A MANO como aprobador (Revisores de Áreas, ficha, o residente de una obra en
--      Configuración → Proyectos) y no entra a la bandeja donde actúa recibe la jefatura de su
--      puesto (JEFE si no es jefatura). Solo agrega.
--   6) CONSOLIDADOR exacto: quien figura a mano como consolidador y no entra ya a Gestión de
--      Rendiciones y Consolidados por otro rol. Desde el deploy lo recalcula el backend al guardar.
--   Las reglas 4-6 son las de Shared/Services/RolesPorFuncion (primer login y cada guardado):
--   si se cambia una, se cambia en los dos lados. «Obra» = proyecto de tipo PROYECTO, igual que
--   ObrasLoader desde el 2026-09-29: un área interna (Post Venta, Arquitectura Comercial), la FFT y el
--   de prueba son oficina central.
--
-- Al final, tres reportes (SELECT): cuántos tiene cada rol, quién PIERDE acceso a una pantalla de
-- Gestión Administrativa y a quién el algoritmo o lo personalizado pone a actuar sin poder entrar
-- (debería salir vacío). Idempotente. Los usuarios ven el cambio en su siguiente refresh de token
-- (≈2 min) o al volver a iniciar sesión.
-- ============================================================================

BEGIN;

-- ── 0) Guardas: si algo no es lo que se asume, no se toca nada ─────────────────
DO $$
DECLARE
    r record;
BEGIN
    FOR r IN
        SELECT * FROM (VALUES (1,  'ADMINISTRADOR DEL SISTEMA'),
                              (5,  'RESIDENTE'),
                              (12, 'USUARIO DE ABRIL'),
                              (52, 'USUARIO DE RECEPCIÓN'),
                              (60, 'ADMINISTRADOR DE OBRA'),
                              (77, 'USUARIO DE GTH'),
                              (81, 'JEFE'),
                              (82, 'GERENTE'),
                              (83, 'TESORERO'),
                              (84, 'COORDINADOR ERP')) AS t(id, nombre)
    LOOP
        IF NOT EXISTS (SELECT 1 FROM role WHERE role_id = r.id AND role_description = r.nombre AND state) THEN
            RAISE EXCEPTION 'El role_id % no es % vigente. Abortado.', r.id, r.nombre;
        END IF;
    END LOOP;

    IF NOT EXISTS (SELECT 1 FROM project_tipo WHERE project_tipo_id = 1 AND codigo = 'PROYECTO') THEN
        RAISE EXCEPTION 'Falta project_tipo PROYECTO (id 1): correr antes 20260928_ProyectosTipoYCicloVida.sql. Abortado.';
    END IF;

    -- 76 y 78 pueden estar ya dados de baja (segunda corrida), pero no pueden ser otra cosa.
    IF EXISTS (SELECT 1 FROM role WHERE role_id = 76 AND role_description <> 'ADMINISTRADOR DE SOLICITUD DE SALIDAS')
       OR EXISTS (SELECT 1 FROM role WHERE role_id = 78 AND role_description <> 'USUARIO REVISOR DE SALIDAS') THEN
        RAISE EXCEPTION 'Los role_id 76/78 no son los roles de salidas que se dan de baja. Abortado.';
    END IF;

    FOR r IN
        SELECT * FROM (VALUES ('gestion-administrativa.solicitud-salidas'),
                              ('gestion-administrativa.rendiciones'),
                              ('gestion-administrativa.gestion-salidas'),
                              ('gestion-administrativa.gestion-rendiciones'),
                              ('gestion-administrativa.consolidados'),
                              ('gestion-administrativa.correcciones-s10'),
                              ('gestion-administrativa.reembolsos'),
                              ('gestion-administrativa.delegacion-revision'),
                              ('gestion-administrativa.config.revisores-areas')) AS t(k)
    LOOP
        IF NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = r.k) THEN
            RAISE EXCEPTION 'No existe la feature %. Abortado.', r.k;
        END IF;
    END LOOP;
END $$;

-- ── 1) Roles nuevos con id fijo ─────────────────────────────────────────────
DO $$
DECLARE
    r       record;
    viejo   integer;
    cuantos integer;
BEGIN
    FOR r IN
        SELECT * FROM (VALUES (94, 'SUB GERENTE'),
                              (95, 'CONSOLIDADOR'),
                              (96, 'COORDINADOR DE ADMINISTRACIÓN DE OBRA')) AS t(id, nombre)
    LOOP
        IF EXISTS (SELECT 1 FROM role WHERE role_id = r.id AND role_description <> r.nombre) THEN
            RAISE EXCEPTION 'El role_id % ya lo usa otro rol (no %). Hay que elegir otro id y cambiarlo en Roles.cs y roles.ts. Abortado.', r.id, r.nombre;
        END IF;

        -- Alguien pudo crearlo desde Seguridad mientras tanto: ese es el que da el sequence.
        SELECT count(*), min(role_id) INTO cuantos, viejo
        FROM role
        WHERE upper(role_description) = r.nombre AND state AND role_id <> r.id;

        IF cuantos > 1 THEN
            RAISE EXCEPTION 'Hay % roles vigentes llamados % fuera del id %. Revisar a mano. Abortado.', cuantos, r.nombre, r.id;
        END IF;

        IF EXISTS (SELECT 1 FROM role WHERE role_id = r.id) THEN
            IF viejo IS NOT NULL THEN
                RAISE EXCEPTION 'El rol % existe como % y como %. Revisar a mano. Abortado.', r.nombre, r.id, viejo;
            END IF;
            CONTINUE;
        END IF;

        IF viejo IS NULL THEN
            INSERT INTO role (role_id, role_description, created_user_id)
            VALUES (r.id, r.nombre, 1);
        ELSE
            INSERT INTO role (role_id, role_description, created_date_time, created_user_id, active, state)
            SELECT r.id, r.nombre, created_date_time, created_user_id, active, state
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

    PERFORM setval('role_role_id_seq',
                   GREATEST((SELECT max(role_id) FROM role), (SELECT last_value FROM role_role_id_seq)));
END $$;

-- ── Foto de ANTES, para el reporte del final (vive hasta cerrar la sesión) ──────
DROP TABLE IF EXISTS _ga_accesos_antes;
CREATE TEMP TABLE _ga_accesos_antes AS
WITH accesos AS (
    SELECT ur.user_id, f.feature_key, r.role_description
    FROM user_role ur
    JOIN role r          ON r.role_id    = ur.role_id AND r.state
    JOIN role_feature rf ON rf.role_id   = ur.role_id
    JOIN feature f       ON f.feature_id = rf.feature_id
    WHERE ur.state
      AND f.feature_key LIKE 'gestion-administrativa.%'
)
SELECT a.user_id, a.feature_key, r.roles_antes
FROM (SELECT DISTINCT user_id, feature_key FROM accesos) a
JOIN (SELECT user_id, string_agg(DISTINCT role_description, ', ') AS roles_antes
      FROM accesos GROUP BY user_id) r ON r.user_id = a.user_id;

-- ── 2) Lo que da cada rol ───────────────────────────────────────────────────
INSERT INTO role_feature (role_id, feature_id)
SELECT x.role_id, f.feature_id
FROM (VALUES
        -- Lo del propio trabajador.
        (12, 'gestion-administrativa.solicitud-salidas'),
        (12, 'gestion-administrativa.rendiciones'),
        -- Las jefaturas y quien coordina a los administradores de obra: las tres bandejas.
        (5,  'gestion-administrativa.gestion-salidas'),
        (5,  'gestion-administrativa.gestion-rendiciones'),
        (5,  'gestion-administrativa.consolidados'),
        (60, 'gestion-administrativa.gestion-salidas'),
        (60, 'gestion-administrativa.gestion-rendiciones'),
        (60, 'gestion-administrativa.consolidados'),
        (81, 'gestion-administrativa.gestion-salidas'),
        (81, 'gestion-administrativa.gestion-rendiciones'),
        (81, 'gestion-administrativa.consolidados'),
        (82, 'gestion-administrativa.gestion-salidas'),
        (82, 'gestion-administrativa.gestion-rendiciones'),
        (82, 'gestion-administrativa.consolidados'),
        (94, 'gestion-administrativa.gestion-salidas'),
        (94, 'gestion-administrativa.gestion-rendiciones'),
        (94, 'gestion-administrativa.consolidados'),
        (96, 'gestion-administrativa.gestion-salidas'),
        (96, 'gestion-administrativa.gestion-rendiciones'),
        (96, 'gestion-administrativa.consolidados'),
        -- El consolidador: arma la planilla grupal, sube el S10 y lo sigue en Consolidados.
        (95, 'gestion-administrativa.gestion-rendiciones'),
        (95, 'gestion-administrativa.consolidados'),
        -- Delegación de Revisión: quien aprueba salidas designa suplentes (la tenía el 76).
        (5,  'gestion-administrativa.delegacion-revision'),
        (81, 'gestion-administrativa.delegacion-revision'),
        (82, 'gestion-administrativa.delegacion-revision'),
        (94, 'gestion-administrativa.delegacion-revision'),
        (96, 'gestion-administrativa.delegacion-revision'),
        -- Revisores de Áreas: el jefe elige a los consolidadores de su área.
        (1,  'gestion-administrativa.config.revisores-areas'),
        (77, 'gestion-administrativa.config.revisores-areas'),
        (81, 'gestion-administrativa.config.revisores-areas'),
        -- Las dos bandejas de un solo rol.
        (83, 'gestion-administrativa.reembolsos'),
        (84, 'gestion-administrativa.correcciones-s10')
     ) AS x(role_id, feature_key)
JOIN feature f ON f.feature_key = x.feature_key
JOIN role r    ON r.role_id     = x.role_id AND r.state
ON CONFLICT (role_id, feature_id) DO NOTHING;

-- La configuración que solo daba el 76 (lugares, motivos, trayectos, capturas, carpeta de adjuntos,
-- visibilidad...) pasa a ADMINISTRADOR DEL SISTEMA para que no quede sin nadie que la administre.
INSERT INTO role_feature (role_id, feature_id)
SELECT 1, rf.feature_id
FROM role_feature rf
JOIN feature f ON f.feature_id = rf.feature_id
WHERE rf.role_id = 76
  AND f.feature_key LIKE 'gestion-administrativa.config.%'
ON CONFLICT (role_id, feature_id) DO NOTHING;

-- ── 3) Baja de 76 y 78 ──────────────────────────────────────────────────────
DO $$
DECLARE
    x record;
BEGIN
    -- Lo que apuntaba a esos roles por id queda sin nadie: se avisa para re-apuntarlo a mano.
    FOR x IN SELECT id, role_id FROM ga_correo_regla WHERE role_id IN (76, 78) AND state LOOP
        RAISE NOTICE 'ga_correo_regla % apunta al rol % que se da de baja: re-apuntarla a mano.', x.id, x.role_id;
    END LOOP;
    FOR x IN SELECT learning_category_id, role_id FROM learning_category_role WHERE role_id IN (76, 78) LOOP
        RAISE NOTICE 'learning_category_role (categoría %) apunta al rol % que se da de baja.', x.learning_category_id, x.role_id;
    END LOOP;
END $$;

DELETE FROM user_role WHERE role_id IN (76, 78);

UPDATE role
   SET state = false, active = false, updated_date_time = now(), updated_user_id = 1
 WHERE role_id IN (76, 78) AND state;

-- ── 4) Roles del puesto (solo agrega) ───────────────────────────────────────
INSERT INTO user_role (user_id, role_id, created_user_id)
SELECT DISTINCT x.user_id, x.role_id, 1
FROM (
    -- Por la categoría del puesto de una ficha viva y adentro (Activo o Inhabilitado SSOMA).
    SELECT p.user_id, m.role_id
    FROM workers w
    JOIN person p  ON p.person_id  = w.person_id
    JOIN puesto pu ON pu.puesto_id = w.puesto_id
    JOIN (VALUES (17, 81),   -- JEFE        → JEFE
                 (29, 94),   -- SUB GERENTE → SUB GERENTE
                 (11, 82),   -- GERENTE     → GERENTE
                 (8,  5),    -- RESIDENTE   → RESIDENTE
                 (46, 83))   -- TESORERO    → TESORERO
         AS m(categoria_id, role_id) ON m.categoria_id = pu.categoria_id
    WHERE w.state AND w.workers_estado_id IN (1, 3)
    UNION
    -- Administra una obra activa: es el caso «Administrador de obra» del algoritmo.
    SELECT p.user_id, 60
    FROM project pr
    JOIN workers w ON w.id = pr.workers_coord_admin_id AND w.state AND w.workers_estado_id IN (1, 3)
    JOIN person p  ON p.person_id = w.person_id
    WHERE pr.state AND pr.active
      AND pr.project_tipo_id = 1                        -- PROYECTO: lo único que es obra
    UNION
    -- Coordina a los administradores de obra (Fiorella Mendoza): ve las obras por su área.
    SELECT p.user_id, 96
    FROM workers w
    JOIN person p  ON p.person_id  = w.person_id
    JOIN puesto pu ON pu.puesto_id = w.puesto_id
    WHERE w.state AND w.workers_estado_id IN (1, 3)
      AND upper(trim(pu.nombre)) = 'COORDINADOR ADMINISTRATIVO DE OBRA'
    UNION
    -- Atiende las correcciones del S10 (hoy nadie tiene el rol y Correcciones S10 no la abre nadie).
    SELECT p.user_id, 84
    FROM workers w
    JOIN person p  ON p.person_id  = w.person_id
    JOIN puesto pu ON pu.puesto_id = w.puesto_id
    WHERE w.state AND w.workers_estado_id IN (1, 3)
      AND upper(trim(pu.nombre)) = 'COORDINADOR ERP'
) x
JOIN app_user u ON u.user_id = x.user_id AND u.state
JOIN role r     ON r.role_id = x.role_id AND r.state
ON CONFLICT (user_id, role_id) DO UPDATE
    SET state = true, active = true, updated_date_time = now(), updated_user_id = 1
    WHERE NOT user_role.state;

-- ── 4b) TESORERO solo para quien es tesorero(a) ─────────────────────────────
DELETE FROM user_role ur
WHERE ur.role_id = 83
  AND NOT EXISTS (
        SELECT 1
        FROM person p
        JOIN workers w  ON w.person_id  = p.person_id AND w.state AND w.workers_estado_id IN (1, 3)
        JOIN puesto pu  ON pu.puesto_id = w.puesto_id
        WHERE p.user_id = ur.user_id
          AND pu.categoria_id = 46);

-- ── 5) Aprobadores a mano que no entran a su bandeja → la jefatura de su puesto ─
WITH designados AS (
    SELECT p.user_id, a.ga_actor_id
    FROM area_actor_asignacion a
    JOIN workers w ON w.id = a.worker_id
    JOIN person p  ON p.person_id = w.person_id
    WHERE a.state AND a.active AND a.ga_actor_id IN (1, 3, 5)
      AND p.user_id IS NOT NULL
      AND lower(trim(w.email_corporativo)) LIKE '%@abril.pe'
    UNION
    SELECT p.user_id, r.ga_actor_id
    FROM workers_actor_asignacion r
    JOIN workers w ON w.id = r.asignado_id
    JOIN person p  ON p.person_id = w.person_id
    WHERE r.state AND r.active AND r.ga_actor_id IN (1, 3, 5)
      AND p.user_id IS NOT NULL
      AND lower(trim(w.email_corporativo)) LIKE '%@abril.pe'
    UNION
    -- El residente de una obra (Configuración → Proyectos) aprueba las salidas de su staff.
    SELECT p.user_id, 1
    FROM project pr
    JOIN workers w ON w.id = pr.residente_workers_id
    JOIN person p  ON p.person_id = w.person_id
    WHERE pr.state
      AND pr.project_tipo_id = 1
      AND p.user_id IS NOT NULL
      AND lower(trim(w.email_corporativo)) LIKE '%@abril.pe'
),
faltan AS (
    SELECT DISTINCT d.user_id
    FROM designados d
    JOIN app_user u ON u.user_id = d.user_id AND u.state
    WHERE NOT EXISTS (
            SELECT 1
            FROM user_role ur
            JOIN role ro         ON ro.role_id   = ur.role_id AND ro.state
            JOIN role_feature rf ON rf.role_id   = ur.role_id
            JOIN feature f       ON f.feature_id = rf.feature_id
            WHERE ur.user_id = d.user_id AND ur.state
              AND f.feature_key = CASE d.ga_actor_id
                                      WHEN 1 THEN 'gestion-administrativa.gestion-salidas'
                                      WHEN 3 THEN 'gestion-administrativa.gestion-rendiciones'
                                      ELSE 'gestion-administrativa.consolidados'
                                  END)
),
rol AS (
    SELECT f.user_id,
           COALESCE((
               SELECT m.role_id
               FROM person p
               JOIN workers w  ON w.person_id  = p.person_id AND w.state AND w.workers_estado_id IN (1, 3)
               JOIN puesto pu  ON pu.puesto_id = w.puesto_id
               JOIN (VALUES (11, 82, 1), (39, 82, 2), (40, 82, 3),   -- las gerencias → GERENTE
                            (29, 94, 4), (17, 81, 5), (8, 5, 6))    -- SUB GERENTE, JEFE, RESIDENTE
                    AS m(categoria_id, role_id, orden) ON m.categoria_id = pu.categoria_id
               WHERE p.user_id = f.user_id
               ORDER BY m.orden
               LIMIT 1), 81) AS role_id                                -- sin jefatura → JEFE
    FROM faltan f
)
INSERT INTO user_role (user_id, role_id, created_user_id)
SELECT rol.user_id, rol.role_id, 1
FROM rol
JOIN role r ON r.role_id = rol.role_id AND r.state
ON CONFLICT (user_id, role_id) DO UPDATE
    SET state = true, active = true, updated_date_time = now(), updated_user_id = 1
    WHERE NOT user_role.state;

-- ── 6) CONSOLIDADOR exacto ──────────────────────────────────────────────────
WITH designados AS (
    SELECT p.user_id
    FROM area_actor_asignacion a
    JOIN workers w ON w.id = a.worker_id
    JOIN person p  ON p.person_id = w.person_id
    WHERE a.state AND a.active AND a.ga_actor_id = 4
      AND p.user_id IS NOT NULL
      AND lower(trim(w.email_corporativo)) LIKE '%@abril.pe'
    UNION
    SELECT p.user_id
    FROM workers_actor_asignacion r
    JOIN workers w ON w.id = r.asignado_id
    JOIN person p  ON p.person_id = w.person_id
    WHERE r.state AND r.active AND r.ga_actor_id = 4
      AND p.user_id IS NOT NULL
      AND lower(trim(w.email_corporativo)) LIKE '%@abril.pe'
),
cubiertos AS (
    SELECT ur.user_id
    FROM user_role ur
    JOIN role ro         ON ro.role_id   = ur.role_id AND ro.state
    JOIN role_feature rf ON rf.role_id   = ur.role_id
    JOIN feature f       ON f.feature_id = rf.feature_id
    WHERE ur.state AND ur.role_id <> 95
      AND f.feature_key IN ('gestion-administrativa.gestion-rendiciones', 'gestion-administrativa.consolidados')
    GROUP BY ur.user_id
    HAVING count(DISTINCT f.feature_key) = 2
),
objetivo AS (
    SELECT DISTINCT d.user_id
    FROM designados d
    JOIN app_user u ON u.user_id = d.user_id AND u.state
    WHERE NOT EXISTS (SELECT 1 FROM cubiertos c WHERE c.user_id = d.user_id)
),
alta AS (
    INSERT INTO user_role (user_id, role_id, created_user_id)
    SELECT o.user_id, 95, 1
    FROM objetivo o
    ON CONFLICT (user_id, role_id) DO UPDATE
        SET state = true, active = true, updated_date_time = now(), updated_user_id = 1
        WHERE NOT user_role.state
    RETURNING user_id
)
DELETE FROM user_role ur
WHERE ur.role_id = 95
  AND NOT EXISTS (SELECT 1 FROM objetivo o WHERE o.user_id = ur.user_id);

COMMIT;

-- ============================================================================
-- Reportes (solo lectura)
-- ============================================================================

-- A) Cuántos usuarios vivos tiene cada rol de Gestión Administrativa.
SELECT r.role_id, r.role_description, r.state AS vigente, count(u.user_id) AS usuarios
FROM role r
LEFT JOIN user_role ur ON ur.role_id = r.role_id AND ur.state
LEFT JOIN app_user u   ON u.user_id  = ur.user_id AND u.state
WHERE r.role_id IN (1, 5, 12, 52, 60, 76, 77, 78, 81, 82, 83, 84, 94, 95, 96)
GROUP BY r.role_id, r.role_description, r.state
ORDER BY r.role_id;

-- B) Quién PIERDE una pantalla de Gestión Administrativa (tenía la feature y ya no). Lo esperado:
--    gente que tenía 76/78 sin ser jefatura ni actor. Si alguien de acá sí debe entrar, darle el
--    rol de su función desde Seguridad.
WITH ahora AS (
    SELECT DISTINCT ur.user_id, f.feature_key
    FROM user_role ur
    JOIN role r          ON r.role_id    = ur.role_id AND r.state
    JOIN role_feature rf ON rf.role_id   = ur.role_id
    JOIN feature f       ON f.feature_id = rf.feature_id
    WHERE ur.state AND f.feature_key LIKE 'gestion-administrativa.%'
)
SELECT u.email,
       (SELECT string_agg(DISTINCT c.nombre, ' / ')
          FROM person p JOIN workers w ON w.person_id = p.person_id AND w.state AND w.workers_estado_id IN (1, 3)
          JOIN puesto pu ON pu.puesto_id = w.puesto_id JOIN categoria c ON c.categoria_id = pu.categoria_id
         WHERE p.user_id = u.user_id) AS categoria,
       string_agg(replace(a.feature_key, 'gestion-administrativa.', ''), ', ' ORDER BY a.feature_key) AS pierde,
       max(a.roles_antes) AS roles_antes
FROM _ga_accesos_antes a
JOIN app_user u ON u.user_id = a.user_id AND u.state
WHERE NOT EXISTS (SELECT 1 FROM ahora n WHERE n.user_id = a.user_id AND n.feature_key = a.feature_key)
GROUP BY u.user_id, u.email
ORDER BY u.email;

-- C) Quién tiene que actuar y no puede entrar a la bandeja (debería salir vacío). «sin usuario» =
--    todavía no inició sesión nunca: recibe sus roles en su primer login.
WITH necesita AS (
    -- Jefaturas de su área (las que el algoritmo pone a aprobar, revisar, firmar o consolidar).
    SELECT w.person_id, f.k AS feature_key, 'jefatura (' || c.nombre || ')' AS por
    FROM workers w
    JOIN puesto pu    ON pu.puesto_id    = w.puesto_id
    JOIN categoria c  ON c.categoria_id  = pu.categoria_id
    CROSS JOIN (VALUES ('gestion-administrativa.gestion-salidas'),
                       ('gestion-administrativa.gestion-rendiciones'),
                       ('gestion-administrativa.consolidados')) AS f(k)
    WHERE w.state AND w.workers_estado_id IN (1, 3)
      AND pu.categoria_id IN (8, 11, 17, 29)
      AND lower(trim(w.email_corporativo)) LIKE '%@abril.pe'
    UNION
    -- Residente y administrador de cada obra.
    SELECT w.person_id, f.k, CASE WHEN w.id = pr.residente_workers_id THEN 'residente de ' ELSE 'administrador de ' END || trim(pr.project_description)
    FROM project pr
    JOIN workers w ON w.id IN (pr.residente_workers_id, pr.workers_coord_admin_id)
                  AND w.state AND w.workers_estado_id IN (1, 3)
    CROSS JOIN (VALUES ('gestion-administrativa.gestion-rendiciones'),
                       ('gestion-administrativa.consolidados')) AS f(k)
    WHERE pr.state AND pr.project_tipo_id = 1
      AND (w.id = pr.residente_workers_id OR pr.active)
      AND lower(trim(w.email_corporativo)) LIKE '%@abril.pe'
    UNION
    SELECT w.person_id, 'gestion-administrativa.gestion-salidas', 'residente de ' || trim(pr.project_description)
    FROM project pr
    JOIN workers w ON w.id = pr.residente_workers_id AND w.state AND w.workers_estado_id IN (1, 3)
    WHERE pr.state AND pr.project_tipo_id = 1
      AND lower(trim(w.email_corporativo)) LIKE '%@abril.pe'
    UNION
    -- Lo asignado a mano.
    SELECT w.person_id, f.k, 'asignado a mano: ' || ga.nombre
    FROM (SELECT worker_id AS w_id, ga_actor_id FROM area_actor_asignacion WHERE state AND active
          UNION
          SELECT asignado_id, ga_actor_id FROM workers_actor_asignacion WHERE state AND active) d
    JOIN workers w  ON w.id = d.w_id
    JOIN ga_actor ga ON ga.ga_actor_id = d.ga_actor_id
    JOIN LATERAL (SELECT unnest(CASE d.ga_actor_id
                                    WHEN 1 THEN ARRAY['gestion-administrativa.gestion-salidas']
                                    WHEN 3 THEN ARRAY['gestion-administrativa.gestion-rendiciones']
                                    WHEN 4 THEN ARRAY['gestion-administrativa.gestion-rendiciones', 'gestion-administrativa.consolidados']
                                    WHEN 5 THEN ARRAY['gestion-administrativa.consolidados']
                                    ELSE ARRAY[]::text[]
                                END) AS k) f ON true
    WHERE lower(trim(w.email_corporativo)) LIKE '%@abril.pe'
),
tiene AS (
    SELECT DISTINCT ur.user_id, f.feature_key
    FROM user_role ur
    JOIN role r          ON r.role_id    = ur.role_id AND r.state
    JOIN role_feature rf ON rf.role_id   = ur.role_id
    JOIN feature f       ON f.feature_id = rf.feature_id
    WHERE ur.state
)
SELECT p.full_name,
       coalesce(u.email, 'sin usuario') AS usuario,
       string_agg(DISTINCT replace(n.feature_key, 'gestion-administrativa.', ''), ', ') AS le_falta,
       string_agg(DISTINCT n.por, '; ') AS por
FROM necesita n
JOIN person p        ON p.person_id = n.person_id
LEFT JOIN app_user u ON u.user_id   = p.user_id AND u.state
WHERE u.user_id IS NULL
   OR NOT EXISTS (SELECT 1 FROM tiene t WHERE t.user_id = u.user_id AND t.feature_key = n.feature_key)
GROUP BY p.person_id, p.full_name, u.email
ORDER BY (u.email IS NULL), p.full_name;
