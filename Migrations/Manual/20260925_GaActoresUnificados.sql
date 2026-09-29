-- ============================================================================
-- Gestión Administrativa — Revisores de Áreas unificado (los cinco actores).
-- PASO 1 de 2 · ANTES DE DESPLEGAR EL BACKEND Y EL FRONTEND
-- Fecha: 2026-09-25
--
-- Qué cambia y por qué
--
-- Hasta hoy "quién hace qué" sobre la salida de un trabajador vivía en cuatro
-- tablas que se configuraban en tres pantallas distintas:
--
--   • area_revisores            (Solicitud de Salidas → Configuración)  → aprobar la salida
--   • area_revisores_rendicion  (Mis Rendiciones → Configuración)       → 1.ª revisión y firma
--   • area_consolidadores       (Consolidados → Configuración)          → consolidar
--   • workers_revisores         (ficha del trabajador, "Jefe personalizado")
--
-- Ahora son CINCO actores —aprobador de la salida, jefe notificado de la salida,
-- aprobador de la 1.ª revisión, consolidadores y aprobadores del consolidado—,
-- cada uno resuelto por TIPO de trabajador (oficina central, staff, administrador
-- de obra, jefe, residente, subgerente), en UNA pantalla (Gestión Administrativa →
-- Configuración → Revisores de Áreas) y en la ficha del trabajador:
--
--   • ga_actor, ga_actor_caso     catálogos (ids fijos: ActorIds / ActorCasoIds).
--   • area_actor_asignacion       lo personalizado por área (y obra), por caso y actor.
--   • workers_actor_asignacion    lo personalizado por trabajador, por actor.
--
-- "Filtrar por proyecto" deja de ser una casilla: la pantalla lo deduce de
-- dónde trabaja la gente del área, y el algoritmo ya no lo mira (decide la obra
-- vigente de cada trabajador). La columna se bota en el PASO 2.
--
-- Qué se migra
--
--   • area_revisores, nivel de área        → aprobador de la salida, OFICINA CENTRAL
--                                            + jefe notificado, STAFF (el aviso al jefe
--                                              del área salía de estas mismas filas).
--   • area_revisores, por proyecto         → aprobador de la salida, STAFF (obra) u
--                                            OFICINA CENTRAL (si el proyecto es esa).
--                                            Si el área NO filtraba por proyecto esas
--                                            filas no se aplicaban: pasan INACTIVAS.
--   • area_revisores_rendicion             → 1.ª revisión (aprueba_primera_revision) y
--                                            aprobadores del consolidado (aprueba_consolidado),
--                                            mismo reparto de casos.
--   • area_consolidadores                  → consolidadores, mismo reparto de casos.
--                                            Un área sin consolidadores propios consolidaba
--                                            con sus revisores de rendición: esos pasan
--                                            también como consolidadores.
--   • workers_revisores                    → aprobador de la salida, de la 1.ª revisión
--                                            y del consolidado del trabajador (sus
--                                            documentos ya los decidía el jefe
--                                            personalizado que compartían todos).
--
--   De area_revisores NO se copia lo que el algoritmo ya resuelve igual: un área (o
--   una obra) con UNA sola fila, activa, que es exactamente la jefatura que el
--   algoritmo le deduce (el primer SUB GERENTE / JEFE / RESIDENTE del nodo, o el
--   GERENTE en una gerencia; en una obra, su residente). Esas filas se cargaron a mano
--   antes de que existiera el algoritmo; copiarlas como "personalizado" congelaría a
--   esa persona aunque cambie la jefatura del área.
--
-- ORDEN DE EJECUCIÓN
--   • ANTES de desplegar: el backend nuevo lee las tablas nuevas.
--   • Correrlo INMEDIATAMENTE antes del deploy: lo que se cambie en las pantallas
--     viejas entre este script y el deploy no se copia (la migración corre una sola
--     vez, con las tablas nuevas vacías).
--   • Las tablas viejas NO se tocan: se botan en el PASO 2, después del deploy.
--
-- Re-ejecutable: IF NOT EXISTS en todo y la copia solo corre con la tabla vacía.
-- ============================================================================

