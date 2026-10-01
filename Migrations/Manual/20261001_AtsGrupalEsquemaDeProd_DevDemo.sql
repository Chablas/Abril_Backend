-- ============================================================================
-- ATS Grupal y PETAR Grupal (Samuel, 30/09): el esquema que se creó directo en prod, sin script
-- ============================================================================
-- SOLO DEV Y DEMO. Prod ya lo tiene (ahí no hace nada).
--
-- El código de Samuel que entró a master y a demo el 2026-10-01 mapea siete tablas y dos
-- columnas que ningún script crea. Sin las columnas falla hasta el ATS individual (42703 al
-- leer ss_ats). Este archivo las copia de prod tal cual (pg_dump --schema-only del 2026-10-01:
-- tipos, NOT NULL, defaults, PK, FK, únicos e índices con los mismos nombres).
--
-- Quedan fuera las columnas capataz_* de ss_ats_grupo: las agregan después los scripts de
-- Samuel, en este orden:
--   1. ESTE ARCHIVO
--   2. Migrations_Manual/2026-09-30_ats_firma_capataz.sql
--   3. Migrations_Manual/2026-09-30_ats_grupo_firma_capataz.sql
--   4. Migrations_Manual/2026-09-30_ats_grupo_firma_capataz_evidencia.sql
--   5. Migrations_Manual/2026-09-30_ats_grupo_proyecto_qr.sql
--   6. Migrations_Manual/2026-09-30_ats_capataz_cuenta.sql
-- Al final, Migrations/Manual/20261001_AtsGrupal_VerificacionSoloLectura.sql: las 15 filas en true.
--
-- Idempotente (IF NOT EXISTS). Sin datos: solo esquema.
-- ============================================================================

BEGIN;

-- ATS Grupal -------------------------------------------------------------------

CREATE SEQUENCE IF NOT EXISTS ss_ats_grupo_id_seq AS integer;

CREATE TABLE IF NOT EXISTS ss_ats_grupo (
    id                    integer      NOT NULL DEFAULT nextval('ss_ats_grupo_id_seq'::regclass),
    creado_por_worker_id  integer      NOT NULL,
    proyecto_id           integer      NOT NULL,
    plantilla_id          integer,
    actividad             text         NOT NULL,
    torre_nombre          text,
    pisos                 text,
    lugar                 text,
    fecha                 date         NOT NULL,
    qr_token              uuid         NOT NULL DEFAULT gen_random_uuid(),
    qr_expira_en          timestamptz  NOT NULL,
    estado                text         NOT NULL DEFAULT 'Activo'::text,
    created_at            timestamptz  NOT NULL DEFAULT now(),
    updated_at            timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT ss_ats_grupo_pkey PRIMARY KEY (id),
    CONSTRAINT ss_ats_grupo_qr_token_key UNIQUE (qr_token),
    CONSTRAINT ss_ats_grupo_creado_por_worker_id_fkey FOREIGN KEY (creado_por_worker_id) REFERENCES workers(id),
    CONSTRAINT ss_ats_grupo_proyecto_id_fkey FOREIGN KEY (proyecto_id) REFERENCES project(project_id),
    CONSTRAINT ss_ats_grupo_plantilla_id_fkey FOREIGN KEY (plantilla_id) REFERENCES ss_ats_plantilla(id)
);
ALTER SEQUENCE ss_ats_grupo_id_seq OWNED BY ss_ats_grupo.id;
CREATE INDEX IF NOT EXISTS ix_ss_ats_grupo_proyecto_fecha ON ss_ats_grupo (proyecto_id, fecha);

CREATE SEQUENCE IF NOT EXISTS ss_ats_grupo_paso_seleccionado_id_seq AS integer;

