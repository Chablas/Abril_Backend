-- =====================================================================
-- Baja DEFINITIVA de la tabla workers_ficha_fusionada
-- Fecha: 2026-09-08
-- Archivo ASCII puro (sin tildes) a proposito: se pega inline con psql -c.
--
-- QUE ERA: rastro de auditoria que dejo la migracion
-- 2026-08-25_workers_fusion_fichas_duplicadas.sql. Decia que ficha
-- duplicada de `workers` se dio de baja (worker_id_eliminado) y contra
-- que ficha viva (worker_id_canonico).
--
-- POR QUE SE PUEDE BORRAR: no la lee NADIE en la aplicacion. No tiene
-- entidad de EF ni DbSet, no aparece en SQL crudo de Dapper, no la usa
-- el frontend y la BD no la referencia (0 FK entrantes, 0 vistas,
-- 0 funciones, 0 triggers). Solo la mencionan dos comentarios de codigo
-- y scripts historicos de Migrations_Manual.
--
-- QUE SE PIERDE: el puente desde las ~2,390 filas de historial que
-- siguen colgando de los worker_id dados de baja (ss_hab_trabajador,
-- ss_sctr_vidaley_worker, worker_eventos, worker_vinculaciones,
-- worker_emos, ss_induccion, ...) hacia la ficha viva de esa persona.
-- El mapa se puede rehacer desde `workers` (state = false + person_id ->
-- la unica ficha state = true de esa person) para 126 de las 128 filas.
-- Las otras dos son personas que HOY tienen DOS filas con state = true: a
-- la canonica de la fusion se le sumo una ficha nueva en setiembre, asi
-- que sin esta tabla no se sabria contra cual de las dos se fusiono:
--     person 610  LUYO SANABRIA  : 13298 (baja) -> 14566, y la otra viva es 15467 (creada 2026-09-01)
--     person 2061 OBREGON VASQUEZ: 12334 (baja) -> 15195, y la otra viva es 15546 (creada 2026-09-04)
-- Si se quiere conservar el dato, exportarlo antes con:
--     \copy workers_ficha_fusionada TO 'wff_backup.csv' CSV HEADER
-- =====================================================================

BEGIN;

-- ---------------------------------------------------------------------
-- Guarda: si algo apunta a la tabla, abortar con el nombre exacto en vez
-- de dejar que el DROP falle (o peor, que un CASCADE se lleve por delante
-- algo que se agrego despues de escribir esto).
-- ---------------------------------------------------------------------
DO $guarda$
DECLARE
    v_oid       oid;
    v_pendiente text := '';
    r           record;
BEGIN
    v_oid := to_regclass('public.workers_ficha_fusionada');
    IF v_oid IS NULL THEN
        RAISE EXCEPTION 'workers_ficha_fusionada no existe: nada que borrar (ya se corrio este script?).';
    END IF;

    FOR r IN
        SELECT 'FK entrante: ' || conrelid::regclass::text || '.' || conname AS que
          FROM pg_constraint WHERE contype = 'f' AND confrelid = v_oid
        UNION ALL
        SELECT 'Vista/matview: ' || c.relname
          FROM pg_class c
          JOIN pg_rewrite r2 ON r2.ev_class = c.oid
          JOIN pg_depend  d  ON d.objid = r2.oid
         WHERE d.refobjid = v_oid AND c.oid <> v_oid
         GROUP BY c.relname
        UNION ALL
        SELECT 'Funcion: ' || n.nspname || '.' || p.proname
          FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
         WHERE n.nspname NOT IN ('pg_catalog', 'information_schema')
           AND p.prosrc ILIKE '%workers_ficha_fusionada%'
        UNION ALL
        SELECT 'Trigger: ' || tgname
          FROM pg_trigger WHERE tgrelid = v_oid AND NOT tgisinternal
    LOOP
        v_pendiente := v_pendiente || E'\n  - ' || r.que;
    END LOOP;

    IF v_pendiente <> '' THEN
        RAISE EXCEPTION 'Algo depende de workers_ficha_fusionada, no se borra nada:%', v_pendiente;
    END IF;
END
$guarda$;

-- ---------------------------------------------------------------------
-- La tabla. La secuencia workers_ficha_fusionada_..._id_seq es OWNED BY
-- la columna serial, asi que se va sola con el DROP (sin CASCADE).
-- ---------------------------------------------------------------------
DROP TABLE public.workers_ficha_fusionada;

-- ---------------------------------------------------------------------
-- El comentario de workers.state apuntaba a la tabla que acaba de morir:
-- se reescribe para que no quede una referencia colgada en el esquema.
-- ---------------------------------------------------------------------
COMMENT ON COLUMN workers.state IS
    'Soft delete. false = ficha eliminada, no se muestra en ninguna pantalla (filtro global de EF en AppDbContext). Se usa para las fichas duplicadas que existian por el modelo viejo de fecha_ingreso/fecha_retiro: al reingresar se abria una ficha nueva en vez de un periodo nuevo. El historial de esas fichas (EMOs, inducciones, habilitacion, vinculaciones) sigue colgando del id dado de baja: para llegar a el desde la ficha viva, buscar por person_id con IgnoreQueryFilters().';

COMMIT;

-- ---------------------------------------------------------------------
-- Verificacion (correr aparte, despues del COMMIT):
--   SELECT to_regclass('public.workers_ficha_fusionada') AS debe_ser_null;
-- ---------------------------------------------------------------------