SET client_encoding TO 'UTF8';

BEGIN;

-- ── 1) Catálogos ────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS ga_actor (
    ga_actor_id    integer PRIMARY KEY,
    codigo         varchar(40) NOT NULL,
    nombre         varchar(80) NOT NULL,
    multiple       boolean     NOT NULL DEFAULT false,
    display_order  integer     NOT NULL,
    state          boolean     NOT NULL DEFAULT true
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_ga_actor_codigo ON ga_actor (codigo) WHERE state;

INSERT INTO ga_actor (ga_actor_id, codigo, nombre, multiple, display_order) VALUES
    (1, 'APROBADOR_SALIDA',           'Aprobador de la salida',        false, 1),
    (2, 'JEFE_NOTIFICADO',            'Jefe notificado de la salida',  false, 2),
    (3, 'APROBADOR_PRIMERA_REVISION', 'Aprobador de la 1.ª revisión',  false, 3),
    (4, 'CONSOLIDADOR',               'Consolidadores',                true,  4),
    (5, 'APROBADOR_CONSOLIDADO',      'Aprobadores del consolidado',   true,  5)
ON CONFLICT (ga_actor_id) DO NOTHING;

COMMENT ON TABLE ga_actor IS
    'Los cinco papeles sobre el ciclo de una salida. Ids fijos (ActorIds). multiple = cuentan todos '
    'los activos; si no, solo el primero activo.';

CREATE TABLE IF NOT EXISTS ga_actor_caso (
    ga_actor_caso_id  integer PRIMARY KEY,
    codigo            varchar(40) NOT NULL,
    nombre            varchar(80) NOT NULL,
    display_order     integer     NOT NULL,
    state             boolean     NOT NULL DEFAULT true
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_ga_actor_caso_codigo ON ga_actor_caso (codigo) WHERE state;

INSERT INTO ga_actor_caso (ga_actor_caso_id, codigo, nombre, display_order) VALUES
    (1, 'OFICINA_CENTRAL',    'Oficina central',       1),
    (2, 'STAFF',              'Staff',                 2),
    (6, 'ADMINISTRADOR_OBRA', 'Administrador de obra', 3),
    (3, 'JEFE',               'Jefe',                  4),
    (4, 'RESIDENTE',          'Residente',             5),
    (5, 'SUBGERENTE',         'Subgerente',            6)
ON CONFLICT (ga_actor_caso_id) DO NOTHING;

-- El administrador de obra (id 6) llegó después: se muestra detrás de Staff.
UPDATE ga_actor_caso c SET display_order = v.orden
FROM (VALUES (1, 1), (2, 2), (6, 3), (3, 4), (4, 5), (5, 6)) AS v(id, orden)
WHERE c.ga_actor_caso_id = v.id AND c.display_order <> v.orden;

COMMENT ON TABLE ga_actor_caso IS
    'Tipo de trabajador para el que se resuelven los actores. Ids fijos (ActorCasoIds).';

-- ── 2) Lo personalizado por área ────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS area_actor_asignacion (
    area_actor_asignacion_id  integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    area_scope_id             integer NOT NULL REFERENCES area_scope(area_scope_id),
    project_id                integer          REFERENCES project(project_id),
    ga_actor_id               integer NOT NULL REFERENCES ga_actor(ga_actor_id),
    ga_actor_caso_id          integer NOT NULL REFERENCES ga_actor_caso(ga_actor_caso_id),
    worker_id                 integer NOT NULL REFERENCES workers(id),
    orden_prioridad           integer NOT NULL DEFAULT 1,
    active                    boolean NOT NULL DEFAULT true,
    state                     boolean NOT NULL DEFAULT true,
    created_at                timestamptz NOT NULL DEFAULT now(),
    updated_at                timestamptz,
    CONSTRAINT chk_area_actor_asignacion_prioridad CHECK (orden_prioridad >= 1)
);

CREATE INDEX IF NOT EXISTS ix_area_actor_asignacion_area
    ON area_actor_asignacion (area_scope_id) WHERE state;

CREATE INDEX IF NOT EXISTS ix_area_actor_asignacion_worker
    ON area_actor_asignacion (worker_id) WHERE state;

-- Una persona una sola vez por (área, obra, actor, caso) mientras esté viva. NULLS NOT DISTINCT
-- para que el nivel de área (project_id NULL) tampoco se duplique.
CREATE UNIQUE INDEX IF NOT EXISTS ux_area_actor_asignacion_vivo
    ON area_actor_asignacion (area_scope_id, project_id, ga_actor_id, ga_actor_caso_id, worker_id)
    NULLS NOT DISTINCT WHERE state;

COMMENT ON TABLE area_actor_asignacion IS
    'Lo personalizado por area (Gestion Administrativa > Configuracion > Revisores de Areas): quien '
    'cumple un actor para un caso de trabajador del area, o solo de una obra del area (project_id).';

-- ── 3) Lo personalizado por trabajador ──────────────────────────────────────

