-- ============================================================================
-- Rol RESIDENTE solo para los residentes de obra
-- ANTES DE DESPLEGAR EL BACKEND Y EL FRONTEND (no depende del código: también corre después)
-- Fecha: 2026-09-28 · Paso 2 de PLAN-RESIDENTES.md
--
-- En prod el rol RESIDENTE (5) lo tienen 13 usuarios: los 8 Ingenieros Residentes y 5 que no son
-- residentes de ninguna obra:
--   · Christian Alvarez (calvarez) y Samuel Daniel Justiniani (sjustiniani): no son residentes.
--   · Carlos Oriundo, Hivet Mamani y Victor Colonio: gestionan a los residentes y todos los
--     proyectos. Lo que el rol RESIDENTE les daba lo pasan a dar sus roles propios:
--     GERENTE INMOBILIARIO (92), JEFE DE PROYECTOS (93) y COORDINADOR DE PROYECTOS (91).
--
-- Este paso:
--   1) Copia a COORDINADOR DE PROYECTOS, GERENTE INMOBILIARIO y JEFE DE PROYECTOS todas las
--      funcionalidades del rol RESIDENTE, menos mejora-continua.milestone-schedule.editar (subir
--      versiones del Cronograma de Hitos es solo del residente del proyecto; esos tres roles ya
--      lo administran). En prod son 18: Cursos, Evaluaciones (períodos, evaluar staff y sus
--      resultados), Lecciones, ATS, Horas hombre, Penalidades (lista y aprobar como residente),
--      Presupuesto de materiales, RAC (lista, detalle, dashboard y cerrar), Vecinos (gestión,
--      dashboard, Control de Licencias) y ver el Cronograma de Hitos.
--   2) Asegura que Victor tenga COORDINADOR DE PROYECTOS, Carlos GERENTE INMOBILIARIO y Hivet
--      JEFE DE PROYECTOS (ya lo hace 20260928_CronogramaHitosRoles.sql; acá se repite por si
--      este corre en otro orden).
--   3) Les quita el rol RESIDENTE a los cinco. Con DELETE, igual que Seguridad → Usuarios al
--      guardar: el login arma los roles del token con todas las filas de user_role sin mirar
--      state, así que una fila con state = false seguiría diciendo RESIDENTE en el token.
--   4) Da de baja las filas de project_resident de esos cinco: en prod, la fila de prueba de
--      calvarez en TORRE ABRIL (2026-03-23). Torre Abril sale del Cronograma de Hitos, que queda
--      con las 8 obras en ejecución, todas con su residente.
--
-- Qué cambia para ellos (prod 2026-09-28):
--   · Carlos, Hivet y Victor no pierden ninguna funcionalidad (Hivet tenía Control de Licencias
--     solo por RESIDENTE: ahora se lo da JEFE DE PROYECTOS). Dejan de aparecer en las listas de
--     residentes (IVTs, Cuaderno de obra, Seguimiento de residentes) y Control de respuesta de
--     informes deja de recortarles la lista (como RESIDENTE sin obra asignada no veían nada).
--   · calvarez y sjustiniani pierden las tres funcionalidades que solo les daba RESIDENTE:
--     evaluaciones.evaluar-staff, evaluaciones.resultados-staff y
--     ssoma.gestion.penalidades.aprobar-residente.
--   · Los residentes de Configuración → Proyectos NO cambian: Samuel sigue de residente de
--     Oficina Central y Hivet de Baronet para los correos de SSOMA; sin el rol no ganan permisos
--     de residente.
--
-- Requiere 20260928_ProyectosRolesYHistorialResidente.sql (roles 91, 92 y 93): aborta si falta.
-- Idempotente. Los cinco lo ven al volver a iniciar sesión (los roles viajan en el token).
-- ============================================================================

BEGIN;