CREATE TABLE IF NOT EXISTS ss_ats_grupo_paso_seleccionado (
    id                integer   NOT NULL DEFAULT nextval('ss_ats_grupo_paso_seleccionado_id_seq'::regclass),
    ats_grupo_id      integer   NOT NULL,
    paso_id           integer,
    categoria_nombre  text      NOT NULL,
    texto             text      NOT NULL,
    aplica            boolean   NOT NULL,
    orden             smallint  NOT NULL DEFAULT 0,
    CONSTRAINT ss_ats_grupo_paso_seleccionado_pkey PRIMARY KEY (id),
    CONSTRAINT ss_ats_grupo_paso_seleccionado_ats_grupo_id_fkey FOREIGN KEY (ats_grupo_id) REFERENCES ss_ats_grupo(id) ON DELETE CASCADE,
    CONSTRAINT ss_ats_grupo_paso_seleccionado_paso_id_fkey FOREIGN KEY (paso_id) REFERENCES ss_ats_paso(id)
);
ALTER SEQUENCE ss_ats_grupo_paso_seleccionado_id_seq OWNED BY ss_ats_grupo_paso_seleccionado.id;

CREATE SEQUENCE IF NOT EXISTS ss_ats_grupo_epp_seleccionado_id_seq AS integer;

CREATE TABLE IF NOT EXISTS ss_ats_grupo_epp_seleccionado (
    id            integer  NOT NULL DEFAULT nextval('ss_ats_grupo_epp_seleccionado_id_seq'::regclass),
    ats_grupo_id  integer  NOT NULL,
    epp_id        integer  NOT NULL,
    nombre        text     NOT NULL,
    CONSTRAINT ss_ats_grupo_epp_seleccionado_pkey PRIMARY KEY (id),
    CONSTRAINT ss_ats_grupo_epp_seleccionado_ats_grupo_id_fkey FOREIGN KEY (ats_grupo_id) REFERENCES ss_ats_grupo(id) ON DELETE CASCADE
);
ALTER SEQUENCE ss_ats_grupo_epp_seleccionado_id_seq OWNED BY ss_ats_grupo_epp_seleccionado.id;

CREATE SEQUENCE IF NOT EXISTS ss_ats_grupo_herramienta_seleccionada_id_seq AS integer;

CREATE TABLE IF NOT EXISTS ss_ats_grupo_herramienta_seleccionada (
    id              integer  NOT NULL DEFAULT nextval('ss_ats_grupo_herramienta_seleccionada_id_seq'::regclass),
    ats_grupo_id    integer  NOT NULL,
    herramienta_id  integer,
    nombre          text     NOT NULL,
    CONSTRAINT ss_ats_grupo_herramienta_seleccionada_pkey PRIMARY KEY (id),
    CONSTRAINT ss_ats_grupo_herramienta_seleccionada_ats_grupo_id_fkey FOREIGN KEY (ats_grupo_id) REFERENCES ss_ats_grupo(id) ON DELETE CASCADE
);
ALTER SEQUENCE ss_ats_grupo_herramienta_seleccionada_id_seq OWNED BY ss_ats_grupo_herramienta_seleccionada.id;

CREATE SEQUENCE IF NOT EXISTS ss_ats_grupo_riesgo_detalle_id_seq AS integer;

CREATE TABLE IF NOT EXISTS ss_ats_grupo_riesgo_detalle (
    id               integer   NOT NULL DEFAULT nextval('ss_ats_grupo_riesgo_detalle_id_seq'::regclass),
    ats_grupo_id     integer   NOT NULL,
    peligro_id       integer   NOT NULL,
    riesgo_id        integer   NOT NULL,
    peligro_nombre   text      NOT NULL,
    riesgo_nombre    text      NOT NULL,
    riesgo_base      text      NOT NULL,
    controles        text      NOT NULL,
    riesgo_residual  text      NOT NULL,
    orden            smallint  NOT NULL DEFAULT 0,
    CONSTRAINT ss_ats_grupo_riesgo_detalle_pkey PRIMARY KEY (id),
    CONSTRAINT ss_ats_grupo_riesgo_detalle_ats_grupo_id_fkey FOREIGN KEY (ats_grupo_id) REFERENCES ss_ats_grupo(id) ON DELETE CASCADE
);
ALTER SEQUENCE ss_ats_grupo_riesgo_detalle_id_seq OWNED BY ss_ats_grupo_riesgo_detalle.id;

ALTER TABLE ss_ats ADD COLUMN IF NOT EXISTS ats_grupo_id integer;
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ss_ats_ats_grupo_id_fkey') THEN
        ALTER TABLE ss_ats ADD CONSTRAINT ss_ats_ats_grupo_id_fkey
            FOREIGN KEY (ats_grupo_id) REFERENCES ss_ats_grupo(id);
    END IF;