CREATE TABLE IF NOT EXISTS workers_actor_asignacion (
    workers_actor_asignacion_id  integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    worker_id                    integer NOT NULL REFERENCES workers(id),
    ga_actor_id                  integer NOT NULL REFERENCES ga_actor(ga_actor_id),
    asignado_id                  integer NOT NULL REFERENCES workers(id),
    orden_prioridad              integer NOT NULL DEFAULT 1,
    active                       boolean NOT NULL DEFAULT true,
    state                        boolean NOT NULL DEFAULT true,
    created_at                   timestamptz NOT NULL DEFAULT now(),
    updated_at                   timestamptz,
    CONSTRAINT chk_workers_actor_asignacion_prioridad CHECK (orden_prioridad >= 1)
);

CREATE INDEX IF NOT EXISTS ix_workers_actor_asignacion_worker
    ON workers_actor_asignacion (worker_id) WHERE state;

CREATE INDEX IF NOT EXISTS ix_workers_actor_asignacion_asignado
    ON workers_actor_asignacion (asignado_id) WHERE state;

CREATE UNIQUE INDEX IF NOT EXISTS ux_workers_actor_asignacion_vivo
    ON workers_actor_asignacion (worker_id, ga_actor_id, asignado_id) WHERE state;

COMMENT ON TABLE workers_actor_asignacion IS
    'Lo personalizado por trabajador (ficha del trabajador): quien cumple un actor sobre ESE '
    'trabajador. Le gana a lo personalizado por area y al algoritmo.';

-- ── 4) Migración de lo cargado en las tablas viejas ─────────────────────────

-- La jefatura que el algoritmo deduce para cada nodo: el GERENTE en un "Área de Gerencia"
-- (area_type_id 1); el SUB GERENTE, si no el JEFE y si no el RESIDENTE en cualquier otro nodo;
-- entre iguales, la ficha más antigua. Solo fichas vivas, de gente que está adentro
-- (ACTIVO o INHABILITADO SSOMA) y con correo corporativo: lo mismo que filtra el backend.
CREATE TEMP TABLE _jefatura_nodo ON COMMIT DROP AS
SELECT DISTINCT ON (pu.area_destino_scope_id)
       pu.area_destino_scope_id AS area_scope_id,
       w.id                     AS worker_id,
       w.person_id
FROM workers w
JOIN puesto pu      ON pu.puesto_id = w.puesto_id
JOIN area_scope s   ON s.area_scope_id = pu.area_destino_scope_id AND s.state
JOIN area_item ai   ON ai.area_item_id = s.area_item_id
WHERE w.state
  AND w.workers_estado_id IN (1, 3)
  AND lower(btrim(coalesce(w.email_corporativo, ''))) LIKE '%@abril.pe'
  AND CASE WHEN ai.area_type_id = 1 THEN pu.categoria_id = 11
           ELSE pu.categoria_id IN (29, 17, 8) END
