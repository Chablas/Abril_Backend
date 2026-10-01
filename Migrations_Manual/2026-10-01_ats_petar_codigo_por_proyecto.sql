-- El código de ATS/PETAR pasa a llevar la abreviatura del proyecto (project.abbreviation, la misma que
-- usan Accidentes/Observaciones): BUG-ATS-0001, BUG-PETAR-0001... con correlativo propio POR PROYECTO.
-- Reemplaza el correlativo global de 2026-10-01_ats_petar_codigo.sql (esas secuencias ya no se usan).

-- Contador atómico por (proyecto, tipo): el INSERT ... ON CONFLICT evita que dos documentos creados a la vez
-- reciban el mismo número.
CREATE TABLE IF NOT EXISTS ss_documento_correlativo (
    proyecto_id integer     NOT NULL,
    tipo        varchar(10) NOT NULL,
    ultimo      integer     NOT NULL DEFAULT 0,
    PRIMARY KEY (proyecto_id, tipo)
);

-- Autocontenido: si la columna ya existe (varchar(20) de la versión global) se amplía; si no, se crea.
ALTER TABLE ss_ats   ADD COLUMN IF NOT EXISTS codigo varchar(40);
ALTER TABLE ss_petar ADD COLUMN IF NOT EXISTS codigo varchar(40);
ALTER TABLE ss_ats   ALTER COLUMN codigo TYPE varchar(40);
ALTER TABLE ss_petar ALTER COLUMN codigo TYPE varchar(40);

CREATE UNIQUE INDEX IF NOT EXISTS ux_ss_ats_codigo   ON ss_ats (codigo)   WHERE codigo IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_ss_petar_codigo ON ss_petar (codigo) WHERE codigo IS NOT NULL;

-- Los códigos globales ("ATS-000001") salieron solo en documentos de prueba — se limpian para no mezclar formatos.
UPDATE ss_ats   SET codigo = NULL WHERE codigo LIKE 'ATS-%';
UPDATE ss_petar SET codigo = NULL WHERE codigo LIKE 'PETAR-%';

DROP SEQUENCE IF EXISTS ss_ats_codigo_seq;
DROP SEQUENCE IF EXISTS ss_petar_codigo_seq;
