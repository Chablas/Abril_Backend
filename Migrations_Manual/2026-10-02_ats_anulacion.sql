-- Anulación de ATS / ATS grupal: nunca se borra, queda Estado = 'Anulado' con motivo, quién y cuándo.
ALTER TABLE ss_ats       ADD COLUMN IF NOT EXISTS anulado_motivo varchar(300);
ALTER TABLE ss_ats       ADD COLUMN IF NOT EXISTS anulado_por_worker_id integer;
ALTER TABLE ss_ats       ADD COLUMN IF NOT EXISTS anulado_en timestamp without time zone;

ALTER TABLE ss_ats_grupo ADD COLUMN IF NOT EXISTS anulado_motivo varchar(300);
ALTER TABLE ss_ats_grupo ADD COLUMN IF NOT EXISTS anulado_por_worker_id integer;
ALTER TABLE ss_ats_grupo ADD COLUMN IF NOT EXISTS anulado_en timestamp without time zone;