ORDER BY pu.area_destino_scope_id,
         CASE pu.categoria_id WHEN 11 THEN -1 WHEN 29 THEN 0 WHEN 17 THEN 1 WHEN 8 THEN 2 END,
         w.id;

-- El residente de cada obra (OFICINA CENTRAL no es obra), con el mismo filtro de ficha.
CREATE TEMP TABLE _residente_obra ON COMMIT DROP AS
SELECT p.project_id, w.id AS worker_id, w.person_id
FROM project p
JOIN workers w ON w.id = p.residente_workers_id
WHERE p.state
  AND upper(btrim(p.project_description)) <> 'OFICINA CENTRAL'
  AND w.state
  AND w.workers_estado_id IN (1, 3)
  AND lower(btrim(coalesce(w.email_corporativo, ''))) LIKE '%@abril.pe';

-- Proyectos que son OFICINA CENTRAL (una fila de project sin bandera: se reconoce por nombre).
CREATE TEMP TABLE _oficina_central ON COMMIT DROP AS
SELECT project_id FROM project WHERE upper(btrim(project_description)) = 'OFICINA CENTRAL';

-- Áreas que filtraban por proyecto: solo ahí se aplicaban las filas por proyecto.
CREATE TEMP TABLE _filtraban ON COMMIT DROP AS
SELECT DISTINCT area_scope_id FROM ga_salidas_area_config WHERE state AND filtra_por_proyecto;

DO $$
DECLARE
    n integer;
