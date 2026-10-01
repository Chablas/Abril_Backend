-- Observaciones (Capataz/Residente/Producción/SSOMA) sobre un ATS o un ATS grupal, y revisiones del ATS grupal.

CREATE TABLE IF NOT EXISTS ss_ats_observacion (
    id                      serial PRIMARY KEY,
    ats_id                  integer NULL REFERENCES ss_ats(id) ON DELETE CASCADE,
    ats_grupo_id            integer NULL REFERENCES ss_ats_grupo(id) ON DELETE CASCADE,
    rol                     varchar(20) NOT NULL,
    autor_worker_id         integer NOT NULL,
    autor_nombre            varchar(200) NOT NULL,
    texto                   varchar(1000) NOT NULL,
    estado                  varchar(12) NOT NULL DEFAULT 'Abierta',
    respuesta               varchar(1000) NULL,
    resuelta_por_worker_id  integer NULL,
    resuelta_por_nombre     varchar(200) NULL,
    resuelta_en             timestamp without time zone NULL,
    created_at              timestamp without time zone NOT NULL DEFAULT (now() at time zone 'utc'),
    CONSTRAINT ck_ss_ats_observacion_destino CHECK ((ats_id IS NOT NULL) <> (ats_grupo_id IS NOT NULL))
);
CREATE INDEX IF NOT EXISTS ix_ss_ats_observacion_ats   ON ss_ats_observacion (ats_id)       WHERE ats_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_ss_ats_observacion_grupo ON ss_ats_observacion (ats_grupo_id) WHERE ats_grupo_id IS NOT NULL;

-- Revisión de un ATS grupal: la corrección crea un grupo nuevo (Rev. N+1) enlazado al anterior, que queda 'Reemplazado'.
ALTER TABLE ss_ats_grupo ADD COLUMN IF NOT EXISTS grupo_anterior_id integer NULL;
ALTER TABLE ss_ats_grupo ADD COLUMN IF NOT EXISTS revision integer NOT NULL DEFAULT 1;
