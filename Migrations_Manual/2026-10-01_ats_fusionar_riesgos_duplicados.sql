-- Fusiona riesgos duplicados dentro del MISMO peligro por tildes/mayúsculas/typos del Excel
-- de origen (ej. "Atrición de dedos" vs "Atricción de dedos", "Tropiezos y caidas" vs
-- "Tropiezos y caídas") — la migración 2026-09-28 comparó texto con IGUALDAD EXACTA contra lo
-- ya seedeado, así que las variantes con otra tilde no matchearon y quedaron como riesgos nuevos
-- separados bajo el mismo peligro. Se conserva el id más chico (el que ya existía) y se mueven
-- ahí ss_ats_riesgo_detalle / ss_ats_riesgo_control del duplicado; requiere_petar se combina con OR.

CREATE TEMP TABLE mapeo_riesgo AS
WITH normalizados AS (
    SELECT id, peligro_id,
        lower(regexp_replace(translate(nombre, 'áéíóúÁÉÍÓÚñÑ', 'aeiouAEIOUnN'), '\s+', ' ', 'g')) AS norm
    FROM ss_ats_riesgo
),
grupos AS (
    SELECT peligro_id, norm, min(id) AS keep_id, array_agg(id) AS todos
    FROM normalizados
    GROUP BY peligro_id, norm
    HAVING count(*) > 1
)
SELECT g.keep_id, unnest(g.todos) AS dup_id
FROM grupos g;

DELETE FROM mapeo_riesgo WHERE dup_id = keep_id;

-- requiere_petar: si CUALQUIERA de los duplicados lo tenía marcado, el que se conserva lo hereda.
UPDATE ss_ats_riesgo r SET requiere_petar = true
FROM mapeo_riesgo m
WHERE r.id = m.keep_id
  AND EXISTS (SELECT 1 FROM ss_ats_riesgo d WHERE d.id = m.dup_id AND d.requiere_petar = true);

-- Detalle de ATS ya guardados que apuntaban al riesgo duplicado
UPDATE ss_ats_riesgo_detalle d SET riesgo_id = m.keep_id
FROM mapeo_riesgo m WHERE d.riesgo_id = m.dup_id;

-- Controles sugeridos del riesgo duplicado
UPDATE ss_ats_riesgo_control c SET riesgo_id = m.keep_id
FROM mapeo_riesgo m WHERE c.riesgo_id = m.dup_id;

DELETE FROM ss_ats_riesgo WHERE id IN (SELECT dup_id FROM mapeo_riesgo);

DROP TABLE mapeo_riesgo;