BEGIN
    IF EXISTS (SELECT 1 FROM area_actor_asignacion) THEN
        RAISE NOTICE 'area_actor_asignacion ya tiene filas: no se vuelve a migrar.';
    ELSE
        -- Grupos (área, proyecto) de area_revisores que el algoritmo ya resuelve igual.
        CREATE TEMP TABLE _redundantes ON COMMIT DROP AS
        SELECT r.area_scope_id, r.project_id
        FROM area_revisores r
        JOIN workers w ON w.id = r.revisor_id
        LEFT JOIN _jefatura_nodo j ON j.area_scope_id = r.area_scope_id
        LEFT JOIN _residente_obra ro ON ro.project_id = r.project_id
        WHERE r.state
          AND r.active
          AND 1 = (SELECT count(*) FROM area_revisores x
                   WHERE x.state
                     AND x.area_scope_id = r.area_scope_id
                     AND x.project_id IS NOT DISTINCT FROM r.project_id)
          AND (
                -- nivel de área u OFICINA CENTRAL: la jefatura del nodo
                ((r.project_id IS NULL OR r.project_id IN (SELECT project_id FROM _oficina_central))
                 AND j.worker_id IS NOT NULL
                 AND (j.worker_id = w.id OR (j.person_id IS NOT NULL AND j.person_id = w.person_id)))
             OR
                -- una obra: su residente
                (r.project_id IS NOT NULL
                 AND r.project_id NOT IN (SELECT project_id FROM _oficina_central)
                 AND ro.worker_id IS NOT NULL
                 AND (ro.worker_id = w.id OR (ro.person_id IS NOT NULL AND ro.person_id = w.person_id)))
              );

        -- area_revisores → aprobador de la salida.
        INSERT INTO area_actor_asignacion
            (area_scope_id, project_id, ga_actor_id, ga_actor_caso_id, worker_id,
             orden_prioridad, active, state, created_at, updated_at)
        SELECT r.area_scope_id,
               r.project_id,
               1,
               CASE WHEN r.project_id IS NULL
                         OR r.project_id IN (SELECT project_id FROM _oficina_central) THEN 1
                    ELSE 2 END,
               r.revisor_id,
               r.orden_prioridad,
               -- Por proyecto solo se aplicaba en las áreas que filtraban.
               r.active AND (r.project_id IS NULL OR r.area_scope_id IN (SELECT area_scope_id FROM _filtraban)),
               true,
               r.created_at,
               r.updated_at
        FROM area_revisores r
        WHERE r.state
          AND NOT EXISTS (SELECT 1 FROM _redundantes d
                          WHERE d.area_scope_id = r.area_scope_id
                            AND d.project_id IS NOT DISTINCT FROM r.project_id)
        ON CONFLICT DO NOTHING;
        GET DIAGNOSTICS n = ROW_COUNT;
        RAISE NOTICE 'Aprobador de la salida (area_revisores): % filas.', n;

        -- El jefe notificado de la salida salía de las filas de área de area_revisores.
        INSERT INTO area_actor_asignacion
            (area_scope_id, project_id, ga_actor_id, ga_actor_caso_id, worker_id,
             orden_prioridad, active, state, created_at, updated_at)
        SELECT r.area_scope_id, NULL, 2, 2, r.revisor_id, r.orden_prioridad, r.active, true,
               r.created_at, r.updated_at
        FROM area_revisores r
        WHERE r.state
          AND r.project_id IS NULL
          AND NOT EXISTS (SELECT 1 FROM _redundantes d
                          WHERE d.area_scope_id = r.area_scope_id AND d.project_id IS NULL)
        ON CONFLICT DO NOTHING;
        GET DIAGNOSTICS n = ROW_COUNT;
        RAISE NOTICE 'Jefe notificado (area_revisores): % filas.', n;

        -- area_revisores_rendicion → 1.ª revisión y aprobadores del consolidado.
        INSERT INTO area_actor_asignacion
            (area_scope_id, project_id, ga_actor_id, ga_actor_caso_id, worker_id,
             orden_prioridad, active, state, created_at, updated_at)
        SELECT r.area_scope_id,
               r.project_id,
               a.actor,
               CASE WHEN r.project_id IS NULL
                         OR r.project_id IN (SELECT project_id FROM _oficina_central) THEN 1
                    ELSE 2 END,
               r.revisor_id,
               r.orden_prioridad,
               r.active AND (r.project_id IS NULL OR r.area_scope_id IN (SELECT area_scope_id FROM _filtraban)),
               true,
               r.created_at,
               r.updated_at
        FROM area_revisores_rendicion r
        CROSS JOIN LATERAL (VALUES (3, r.aprueba_primera_revision), (5, r.aprueba_consolidado)) AS a(actor, marca)
        WHERE r.state AND a.marca
        ON CONFLICT DO NOTHING;
        GET DIAGNOSTICS n = ROW_COUNT;
        RAISE NOTICE '1.a revision / consolidado (area_revisores_rendicion): % filas.', n;

        -- area_consolidadores → consolidadores.
        INSERT INTO area_actor_asignacion
            (area_scope_id, project_id, ga_actor_id, ga_actor_caso_id, worker_id,
             orden_prioridad, active, state, created_at, updated_at)
        SELECT c.area_scope_id,
               c.project_id,
               4,
               CASE WHEN c.project_id IS NULL
                         OR c.project_id IN (SELECT project_id FROM _oficina_central) THEN 1
                    ELSE 2 END,
               c.consolidador_id,
               c.orden_prioridad,
               c.active AND (c.project_id IS NULL OR c.area_scope_id IN (SELECT area_scope_id FROM _filtraban)),
               true,
               c.created_at,
               c.updated_at
        FROM area_consolidadores c
        WHERE c.state
        ON CONFLICT DO NOTHING;
        GET DIAGNOSTICS n = ROW_COUNT;
        RAISE NOTICE 'Consolidadores (area_consolidadores): % filas.', n;

        -- Un área sin consolidadores propios consolidaba con TODOS sus revisores de rendición
        -- (sin mirar las casillas): se copian como consolidadores para que no cambie quién consolida.
        INSERT INTO area_actor_asignacion
            (area_scope_id, project_id, ga_actor_id, ga_actor_caso_id, worker_id,
             orden_prioridad, active, state, created_at, updated_at)
        SELECT r.area_scope_id,
               r.project_id,
               4,
               CASE WHEN r.project_id IS NULL
                         OR r.project_id IN (SELECT project_id FROM _oficina_central) THEN 1
                    ELSE 2 END,
               r.revisor_id,
               r.orden_prioridad,
               r.active AND (r.project_id IS NULL OR r.area_scope_id IN (SELECT area_scope_id FROM _filtraban)),
               true,
               r.created_at,
               r.updated_at
        FROM area_revisores_rendicion r
        WHERE r.state
          AND NOT EXISTS (SELECT 1 FROM area_consolidadores c
                          WHERE c.state AND c.area_scope_id = r.area_scope_id)
        ON CONFLICT DO NOTHING;
        GET DIAGNOSTICS n = ROW_COUNT;
        RAISE NOTICE 'Consolidadores heredados de area_revisores_rendicion: % filas.', n;
    END IF;

    IF EXISTS (SELECT 1 FROM workers_actor_asignacion) THEN
        RAISE NOTICE 'workers_actor_asignacion ya tiene filas: no se vuelve a migrar.';
    ELSE
        -- workers_revisores ("Jefe personalizado") → aprobador de la salida del trabajador, y
        -- también de su 1.ª revisión y de su consolidado: el jefe personalizado ya decidía los
        -- documentos cuando todos los de adentro lo compartían (siempre, en una planilla individual).
        INSERT INTO workers_actor_asignacion
            (worker_id, ga_actor_id, asignado_id, orden_prioridad, active, state, created_at, updated_at)
        SELECT r.solicitante_id, a.actor, r.revisor_id, r.orden_prioridad, r.active, true,
               r.created_at, r.updated_at
        FROM workers_revisores r
        CROSS JOIN (VALUES (1), (3), (5)) AS a(actor)
        WHERE r.state
        ON CONFLICT DO NOTHING;
        GET DIAGNOSTICS n = ROW_COUNT;
        RAISE NOTICE 'Personalizado por trabajador (workers_revisores): % filas.', n;
    END IF;
