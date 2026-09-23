-- ═══ Código de la rendición grupal (Consolidado del S10): CON-AAAA-NNNN ═══
-- Mismo patrón que SOL-AAAA-NNNN y REN-AAAA-NNNN (RG-02): codigo + anio + numero,
-- con los únicos parciales por state para que un documento reemplazado no bloquee
-- que su sucesor herede el mismo código.
-- Re-corrible: se puede ejecutar varias veces sin efecto extra.

ALTER TABLE ga_consolidado_s10 ADD COLUMN IF NOT EXISTS codigo text;
ALTER TABLE ga_consolidado_s10 ADD COLUMN IF NOT EXISTS anio   integer;
ALTER TABLE ga_consolidado_s10 ADD COLUMN IF NOT EXISTS numero integer;

-- Numeración de los consolidados que ya existen: por año de subida (hora de Perú, que
-- es con la que el backend arma el correlativo) y en orden de subida. Solo toca filas
-- sin código, así que re-ejecutarlo no renumera nada.
WITH numerados AS (
    SELECT id,
           EXTRACT(YEAR FROM (uploaded_at AT TIME ZONE 'America/Lima'))::int AS anio,
           ROW_NUMBER() OVER (
               PARTITION BY EXTRACT(YEAR FROM (uploaded_at AT TIME ZONE 'America/Lima'))::int
               ORDER BY uploaded_at, id
           )::int AS numero
    FROM ga_consolidado_s10
    WHERE codigo IS NULL
)
UPDATE ga_consolidado_s10 c
SET    anio   = n.anio,
       numero = n.numero,
       codigo = 'CON-' || n.anio || '-' || LPAD(n.numero::text, 4, '0')
FROM   numerados n
WHERE  c.id = n.id;

-- Únicos parciales por state: la regla de la casa (varias filas con state=false, una sola
-- viva). Es lo que permite que al reemplazar el documento el nuevo herede el código del
-- que se da de baja.
CREATE UNIQUE INDEX IF NOT EXISTS ux_ga_consolidado_s10_codigo
    ON ga_consolidado_s10 (codigo)
    WHERE state AND codigo IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_ga_consolidado_s10_anio_numero
    ON ga_consolidado_s10 (anio, numero)
    WHERE state AND anio IS NOT NULL AND numero IS NOT NULL;

ALTER TABLE ga_consolidado_s10
    DROP CONSTRAINT IF EXISTS chk_ga_consolidado_s10_codigo;
ALTER TABLE ga_consolidado_s10
    ADD CONSTRAINT chk_ga_consolidado_s10_codigo
    CHECK (codigo IS NULL OR btrim(codigo) <> '');
