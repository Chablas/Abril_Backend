-- Separa, para cada driver (HH / TRABAJADORES), el valor CALCULADO (desde Tareo/Excel de
-- planilla o worker_vinculaciones, "en vivo") del valor MANUAL FINAL (lo que el responsable
-- tipeó en Datos Base -- project.hh_total_casa / project.cant_trabajadores_casa -- típicamente
-- cuando el proyecto ya cerró y el real definitivo ya se conocía por otra vía).
--
-- cantidad / ratio se mantienen como el valor "oficial" (el que entra a la mediana): manual si
-- existe, si no el calculado. cantidad_calculado guarda siempre el crudo de Tareo/vinculaciones
-- para que quede visible aunque haya manual. cantidad_manual queda NULL cuando el proyecto
-- todavía no tiene ese dato cargado en Datos Base.

ALTER TABLE ss_ratio_proyecto_driver
    ADD COLUMN IF NOT EXISTS cantidad_calculado numeric,
    ADD COLUMN IF NOT EXISTS cantidad_manual numeric;

-- Backfill: todo lo que hay hoy en "cantidad" viene del cálculo automático (no existía
-- todavía la prioridad del manual), así que se copia tal cual a cantidad_calculado.
UPDATE ss_ratio_proyecto_driver
SET cantidad_calculado = cantidad
WHERE cantidad_calculado IS NULL;

ALTER TABLE ss_ratio_proyecto_driver
    ALTER COLUMN cantidad_calculado SET NOT NULL;
