-- Código del ATS grupal, mismo formato que el individual pero con tipo propio: BUG-ATSG-0001
-- (abreviatura del proyecto + correlativo por proyecto; usa la tabla ss_documento_correlativo con tipo 'ATSG').
-- Los grupos ya creados son de prueba y quedan sin código (NULL).
ALTER TABLE ss_ats_grupo ADD COLUMN IF NOT EXISTS codigo varchar(40);
CREATE UNIQUE INDEX IF NOT EXISTS ux_ss_ats_grupo_codigo ON ss_ats_grupo (codigo) WHERE codigo IS NOT NULL;
