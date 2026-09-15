-- Vigencia confirmada abriendo el PDF 136115_(3)._JUNIO.pdf (constancia
-- 1224067, Pacífico Seguros): "vigencia 01 de Junio del 2026 al 31 de Enero
-- del 2027" -> 2027-01-31. Cubre a SANTA CRUZ VALLEJOS (ya corregida),
-- SCHOLZ LLAQUE y SOLLER CHAVEZ (estas dos, pendientes).

UPDATE ss_hab_trabajador
SET vigencia = '2027-01-31', updated_at = now()
WHERE id = 186965;  -- SCHOLZ LLAQUE SAMANTA ELSA

UPDATE ss_hab_trabajador
SET vigencia = '2027-01-31', updated_at = now()
WHERE id = 210728;  -- SOLLER CHAVEZ JUAN CARLOS

SELECT id, worker_id, archivo_url, vigencia, updated_at
FROM ss_hab_trabajador
WHERE id IN (186965, 210728);
