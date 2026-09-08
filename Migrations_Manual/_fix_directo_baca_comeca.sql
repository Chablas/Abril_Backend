-- Ejecuta este archivo COMPLETO de una sola vez (botón "Execute", no
-- selección parcial). Sin BEGIN/COMMIT manual — deja que autocommit de
-- pgAdmin haga el commit solo, para evitar el problema de transacciones
-- colgadas de los scripts anteriores.

UPDATE ss_hab_trabajador
SET vigencia = '2027-01-31', updated_at = now()
WHERE id = 212283;  -- BACA ARIAS SCOOT ADERLI

UPDATE ss_hab_trabajador
SET vigencia = '2027-01-31', updated_at = now()
WHERE id = 213003;  -- COMECA LOJA JOSSELYN ADELITA

-- Verificación inmediata, en la misma ejecución:
SELECT id, worker_id, item_id, archivo_url, vigencia, updated_at
FROM ss_hab_trabajador
WHERE id IN (212283, 213003);
