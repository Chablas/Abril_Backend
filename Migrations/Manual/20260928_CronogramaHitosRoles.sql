-- ============================================================================
-- Mejora Continua · Cronograma de Hitos — Roles que administran el cronograma
-- PASO 1 de 2 · ANTES DE DESPLEGAR EL BACKEND Y EL FRONTEND
-- Fecha: 2026-09-28
--
-- El cronograma queda con tres features:
--   · mejora-continua.milestone-schedule              → VER: listado, historial y Gantt.
--   · mejora-continua.milestone-schedule.editar       → RESIDENTE: sube versiones nuevas y
--     modifica el cronograma, solo del proyecto donde es el residente en Configuración →
--     Proyectos → Emails SSOMA (el código exige además el rol RESIDENTE).
--   · mejora-continua.milestone-schedule.administrar  → NUEVA: eliminar versiones, editar y
--     agregar hitos de una versión guardada, culminar, marcar críticos, foto y característica,
--     en cualquier proyecto. No sube versiones: eso es solo del residente del proyecto.
-- Quien tiene solo la de ver usa la pantalla en solo lectura (ve los botones deshabilitados).
-- Las features se suman entre los roles del usuario: USUARIO DE ABRIL + RESIDENTE edita su
-- proyecto.
--
-- Este paso:
--   1) Crea la feature de administrar (y la de editar, si faltara).
--   2) Crea COORDINADOR DE PROYECTOS, JEFE DE PROYECTOS y GERENTE INMOBILIARIO con ver +
--      administrar. RESIDENTE ya existe: conserva ver + editar y a sus usuarios.
--   3) Asigna Victor Colonio → COORDINADOR DE PROYECTOS, Hivet Mamani → JEFE DE PROYECTOS y
--      Carlos Oriundo → GERENTE INMOBILIARIO.
--   4) Solo lectura: USUARIO DE ABRIL, ESPECIALISTA EN BUSINESS INTELLIGENCE y ESPECIALISTA EN
--      TRANSFORMACIÓN DIGITAL tienen la de ver y ninguna de las otras dos.
-- Con el código viejo nada de esto cambia permisos (la feature nueva no la mira nadie). Lo que
-- se quita (ADMINISTRADOR DE UDP y ADMINISTRADOR DE RESIDENTES) va en el paso 2, después del
-- deploy.
--
-- Los role_id nuevos no se fijan: los da el sequence y el código no los nombra (todo se decide
-- por feature). Los usuarios se buscan por correo. Idempotente. Aplicar en dev y prod.
-- ============================================================================

BEGIN;

-- Guardas: si falta algo de lo que se asume, no se toca nada.
DO $$
DECLARE
    faltan text;
BEGIN
    SELECT string_agg(e.email, ', ')
    INTO faltan
    FROM (VALUES ('vcolonio@abril.pe'), ('hmamani@abril.pe'), ('coriundo@abril.pe')) AS e(email)
    WHERE NOT EXISTS (SELECT 1 FROM app_user u WHERE lower(u.email) = e.email AND u.state);

    IF faltan IS NOT NULL THEN
        RAISE EXCEPTION 'No hay un usuario vigente con el correo: %. Abortado.', faltan;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'mejora-continua.milestone-schedule') THEN
        RAISE EXCEPTION 'No existe la feature mejora-continua.milestone-schedule. Abortado.';
    END IF;

    -- El código compara el rol RESIDENTE por su ID (Roles.Residente = "5").
    IF NOT EXISTS (SELECT 1 FROM role WHERE role_id = 5 AND role_description = 'RESIDENTE' AND state) THEN
        RAISE EXCEPTION 'El role_id 5 no es RESIDENTE vigente. Abortado.';
    END IF;
END $$;

-- 1) Features, en el mismo módulo que la de ver (leído de la fila).
INSERT INTO feature (feature_key, module_id)
SELECT k.feature_key, ver.module_id
FROM (VALUES ('mejora-continua.milestone-schedule.editar'),
             ('mejora-continua.milestone-schedule.administrar')) AS k(feature_key)
CROSS JOIN feature ver
WHERE ver.feature_key = 'mejora-continua.milestone-schedule'
ON CONFLICT (feature_key) DO NOTHING;

