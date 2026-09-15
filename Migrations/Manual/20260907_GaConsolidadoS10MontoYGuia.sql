-- ============================================================================
-- Gestión Administrativa · Salidas — Monto y número de guía del Consolidado S10
-- Fecha: 2026-09-07
--
-- Hasta hoy adjuntar el Consolidado del S10 era solo subir el PDF. Ahora el
-- formulario también pide los dos datos con los que el S10 lo registró:
--
--   • monto_total → el importe total del consolidado. Tiene que COINCIDIR con
--     el monto de la planilla que se está rindiendo (la suma de TODAS sus
--     salidas, que es lo que cubre el consolidado); si no coincide, el backend
--     rechaza la subida y el formulario no deja avanzar.
--   • numero_guia → el número de guía, que es TEXTO: no es un correlativo
--     nuestro sino el que devuelve el S10, y puede traer letras y separadores.
--
-- Las dos columnas quedan NULLABLE a propósito: los consolidados que ya están
-- subidos no tienen esos datos capturados y no hay valor cierto con el que
-- rellenarlos — inventar un 0 o un '-' sería ensuciar la auditoría. Para las
-- filas NUEVAS los exige el servicio (ConsolidadoS10Service), no la base.
--
-- Idempotente: se puede correr varias veces. Aplicar en dev y prod.
-- ============================================================================

BEGIN;

ALTER TABLE ga_consolidado_s10
    ADD COLUMN IF NOT EXISTS monto_total numeric(12,2),
    ADD COLUMN IF NOT EXISTS numero_guia varchar(60);

COMMENT ON COLUMN ga_consolidado_s10.monto_total IS
    'Importe total del consolidado del S10. Debe coincidir con el monto de la planilla '
    'completa (todas sus salidas). NULL solo en los consolidados subidos antes de que el '
    'formulario pidiera el dato.';

COMMENT ON COLUMN ga_consolidado_s10.numero_guia IS
    'Número de guía que devuelve el S10. Es texto: puede traer letras y separadores. '
    'NULL solo en los consolidados subidos antes de que el formulario pidiera el dato.';

-- Los dos CHECK admiten NULL (las filas viejas) pero no un valor sin sentido.
-- Pasan de inmediato sobre lo que ya está cargado: un CHECK no se evalúa sobre
-- NULL, así que no hace falta NOT VALID.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid = 'ga_consolidado_s10'::regclass
          AND conname  = 'chk_ga_consolidado_s10_monto_total'
    ) THEN
        ALTER TABLE ga_consolidado_s10
            ADD CONSTRAINT chk_ga_consolidado_s10_monto_total
            CHECK (monto_total IS NULL OR monto_total > 0);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid = 'ga_consolidado_s10'::regclass
          AND conname  = 'chk_ga_consolidado_s10_numero_guia'
    ) THEN
        ALTER TABLE ga_consolidado_s10
            ADD CONSTRAINT chk_ga_consolidado_s10_numero_guia
            CHECK (numero_guia IS NULL OR btrim(numero_guia) <> '');
    END IF;
END $$;

COMMIT;
