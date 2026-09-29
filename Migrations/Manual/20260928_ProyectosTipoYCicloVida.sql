-- ============================================================================
-- Configuración → Proyectos — Tipo de proyecto y ciclo de vida
-- ANTES DE DESPLEGAR EL BACKEND Y EL FRONTEND
-- Fecha: 2026-09-28
--
-- Hoy el ciclo de vida del proyecto está repartido en tres columnas que se contradicen:
--   · project.estado    texto libre del modal («Estado del proyecto»): ACTIVO, FINALIZADO,
--                       INACTIVO o vacío. Lo leen Checklist, Dashboard UDP, Drivers y AC.
--   · project.activo    texto («Ciclo de vida»): Activo, Finalizado o vacío. Lo leen los ratios
--                       de Presupuesto de materiales (y dos consultas lo interpretaban distinto
--                       cuando está vacío).
--   · project.operativo boolean del dashboard del PASO.
-- En prod, OFICINA CENTRAL decía INACTIVO en una y Activo en otra, AQUILARIA seguía ACTIVO
-- aunque terminó en 2024 y GRAN MANZANO no tenía ciclo de vida (los ratios lo trataban como
-- obra cerrada). Tampoco había cómo distinguir un proyecto de verdad (un edificio que se vende
-- al público) de las filas que no lo son: la sede central, áreas de la empresa registradas como
-- proyecto para que funcionen otras pantallas y el proyecto de prueba (Torre Abril, que se
-- canceló cuando otra empresa compró el terreno).
--
-- Este paso:
--   1) Crea los catálogos, con ids fijos (el código los nombra: ProjectTipoIds /
--      ProjectCicloVidaIds):
--      · project_tipo: 1 PROYECTO (edificio que se vende al público), 2 FFT (edificación
--        personal del gerente general, no se vende), 3 OFICINA_CENTRAL (la sede), 4 AREA_INTERNA
--        (Post Venta, Arquitectura Comercial, Eventos...) y 5 PRUEBA. es_obra dice si los de ese
--        tipo se tratan como obra: PROYECTO, FFT y PRUEBA (para poder probar ahí) sí; la sede y
--        las áreas, no.
--      · project_ciclo_vida: 1 ACTIVO, 2 FINALIZADO, 3 INACTIVO.
--   2) Agrega project.project_tipo_id y project.project_ciclo_vida_id (FK, NOT NULL; default
--      PROYECTO / ACTIVO para que el backend viejo pueda seguir creando proyectos hasta el
--      deploy).
--   3) Los llena desde las tres columnas viejas, con estas correcciones:
--      · Tipo: OFICINA_CENTRAL = OFICINA CENTRAL; AREA_INTERNA = POST VENTA, ARQUITECTURA
--        COMERCIAL, EVENTOS y GENERAL; PRUEBA = TORRE ABRIL y la fila «e» (ya eliminada); el
--        resto, PROYECTO. Ninguno queda FFT: ese tipo (p. ej. NAPLO) se asigna desde la
--        pantalla.
--      · Ciclo de vida: FINALIZADO si alguna columna vieja lo decía (o operativo = false) y
--        AQUILARIA (fin 2024-07-31); INACTIVO si estado decía INACTIVO fuera de la sede y las
--        áreas (Baronet, Naplo, Contralmirante Villar); el resto, ACTIVO. La sede y las áreas
--        pasan a ACTIVO: el INACTIVO que tenían significaba «no es un proyecto», y eso ahora lo
--        dice el tipo. Torre Abril queda ACTIVO: como proyecto de prueba sigue en uso.
--
-- NO confundir con project.active (columna de sistema: si el proyecto aparece en filtros y
-- desplegables) ni con project.state (baja lógica): esas no se tocan.
--
-- Las columnas viejas se botan en 20260928_ProyectosTipoYCicloVida_PostDeploy.sql, DESPUÉS del
-- deploy (el backend desplegado todavía las lee). Si entre este script y el deploy alguien cambia
-- el estado o el ciclo de vida de un proyecto desde la pantalla vieja, ese cambio no llega a las
-- columnas nuevas.
--
-- Idempotente: el llenado solo corre sobre filas sin valor, así que volver a correrlo no pisa lo
-- que se haya editado después. Los proyectos se buscan por nombre (los ids no coinciden entre
-- dev y prod). Aplicar en dev, demo y prod.
-- ============================================================================

BEGIN;