END $$;
CREATE INDEX IF NOT EXISTS ix_ss_ats_ats_grupo_id ON ss_ats (ats_grupo_id);

-- PETAR Grupal -----------------------------------------------------------------

CREATE SEQUENCE IF NOT EXISTS ss_petar_grupo_id_seq AS integer;

CREATE TABLE IF NOT EXISTS ss_petar_grupo (
    id                        integer      NOT NULL DEFAULT nextval('ss_petar_grupo_id_seq'::regclass),
    ats_grupo_id              integer      NOT NULL,
    tipo_id                   integer      NOT NULL,
    proyecto_id               integer      NOT NULL,
    creado_por_worker_id      integer      NOT NULL,
    descripcion_trabajo       text         NOT NULL,
    lugar                     text,
    fecha                     date         NOT NULL,
    hora_inicio               time,
    hora_fin                  time,
    estado                    text         NOT NULL DEFAULT 'Activo'::text,
    supervisor_worker_id      integer,
    supervisor_nombre         text,
    supervisor_cargo          text,
    supervisor_firma_url      text,
    supervisor_firma_hash     text,
    supervisor_hora_servidor  timestamptz,
    ssoma_worker_id           integer,
    ssoma_nombre              text,
    ssoma_cargo               text,
    ssoma_firma_url           text,
    ssoma_firma_hash          text,
    ssoma_hora_servidor       timestamptz,
    created_at                timestamptz  NOT NULL DEFAULT now(),
    updated_at                timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT ss_petar_grupo_pkey PRIMARY KEY (id),
    CONSTRAINT ss_petar_grupo_ats_grupo_id_fkey FOREIGN KEY (ats_grupo_id) REFERENCES ss_ats_grupo(id),
    CONSTRAINT ss_petar_grupo_tipo_id_fkey FOREIGN KEY (tipo_id) REFERENCES ss_petar_tipo(id),
    CONSTRAINT ss_petar_grupo_proyecto_id_fkey FOREIGN KEY (proyecto_id) REFERENCES project(project_id),
    CONSTRAINT ss_petar_grupo_creado_por_worker_id_fkey FOREIGN KEY (creado_por_worker_id) REFERENCES workers(id)
);
ALTER SEQUENCE ss_petar_grupo_id_seq OWNED BY ss_petar_grupo.id;
CREATE INDEX IF NOT EXISTS ix_ss_petar_grupo_ats_grupo_id ON ss_petar_grupo (ats_grupo_id);

CREATE SEQUENCE IF NOT EXISTS ss_petar_grupo_item_respuesta_id_seq AS integer;

CREATE TABLE IF NOT EXISTS ss_petar_grupo_item_respuesta (
    id              integer   NOT NULL DEFAULT nextval('ss_petar_grupo_item_respuesta_id_seq'::regclass),
    petar_grupo_id  integer   NOT NULL,
    item_id         integer   NOT NULL,
    texto           text      NOT NULL,
    respuesta       text      NOT NULL,
    orden           smallint  NOT NULL DEFAULT 0,
    CONSTRAINT ss_petar_grupo_item_respuesta_pkey PRIMARY KEY (id),
    CONSTRAINT ss_petar_grupo_item_respuesta_petar_grupo_id_fkey FOREIGN KEY (petar_grupo_id) REFERENCES ss_petar_grupo(id) ON DELETE CASCADE
);
ALTER SEQUENCE ss_petar_grupo_item_respuesta_id_seq OWNED BY ss_petar_grupo_item_respuesta.id;

ALTER TABLE ss_petar ADD COLUMN IF NOT EXISTS petar_grupo_id integer;
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ss_petar_petar_grupo_id_fkey') THEN
        ALTER TABLE ss_petar ADD CONSTRAINT ss_petar_petar_grupo_id_fkey
            FOREIGN KEY (petar_grupo_id) REFERENCES ss_petar_grupo(id);
    END IF;
END $$;
CREATE INDEX IF NOT EXISTS ix_ss_petar_petar_grupo_id ON ss_petar (petar_grupo_id);

COMMIT;
