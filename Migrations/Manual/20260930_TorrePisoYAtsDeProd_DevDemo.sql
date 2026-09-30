-- ============================================================================
-- Dev y demo: lo que se agregó directo en prod para Torre/Piso (RAC, Inspección, ATS y las torres
-- de Configuración → Proyectos) y para el catálogo de ATS, sin script en el repo (salvo
-- ss_ats_riesgo_control.tipo: Migrations_Manual/2026-09-29_ats_riesgo_control_tipo.sql).
-- Fecha: 2026-09-30
--
-- El código de origin/demo ya lo usa desde el deploy del 2026-09-29 (commits ed125216, eb06f707 y
-- c6f1f3cc de Samuel): sin esto, en demo y en dev RAC, Inspección y ATS dan 42703 y las torres de
-- un proyecto, 42P01.
--
-- Solo estructura, sin datos: las torres quedan vacías, los EPP en «Específico», los pasos sin
-- PETAR y los controles en «Administrativo» (en prod los clasificaron a mano). Definiciones
-- copiadas de prod (pg_dump --schema-only y pg_attribute).
--
-- Idempotente y en un solo bloque (todo o nada). Solo crea lo que falta: en prod no ejecuta
-- ningún ALTER (ya lo tiene todo). Se puede correr antes o después del deploy.
-- ============================================================================

DO $$
DECLARE
  c record;
BEGIN
  IF to_regclass('public.project_torre') IS NULL THEN
    CREATE TABLE public.project_torre (
        id                 integer  NOT NULL,
        project_id         integer  NOT NULL,
        nombre             text     NOT NULL,
        orden              smallint NOT NULL DEFAULT 0,
        cantidad_sotanos   integer  NOT NULL DEFAULT 0,
        cantidad_pisos     integer  NOT NULL DEFAULT 0,
        cantidad_cisternas integer  NOT NULL DEFAULT 0
    );
    CREATE SEQUENCE public.project_torre_id_seq AS integer START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;
    ALTER SEQUENCE public.project_torre_id_seq OWNED BY public.project_torre.id;
    ALTER TABLE public.project_torre ALTER COLUMN id SET DEFAULT nextval('public.project_torre_id_seq'::regclass);
    ALTER TABLE public.project_torre ADD CONSTRAINT project_torre_pkey PRIMARY KEY (id);
    CREATE INDEX ix_project_torre_project ON public.project_torre USING btree (project_id);
    ALTER TABLE public.project_torre
        ADD CONSTRAINT project_torre_project_id_fkey FOREIGN KEY (project_id) REFERENCES public.project(project_id);
    RAISE NOTICE 'creada project_torre';
  END IF;

  FOR c IN
    SELECT * FROM (VALUES
      ('ss_ats',                'pisos',          'text'),
      ('ss_ats',                'torre_nombre',   'text'),
      ('ss_ats_epp',            'categoria',      'character varying(40) NOT NULL DEFAULT ''Específico'''),
      ('ss_ats_paso',           'requiere_petar', 'boolean NOT NULL DEFAULT false'),
      ('ss_ats_riesgo_control', 'tipo',           'character varying(20) NOT NULL DEFAULT ''Administrativo'''),
      ('ssoma_inspeccion',      'ambito_lugar',   'text'),
      ('ssoma_inspeccion',      'proyecto_piso',  'text'),
      ('ssoma_inspeccion',      'torre_nombre',   'text'),
      ('ssoma_rac',             'torre_nombre',   'text')
    ) AS v(tabla, columna, definicion)
  LOOP
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema = 'public' AND table_name = c.tabla AND column_name = c.columna) THEN
      EXECUTE format('ALTER TABLE public.%I ADD COLUMN %I %s', c.tabla, c.columna, c.definicion);
      RAISE NOTICE 'agregada %.%', c.tabla, c.columna;
    END IF;
  END LOOP;
END $$;

-- Verificación (debe devolver 10 filas: la tabla y las 9 columnas):
-- SELECT 'project_torre' AS que, to_regclass('public.project_torre') IS NOT NULL AS existe
-- UNION ALL
-- SELECT table_name || '.' || column_name, true FROM information_schema.columns
-- WHERE table_schema = 'public' AND (table_name, column_name) IN (
--   ('ss_ats','pisos'), ('ss_ats','torre_nombre'), ('ss_ats_epp','categoria'), ('ss_ats_paso','requiere_petar'),
--   ('ss_ats_riesgo_control','tipo'), ('ssoma_inspeccion','ambito_lugar'), ('ssoma_inspeccion','proyecto_piso'),
--   ('ssoma_inspeccion','torre_nombre'), ('ssoma_rac','torre_nombre'));