-- ALTER TABLE project bloquea la tabla hasta el COMMIT: si hay una consulta larga delante, mejor
-- abortar y reintentar que dejar a toda la app esperando.
SET LOCAL lock_timeout = '15s';

-- 1) Catálogos, con ids fijos.
CREATE TABLE IF NOT EXISTS project_tipo (
    project_tipo_id   integer PRIMARY KEY,
    codigo            text NOT NULL,
    nombre            text NOT NULL,
    descripcion       text NULL,
    es_obra           boolean NOT NULL,
    orden             integer NOT NULL DEFAULT 0,
    created_date_time timestamp with time zone NOT NULL DEFAULT now(),
    created_user_id   integer NULL,
    updated_date_time timestamp with time zone NULL,
    updated_user_id   integer NULL,
    active            boolean NOT NULL DEFAULT true,
    state             boolean NOT NULL DEFAULT true
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_project_tipo_codigo_vigente
    ON project_tipo (codigo) WHERE state;

CREATE TABLE IF NOT EXISTS project_ciclo_vida (
    project_ciclo_vida_id integer PRIMARY KEY,
    codigo                text NOT NULL,
    nombre                text NOT NULL,
    descripcion           text NULL,
    orden                 integer NOT NULL DEFAULT 0,
    created_date_time     timestamp with time zone NOT NULL DEFAULT now(),
    created_user_id       integer NULL,
    updated_date_time     timestamp with time zone NULL,
    updated_user_id       integer NULL,
    active                boolean NOT NULL DEFAULT true,
    state                 boolean NOT NULL DEFAULT true
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_project_ciclo_vida_codigo_vigente
    ON project_ciclo_vida (codigo) WHERE state;

INSERT INTO project_tipo (project_tipo_id, codigo, nombre, descripcion, es_obra, orden, created_user_id)
VALUES (1, 'PROYECTO',        'Proyecto',        'Proyecto inmobiliario: edificio que se vende al público.', true, 1, 1),
       (2, 'FFT',             'FFT',             'Edificación personal del gerente general (p. ej. su casa de playa): no está ni estará a la venta al público.', true, 2, 1),
       (3, 'OFICINA_CENTRAL', 'Oficina Central', 'Sede central de Abril, donde trabaja el personal de oficina (GTH, TI, Post Venta y demás áreas). No es un proyecto.', false, 3, 1),
       (4, 'AREA_INTERNA',    'Área interna',    'Área de la empresa registrada como proyecto para que funcionen otras pantallas (Post Venta, Arquitectura Comercial, Eventos). No es un proyecto.', false, 4, 1),
       (5, 'PRUEBA',          'Prueba',          'Proyecto de prueba del sistema. No es un proyecto.', true, 5, 1)
ON CONFLICT (project_tipo_id) DO NOTHING;

INSERT INTO project_ciclo_vida (project_ciclo_vida_id, codigo, nombre, descripcion, orden, created_user_id)
VALUES (1, 'ACTIVO',     'Activo',     'Vigente: en ejecución o por ejecutarse.', 1, 1),
       (2, 'FINALIZADO', 'Finalizado', 'Terminado.', 2, 1),
       (3, 'INACTIVO',   'Inactivo',   'Paralizado, cancelado o sin actividad.', 3, 1)
ON CONFLICT (project_ciclo_vida_id) DO NOTHING;

-- Los ids los nombra el código: si una fila ya existía con otro significado, no se sigue.
DO $$
BEGIN
    IF (SELECT count(*) FROM project_tipo
        WHERE (project_tipo_id, codigo) IN ((1, 'PROYECTO'), (2, 'FFT'), (3, 'OFICINA_CENTRAL'),
                                            (4, 'AREA_INTERNA'), (5, 'PRUEBA'))) <> 5 THEN
        RAISE EXCEPTION 'project_tipo no tiene PROYECTO=1, FFT=2, OFICINA_CENTRAL=3, AREA_INTERNA=4, PRUEBA=5. Abortado.';
    END IF;
    IF (SELECT count(*) FROM project_ciclo_vida
        WHERE (project_ciclo_vida_id, codigo) IN ((1, 'ACTIVO'), (2, 'FINALIZADO'), (3, 'INACTIVO'))) <> 3 THEN
        RAISE EXCEPTION 'project_ciclo_vida no tiene ACTIVO=1, FINALIZADO=2, INACTIVO=3. Abortado.';
    END IF;
END $$;

-- 2) Columnas nuevas: primero sin default, para que el llenado de abajo las encuentre vacías.
ALTER TABLE project ADD COLUMN IF NOT EXISTS project_tipo_id integer NULL;
ALTER TABLE project ADD COLUMN IF NOT EXISTS project_ciclo_vida_id integer NULL;

-- 3) Llenado. Solo si queda alguna fila sin valor (la primera vez, todas). Va en SQL dinámico
--    porque lee las columnas viejas, que después del PostDeploy ya no existen: así una segunda
--    corrida no falla.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM project WHERE project_tipo_id IS NULL) THEN
        EXECUTE $q$
            UPDATE project
               SET project_tipo_id =
                   CASE WHEN upper(btrim(project_description)) = 'OFICINA CENTRAL' THEN 3
                        WHEN upper(btrim(project_description)) IN
                             ('POST VENTA', 'ARQUITECTURA COMERCIAL', 'EVENTOS', 'GENERAL') THEN 4
                        WHEN upper(btrim(project_description)) IN ('TORRE ABRIL', 'E') THEN 5
                        ELSE 1 END
             WHERE project_tipo_id IS NULL
        $q$;
    END IF;

    IF EXISTS (SELECT 1 FROM project WHERE project_ciclo_vida_id IS NULL) THEN
        -- En la sede y las áreas (3 y 4) el INACTIVO de estado significaba «no es un proyecto».
        EXECUTE $q$
            UPDATE project
               SET project_ciclo_vida_id =
                   CASE WHEN upper(btrim(coalesce(estado, ''))) = 'FINALIZADO'
                          OR lower(btrim(coalesce(activo, ''))) = 'finalizado'
                          OR NOT operativo
                          OR upper(btrim(project_description)) = 'AQUILARIA' THEN 2
                        WHEN project_tipo_id NOT IN (3, 4)
                         AND (upper(btrim(coalesce(estado, ''))) = 'INACTIVO'
                              OR lower(btrim(coalesce(activo, ''))) = 'inactivo') THEN 3
                        ELSE 1 END
             WHERE project_ciclo_vida_id IS NULL
        $q$;
    END IF;
