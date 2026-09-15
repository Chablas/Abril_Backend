-- Vigencia confirmada abriendo cada PDF directamente desde la app.
UPDATE ss_hab_trabajador SET vigencia = '2027-01-31', updated_at = now() WHERE id = 195665; -- YSASI ZARATE JOYCE VANIA (poliza 397)
UPDATE ss_hab_trabajador SET vigencia = '2027-02-01', updated_at = now() WHERE id = 201313; -- AVILA GUERRERO CESAR GUILLERMO (poliza 394)
UPDATE ss_hab_trabajador SET vigencia = '2027-01-31', updated_at = now() WHERE id = 210752; -- ASENCIOS QUISPE MANUEL GILDER (poliza 619)
UPDATE ss_hab_trabajador SET vigencia = '2027-01-31', updated_at = now() WHERE id = 203806; -- SANCHEZ VALDIVIA KIMBERLY NICOLE (poliza 429)
UPDATE ss_hab_trabajador SET vigencia = '2027-01-31', updated_at = now() WHERE id = 206955; -- GOMEZ MEJIA FABIAN ANTHONY (poliza 429)
UPDATE ss_hab_trabajador SET vigencia = '2027-01-31', updated_at = now() WHERE id = 203978; -- CASTAÑEDA BARRIONUEVO ELIZABETH SORELLI (poliza 393)
UPDATE ss_hab_trabajador SET vigencia = '2027-01-31', updated_at = now() WHERE id = 205496; -- VEGA BALDEON GERALDINE BRIGHITE (poliza 393)
UPDATE ss_hab_trabajador SET vigencia = '2027-01-31', updated_at = now() WHERE id = 204943; -- DAZA HUAYHUAS LADY NATALY (poliza 395)

SELECT id, worker_id, archivo_url, vigencia, updated_at
FROM ss_hab_trabajador
WHERE id IN (195665, 201313, 210752, 203806, 206955, 203978, 205496, 204943)
ORDER BY id;