END $$;

-- ── 5) La pantalla nueva: Gestión Administrativa → Configuración → Revisores de Áreas ──
-- Mismo módulo que las otras secciones de esa configuración; la reciben los roles que hoy
-- tienen alguna de las dos pantallas que reemplaza.

INSERT INTO feature (feature_key, module_id)
SELECT 'gestion-administrativa.config.revisores-areas', module_id
FROM feature
WHERE feature_key = 'gestion-administrativa.config.lugares'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'gestion-administrativa.config.revisores-areas');

INSERT INTO role_feature (role_id, feature_id)
SELECT DISTINCT rf.role_id, nueva.feature_id
FROM role_feature rf
JOIN feature vieja ON vieja.feature_id = rf.feature_id
                  AND vieja.feature_key IN ('configuracion.revisores-areas',
                                            'gestion-administrativa.config.consolidadores-areas')
CROSS JOIN feature nueva
WHERE nueva.feature_key = 'gestion-administrativa.config.revisores-areas'
ON CONFLICT (role_id, feature_id) DO NOTHING;

COMMIT;

-- ── Verificación (solo lectura) ─────────────────────────────────────────────
SELECT a.nombre AS actor, c.nombre AS caso,
       count(*) FILTER (WHERE x.project_id IS NULL)     AS por_area,
       count(*) FILTER (WHERE x.project_id IS NOT NULL) AS por_obra,
       count(*) FILTER (WHERE NOT x.active)             AS inactivas
FROM area_actor_asignacion x
JOIN ga_actor a      ON a.ga_actor_id = x.ga_actor_id
JOIN ga_actor_caso c ON c.ga_actor_caso_id = x.ga_actor_caso_id
WHERE x.state
GROUP BY a.display_order, a.nombre, c.display_order, c.nombre
ORDER BY a.display_order, c.display_order;

SELECT count(*) AS personalizados_por_trabajador FROM workers_actor_asignacion WHERE state;

SELECT r.role_description
FROM role_feature rf
JOIN role r    ON r.role_id = rf.role_id
JOIN feature f ON f.feature_id = rf.feature_id
WHERE f.feature_key = 'gestion-administrativa.config.revisores-areas'
ORDER BY 1;