-- Guardas: si falta algo de lo que se asume, no se toca nada.
DO $$
DECLARE
    faltan text;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM role WHERE role_id = 5 AND role_description = 'RESIDENTE' AND state) THEN
        RAISE EXCEPTION 'El role_id 5 no es RESIDENTE vigente. Abortado.';
    END IF;

    SELECT string_agg(e.id || ' ' || e.nombre, ', ')
    INTO faltan
    FROM (VALUES (91, 'COORDINADOR DE PROYECTOS'),
                 (92, 'GERENTE INMOBILIARIO'),
                 (93, 'JEFE DE PROYECTOS')) AS e(id, nombre)
    WHERE NOT EXISTS (SELECT 1 FROM role r WHERE r.role_id = e.id AND r.role_description = e.nombre AND r.state);

    IF faltan IS NOT NULL THEN
        RAISE EXCEPTION 'Faltan los roles % (correr antes 20260928_ProyectosRolesYHistorialResidente.sql). Abortado.', faltan;
    END IF;

    SELECT string_agg(e.email, ', ')
    INTO faltan
    FROM (VALUES ('calvarez@abril.pe'), ('sjustiniani@abril.pe'), ('coriundo@abril.pe'),
                 ('hmamani@abril.pe'), ('vcolonio@abril.pe')) AS e(email)
    WHERE NOT EXISTS (SELECT 1 FROM app_user u WHERE lower(u.email) = e.email AND u.state);

    IF faltan IS NOT NULL THEN
        RAISE EXCEPTION 'No hay un usuario vigente con el correo: %. Abortado.', faltan;
    END IF;
END $$;

-- 1) Lo que daba RESIDENTE, ahora también en los tres roles que gestionan proyectos.
INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, rf.feature_id
FROM role_feature rf
JOIN feature f ON f.feature_id = rf.feature_id
CROSS JOIN (VALUES (91), (92), (93)) AS r(role_id)
WHERE rf.role_id = 5
  AND f.feature_key <> 'mejora-continua.milestone-schedule.editar'
ON CONFLICT (role_id, feature_id) DO NOTHING;

-- 2) Cada uno con su rol. Si lo tuvo y se le quitó, la fila se revive.
INSERT INTO user_role (user_id, role_id, created_user_id)
SELECT u.user_id, a.role_id, 1
FROM (VALUES ('vcolonio@abril.pe', 91),
             ('coriundo@abril.pe', 92),
             ('hmamani@abril.pe',  93)) AS a(email, role_id)
JOIN app_user u ON lower(u.email) = a.email AND u.state
ON CONFLICT (user_id, role_id) DO UPDATE
   SET state             = true,
       active            = true,
       updated_date_time = now(),
       updated_user_id   = 1
 WHERE NOT user_role.state OR NOT user_role.active;

-- 3) Sin el rol RESIDENTE.
DELETE FROM user_role ur
USING app_user u
WHERE u.user_id = ur.user_id
  AND ur.role_id = 5
  AND lower(u.email) IN ('calvarez@abril.pe', 'sjustiniani@abril.pe', 'coriundo@abril.pe',
                         'hmamani@abril.pe', 'vcolonio@abril.pe');

-- 4) Sus filas en la tabla vieja de residentes (en prod, la de prueba de calvarez en Torre Abril).
--    updated_date_time es timestamp sin zona y la tabla guarda UTC (ver su default).
UPDATE project_resident pr
   SET state = false, active = false, updated_date_time = now() AT TIME ZONE 'UTC', updated_user_id = 1
  FROM app_user u
 WHERE u.user_id = pr.user_id
   AND lower(u.email) IN ('calvarez@abril.pe', 'sjustiniani@abril.pe', 'coriundo@abril.pe',
                          'hmamani@abril.pe', 'vcolonio@abril.pe')
   AND pr.state;

COMMIT;

-- ── Verificación ────────────────────────────────────────────────────────────
-- a) Quién tiene el rol RESIDENTE (deben quedar solo los 8 Ingenieros Residentes):
-- SELECT u.email, pe.full_name
-- FROM user_role ur
-- JOIN app_user u ON u.user_id = ur.user_id AND u.state
-- LEFT JOIN person pe ON pe.user_id = u.user_id
-- WHERE ur.role_id = 5 AND ur.state
-- ORDER BY 1;
--
-- b) Funcionalidades de RESIDENTE que les faltan a 91, 92 y 93 (debe salir solo .editar, 3 filas):
-- SELECT r.role_id, f.feature_key
-- FROM role_feature rf5
-- JOIN feature f ON f.feature_id = rf5.feature_id
-- CROSS JOIN (VALUES (91), (92), (93)) AS r(role_id)
-- WHERE rf5.role_id = 5
--   AND NOT EXISTS (SELECT 1 FROM role_feature x WHERE x.role_id = r.role_id AND x.feature_id = rf5.feature_id)
-- ORDER BY 1, 2;
--
-- c) La tabla vieja sin filas de ellos (debe dar 0) y el Cronograma de Hitos con 8 obras:
-- SELECT count(*) FROM project_resident pr JOIN app_user u ON u.user_id = pr.user_id
-- WHERE pr.state AND lower(u.email) IN ('calvarez@abril.pe', 'sjustiniani@abril.pe',
--       'coriundo@abril.pe', 'hmamani@abril.pe', 'vcolonio@abril.pe');
