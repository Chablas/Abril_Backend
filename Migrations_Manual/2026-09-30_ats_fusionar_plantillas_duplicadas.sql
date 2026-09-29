-- Fusiona plantillas de ATS duplicadas por tildes/mayúsculas (ej. "ATS Acarreo De Vidrios" vs
-- "ATS Acarreo de Vidrios") — la migración anterior (2026-09-28) comparó nombres con IGUALDAD
-- EXACTA, así que las que ya existían con otra tilde/capitalización no matchearon y se insertaron
-- de nuevo. Se conserva SIEMPRE el id más chico (el que ya existía antes de esa migración) y se
-- mueven ahí todos los mapeos del duplicado antes de borrarlo.

CREATE TEMP TABLE mapeo_fusion AS
WITH normalizados AS (
    SELECT id,
        lower(regexp_replace(translate(nombre, 'áéíóúÁÉÍÓÚñÑ', 'aeiouAEIOUnN'), '\s+', ' ', 'g')) AS norm
    FROM ss_ats_plantilla
),
grupos AS (
    SELECT norm, min(id) AS keep_id, array_agg(id) AS todos
    FROM normalizados
    GROUP BY norm
    HAVING count(*) > 1
)
SELECT g.keep_id, unnest(g.todos) AS dup_id
FROM grupos g;

DELETE FROM mapeo_fusion WHERE dup_id = keep_id;

-- Peligros
INSERT INTO ss_ats_plantilla_peligro (plantilla_id, peligro_id)
SELECT m.keep_id, pp.peligro_id
FROM ss_ats_plantilla_peligro pp
JOIN mapeo_fusion m ON m.dup_id = pp.plantilla_id
WHERE NOT EXISTS (SELECT 1 FROM ss_ats_plantilla_peligro x WHERE x.plantilla_id = m.keep_id AND x.peligro_id = pp.peligro_id);

DELETE FROM ss_ats_plantilla_peligro pp USING mapeo_fusion m WHERE pp.plantilla_id = m.dup_id;

-- EPP
INSERT INTO ss_ats_plantilla_epp (plantilla_id, epp_id)
SELECT m.keep_id, pe.epp_id
FROM ss_ats_plantilla_epp pe
JOIN mapeo_fusion m ON m.dup_id = pe.plantilla_id
WHERE NOT EXISTS (SELECT 1 FROM ss_ats_plantilla_epp x WHERE x.plantilla_id = m.keep_id AND x.epp_id = pe.epp_id);

DELETE FROM ss_ats_plantilla_epp pe USING mapeo_fusion m WHERE pe.plantilla_id = m.dup_id;

-- Herramientas
INSERT INTO ss_ats_plantilla_herramienta (plantilla_id, herramienta_id)
SELECT m.keep_id, ph.herramienta_id
FROM ss_ats_plantilla_herramienta ph
JOIN mapeo_fusion m ON m.dup_id = ph.plantilla_id
WHERE NOT EXISTS (SELECT 1 FROM ss_ats_plantilla_herramienta x WHERE x.plantilla_id = m.keep_id AND x.herramienta_id = ph.herramienta_id);

DELETE FROM ss_ats_plantilla_herramienta ph USING mapeo_fusion m WHERE ph.plantilla_id = m.dup_id;

-- Puestos que autosugieren la plantilla
INSERT INTO ss_ats_plantilla_puesto (plantilla_id, puesto_id)
SELECT m.keep_id, pu.puesto_id
FROM ss_ats_plantilla_puesto pu
JOIN mapeo_fusion m ON m.dup_id = pu.plantilla_id
WHERE NOT EXISTS (SELECT 1 FROM ss_ats_plantilla_puesto x WHERE x.plantilla_id = m.keep_id AND x.puesto_id = pu.puesto_id);

DELETE FROM ss_ats_plantilla_puesto pu USING mapeo_fusion m WHERE pu.plantilla_id = m.dup_id;

-- Actividades/pasos de la plantilla duplicada — se reasignan al id que se conserva
UPDATE ss_ats_plantilla_actividad a SET plantilla_id = m.keep_id
FROM mapeo_fusion m WHERE a.plantilla_id = m.dup_id;

-- ATS ya firmados/en borrador que apuntaban a la plantilla duplicada
UPDATE ss_ats a SET plantilla_id = m.keep_id
FROM mapeo_fusion m WHERE a.plantilla_id = m.dup_id;

-- Por último, borrar los registros de plantilla duplicados
DELETE FROM ss_ats_plantilla WHERE id IN (SELECT dup_id FROM mapeo_fusion);

DROP TABLE mapeo_fusion;
