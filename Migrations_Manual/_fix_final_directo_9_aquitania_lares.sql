-- Ejecuta este archivo COMPLETO de una sola vez. Sin BEGIN/COMMIT: cada
-- UPDATE se autocommitea solo al ejecutarse. Repite los 9 que el chequeo
-- global mostró como TODAVÍA desfasados (a pesar de intentos anteriores).

UPDATE ss_hab_trabajador SET vigencia = '2027-01-31', updated_at = now() WHERE id = 213770; -- ALFARO ARRASCUE ALBANIA NAURU
UPDATE ss_hab_trabajador SET vigencia = '2027-01-31', updated_at = now() WHERE id = 212283; -- BACA ARIAS SCOOT ADERLI
UPDATE ss_hab_trabajador SET vigencia = '2027-01-31', updated_at = now() WHERE id = 213003; -- COMECA LOJA JOSSELYN ADELITA
UPDATE ss_hab_trabajador SET vigencia = '2027-01-31', updated_at = now() WHERE id = 203007; -- GAMARRA VALENCIA ANA LUCIA
UPDATE ss_hab_trabajador SET vigencia = '2027-01-31', updated_at = now() WHERE id = 213786; -- PALACIOS FHON CARLOS JOSE
UPDATE ss_hab_trabajador SET vigencia = '2027-01-31', updated_at = now() WHERE id = 213245; -- PRECIADO ELORRIAGA FERNANDO
UPDATE ss_hab_trabajador SET vigencia = '2027-01-31', updated_at = now() WHERE id = 211759; -- SANTA CRUZ VALLEJOS LEYDI ROXANA
UPDATE ss_hab_trabajador SET vigencia = '2026-08-31', updated_at = now() WHERE id = 218504; -- ABAD NAUTO JOHAN PIERO
UPDATE ss_hab_trabajador SET vigencia = '2026-08-31', updated_at = now() WHERE id = 202265; -- COLLANTES ABANTO MARCO ANTONIO

SELECT id, worker_id, archivo_url, vigencia, updated_at
FROM ss_hab_trabajador
WHERE id IN (213770, 212283, 213003, 203007, 213786, 213245, 211759, 218504, 202265)
ORDER BY id;