-- RESIDENTE conserva ver + editar (ya las tiene: esto solo lo asegura).
INSERT INTO role_feature (role_id, feature_id)
SELECT 5, f.feature_id
FROM feature f
WHERE f.feature_key IN ('mejora-continua.milestone-schedule', 'mejora-continua.milestone-schedule.editar')
ON CONFLICT (role_id, feature_id) DO NOTHING;

-- 2) Los tres roles nuevos, con el sequence normal...
INSERT INTO role (role_description, created_user_id)
SELECT n.role_description, 1
FROM (VALUES ('COORDINADOR DE PROYECTOS'),
             ('JEFE DE PROYECTOS'),
             ('GERENTE INMOBILIARIO')) AS n(role_description)
WHERE NOT EXISTS (SELECT 1 FROM role r WHERE r.role_description = n.role_description AND r.state);

-- ... con ver + administrar.
INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM role r
CROSS JOIN feature f
WHERE r.role_description IN ('COORDINADOR DE PROYECTOS', 'JEFE DE PROYECTOS', 'GERENTE INMOBILIARIO')
  AND r.state
  AND f.feature_key IN ('mejora-continua.milestone-schedule', 'mejora-continua.milestone-schedule.administrar')
ON CONFLICT (role_id, feature_id) DO NOTHING;

-- 3) Asignaciones. Si alguno lo tuvo y se le quitó, la fila se revive.
INSERT INTO user_role (user_id, role_id, created_user_id)
SELECT u.user_id, r.role_id, 1
FROM (VALUES ('vcolonio@abril.pe', 'COORDINADOR DE PROYECTOS'),
             ('hmamani@abril.pe',  'JEFE DE PROYECTOS'),
             ('coriundo@abril.pe', 'GERENTE INMOBILIARIO')) AS a(email, role_description)
JOIN app_user u ON lower(u.email) = a.email AND u.state
JOIN role r     ON r.role_description = a.role_description AND r.state
ON CONFLICT (user_id, role_id) DO UPDATE
   SET state             = true,
       active            = true,
       updated_date_time = now(),
       updated_user_id   = 1
 WHERE NOT user_role.state OR NOT user_role.active;

-- 4) Solo lectura: ven, y no editan ni administran. El LIKE de TRANSFORMACI%N es para no
--    depender de cómo quedó guardada la tilde.
INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM role r
CROSS JOIN feature f
WHERE r.state
  AND (r.role_description IN ('USUARIO DE ABRIL', 'ESPECIALISTA EN BUSINESS INTELLIGENCE')
       OR r.role_description LIKE 'ESPECIALISTA EN TRANSFORMACI%N DIGITAL')
  AND f.feature_key = 'mejora-continua.milestone-schedule'
ON CONFLICT (role_id, feature_id) DO NOTHING;

DELETE FROM role_feature rf
USING role r, feature f
WHERE rf.role_id = r.role_id
  AND rf.feature_id = f.feature_id
  AND (r.role_description IN ('USUARIO DE ABRIL', 'ESPECIALISTA EN BUSINESS INTELLIGENCE')
       OR r.role_description LIKE 'ESPECIALISTA EN TRANSFORMACI%N DIGITAL')
  AND f.feature_key IN ('mejora-continua.milestone-schedule.editar', 'mejora-continua.milestone-schedule.administrar');

COMMIT;

-- ── Verificación ────────────────────────────────────────────────────────────
-- a) Qué roles tienen cada feature del cronograma. Tras este paso: administrar solo los tres
--    roles nuevos; editar RESIDENTE (y ADMINISTRADOR DE RESIDENTES hasta el paso 2); ver, además,
--    USUARIO DE ABRIL y los dos especialistas.
-- SELECT f.feature_key, r.role_id, r.role_description
-- FROM feature f
-- JOIN role_feature rf ON rf.feature_id = f.feature_id
-- JOIN role r ON r.role_id = rf.role_id
-- WHERE f.feature_key LIKE 'mejora-continua.milestone-schedule%'
-- ORDER BY 1, 2;
--
-- b) Los roles nuevos y sus usuarios:
-- SELECT r.role_id, r.role_description, u.user_id, u.email
-- FROM role r
-- LEFT JOIN user_role ur ON ur.role_id = r.role_id AND ur.state
-- LEFT JOIN app_user u ON u.user_id = ur.user_id
-- WHERE r.role_description IN ('COORDINADOR DE PROYECTOS', 'JEFE DE PROYECTOS', 'GERENTE INMOBILIARIO')
-- ORDER BY r.role_id, u.email;
