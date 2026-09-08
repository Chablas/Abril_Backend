-- =====================================================================
-- workers: una sola ficha viva (state = true) por person   -- PARA PROD
-- Fecha: 2026-09-08
-- Archivo ASCII puro (sin tildes) a proposito: se pega inline con psql -c.
--
-- QUE HACE: un UNIQUE INDEX parcial sobre workers (person_id) que solo
-- mira las fichas vivas. A partir de aca, abrir una segunda ficha viva
-- para la misma person se cae con 23505.
--
-- POR QUE LLEVA UNA LISTA DE IDS: un indice unico se valida entero al
-- construirse y no se puede marcar NOT VALID (Postgres solo acepta
-- NOT VALID en CHECK y FOREIGN KEY). Como al 2026-09-08 prod ya tiene 43
-- fichas de mas repartidas en 41 persons, sin exceptuarlas el CREATE
-- INDEX abortaria. La unica forma de decir "estas filas no cuentan" es
-- justamente el WHERE del indice parcial.
--
-- COMO SE ELIGIO CUAL SE QUEDA DENTRO: por cada person duplicada se
-- conserva la ficha vigente (esta_adentro, llego_a_ingresar y, a
-- igualdad, la mas reciente: el mismo criterio que uso la fusion del
-- 2026-08-25) y se exceptuan las demas. La conservada es la que va a
-- chocar contra cualquier ficha nueva de esa person.
--
-- LA LISTA ES DE PROD Y TIENE FECHA. En prod aparecen fichas duplicadas
-- nuevas casi todos los dias. Si entre que se genero esta lista y el
-- momento de correrla aparecio una mas, el CREATE INDEX aborta con
-- "llave duplicada ... Ya existe la llave (person_id)=(N)" y no se crea
-- nada: eso ES la red de seguridad. En ese caso, regenerar la lista con
-- la consulta del final del archivo y volver a correr.
--
-- EN DEV NO CORRER ESTE ARCHIVO: dev no tiene duplicados, asi que le
-- corresponde la version sin lista (la del final). Estos 43 ids son ids
-- de prod y en dev apuntarian a trabajadores cualquiera.
--
-- LIMITACION CONOCIDA: una fila exceptuada es invisible para el indice
-- para siempre. Si a una de esas 41 persons se le da de baja justo la
-- ficha que quedo DENTRO del indice, la exceptuada queda sola y sin
-- guardia: recien ahi se le podria volver a abrir una ficha nueva. El
-- agujero se cierra el dia que se limpien los duplicados y se recree el
-- indice sin lista.
-- =====================================================================

DROP INDEX IF EXISTS ux_workers_person_id_ficha_viva;

CREATE UNIQUE INDEX ux_workers_person_id_ficha_viva
    ON workers (person_id)
 WHERE state
   AND person_id IS NOT NULL
   AND id <> ALL (ARRAY[
        12248, 12327, 12420, 12448, 12493, 12643, 12659, 12691, 12840, 12891,
        12919, 12927, 13096, 13132, 13186, 13394, 13465, 13488, 13542, 13646,
        13737, 13770, 13803, 13809, 13864, 13873, 13944, 13977, 14037, 14078,
        14525, 14526, 14533, 14566, 15195, 15313, 15318, 15324, 15409, 15434,
        15443, 15445, 15549]);

COMMENT ON INDEX ux_workers_person_id_ficha_viva IS
    'Una person no puede tener dos fichas con state = true. Los 43 ids del predicado son duplicados que YA existian al crear el indice (2026-09-08) y quedan exceptuados para no tener que darlos de baja. Cuando se limpien, recrear el indice sin la lista.';

-- =====================================================================
-- Verificacion (despues de correrlo):
--
--   SELECT indexdef FROM pg_indexes
--    WHERE tablename = 'workers' AND indexname = 'ux_workers_person_id_ficha_viva';
--
-- ---------------------------------------------------------------------
-- REGENERAR LA LISTA (si el CREATE INDEX aborta por un duplicado nuevo):
--
--   SELECT string_agg(id::text, ', ' ORDER BY id)
--     FROM (SELECT w.id,
--                  row_number() OVER (
--                      PARTITION BY w.person_id
--                      ORDER BY we.esta_adentro     DESC NULLS LAST,
--                               we.llego_a_ingresar DESC NULLS LAST,
--                               w.id                DESC) AS rn
--             FROM workers w
--             LEFT JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
--            WHERE w.state AND w.person_id IS NOT NULL) t
--    WHERE t.rn > 1;
--
-- ---------------------------------------------------------------------
-- VERSION SIN LISTA (la que corresponde en dev, y en prod el dia que se
-- limpien los duplicados). Cierra el agujero de las filas exceptuadas:
--
--   DROP INDEX IF EXISTS ux_workers_person_id_ficha_viva;
--   CREATE UNIQUE INDEX ux_workers_person_id_ficha_viva
--       ON workers (person_id)
--    WHERE state AND person_id IS NOT NULL;
-- =====================================================================
