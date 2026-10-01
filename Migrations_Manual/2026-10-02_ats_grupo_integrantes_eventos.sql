-- ATS grupal: lista previa de integrantes esperados (para ver quién falta firmar) y registro de eventos
-- (cerrado / reabierto) con quién lo hizo.

CREATE TABLE IF NOT EXISTS ss_ats_grupo_integrante (
    id            serial PRIMARY KEY,
    ats_grupo_id  integer NOT NULL REFERENCES ss_ats_grupo(id) ON DELETE CASCADE,
    worker_id     integer NOT NULL,
    created_at    timestamp without time zone NOT NULL DEFAULT (now() at time zone 'utc'),
    CONSTRAINT ux_ss_ats_grupo_integrante UNIQUE (ats_grupo_id, worker_id)
);

CREATE TABLE IF NOT EXISTS ss_ats_grupo_evento (
    id            serial PRIMARY KEY,
    ats_grupo_id  integer NOT NULL REFERENCES ss_ats_grupo(id) ON DELETE CASCADE,
    evento        varchar(30) NOT NULL,
    worker_id     integer NULL,
    detalle       varchar(300) NULL,
    created_at    timestamp without time zone NOT NULL DEFAULT (now() at time zone 'utc')
);
CREATE INDEX IF NOT EXISTS ix_ss_ats_grupo_evento_grupo ON ss_ats_grupo_evento (ats_grupo_id, created_at DESC);