END $$;

-- 4) Default (para el backend viejo, que no conoce estas columnas), NOT NULL y FKs.
ALTER TABLE project ALTER COLUMN project_tipo_id SET DEFAULT 1;
ALTER TABLE project ALTER COLUMN project_ciclo_vida_id SET DEFAULT 1;
ALTER TABLE project ALTER COLUMN project_tipo_id SET NOT NULL;
ALTER TABLE project ALTER COLUMN project_ciclo_vida_id SET NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_project_project_tipo') THEN
        ALTER TABLE project
            ADD CONSTRAINT fk_project_project_tipo
            FOREIGN KEY (project_tipo_id) REFERENCES project_tipo (project_tipo_id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_project_project_ciclo_vida') THEN
        ALTER TABLE project
            ADD CONSTRAINT fk_project_project_ciclo_vida
            FOREIGN KEY (project_ciclo_vida_id) REFERENCES project_ciclo_vida (project_ciclo_vida_id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_project_project_tipo ON project (project_tipo_id);
CREATE INDEX IF NOT EXISTS ix_project_project_ciclo_vida ON project (project_ciclo_vida_id);

COMMIT;

-- ── Verificación ────────────────────────────────────────────────────────────
-- a) Cuántos proyectos quedaron en cada caso. Prod 2026-09-28 (45 filas, una eliminada):
--    PROYECTO 38 = 25 ACTIVO, 10 FINALIZADO (Aquilaria incluido) y 3 INACTIVO (Baronet,
--    Contralmirante Villar, Naplo); OFICINA_CENTRAL 1 ACTIVO; AREA_INTERNA 4 ACTIVO
--    (Arquitectura Comercial, Eventos, General, Post Venta); PRUEBA 2 ACTIVO (Torre Abril y «e»);
--    FFT 0.
-- SELECT t.codigo AS tipo, c.codigo AS ciclo_vida, count(*) AS proyectos,
--        string_agg(p.project_description, ', ' ORDER BY p.project_description) AS cuales
-- FROM project p
-- JOIN project_tipo t ON t.project_tipo_id = p.project_tipo_id
-- JOIN project_ciclo_vida c ON c.project_ciclo_vida_id = p.project_ciclo_vida_id
-- GROUP BY 1, 2
-- ORDER BY 1, 2;
--
-- b) Nada quedó sin valor (debe dar 0):
-- SELECT count(*) FROM project WHERE project_tipo_id IS NULL OR project_ciclo_vida_id IS NULL;
