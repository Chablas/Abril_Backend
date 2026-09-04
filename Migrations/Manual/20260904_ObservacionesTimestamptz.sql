-- Migración manual (pgAdmin) — ac_observaciones / ac_observacion_fotos a TIMESTAMPTZ.
-- Ejecutar directamente contra la BD PostgreSQL. No usar dotnet ef.
--
-- Las dos tablas se crearon en 20260713_AddAcObservaciones.sql con TIMESTAMP (sin zona), pero el
-- modelo EF mapea DateTime a "timestamp with time zone" por defecto. Mientras nadie filtró por
-- fechas el desajuste no se notó; el primer filtro desde/hasta tiró 500 ("Cannot write DateTime
-- with Kind=Unspecified to PostgreSQL type 'timestamp with time zone'"). El estándar del proyecto
-- es timestamptz guardando UTC, así que se alinea la BD en vez de degradar el modelo.
--
-- El USING no es opcional y NO es el mismo para todas las columnas:
--
--   * fecha y plazo_levantamiento son FECHAS DE CALENDARIO (las 6938 filas están a 00:00:00): las
--     eligió una persona en Lima en un input de fecha, así que esa medianoche es medianoche de
--     Lima -> AT TIME ZONE 'America/Lima'. Interpretarlas como UTC las dejaría en 00:00Z y, al
--     servirlas al frontend en UTC-5, TODAS las observaciones mostrarían el día anterior.
--
--   * created_at y fecha_levantamiento son INSTANTES escritos con DateTime.UtcNow (y el DEFAULT
--     era now() AT TIME ZONE 'utc'), o sea que el valor guardado ya es hora UTC -> AT TIME ZONE 'UTC'.
--
-- Sin USING explícito Postgres usaría el TimeZone del servidor, que es Etc/UTC en la VPS pero
-- America/Bogota en la BD local: el mismo script daría resultados distintos en cada ambiente.
--
-- Re-ejecutable: si las columnas ya son timestamptz los bloques no hacen nada.

DO $$
BEGIN
    IF (SELECT data_type FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'ac_observaciones' AND column_name = 'fecha')
       = 'timestamp without time zone'
    THEN
        ALTER TABLE ac_observaciones
            ALTER COLUMN fecha               TYPE timestamptz USING fecha               AT TIME ZONE 'America/Lima',
            ALTER COLUMN plazo_levantamiento TYPE timestamptz USING plazo_levantamiento AT TIME ZONE 'America/Lima',
            ALTER COLUMN created_at          TYPE timestamptz USING created_at          AT TIME ZONE 'UTC',
            ALTER COLUMN fecha_levantamiento TYPE timestamptz USING fecha_levantamiento AT TIME ZONE 'UTC';

        -- El DEFAULT era (now() AT TIME ZONE 'utc'), que devuelve TIMESTAMP sin zona: al castearse
        -- a la columna nueva volvería a depender del TimeZone del servidor. now() ya es timestamptz.
        ALTER TABLE ac_observaciones ALTER COLUMN created_at SET DEFAULT now();

        -- Guarda: si la conversión no dejó cada fecha a medianoche de Lima, algo se interpretó mal
        -- y es preferible abortar (el DO block va en transacción, así que revierte los ALTER).
        IF EXISTS (SELECT 1 FROM ac_observaciones
                   WHERE (fecha AT TIME ZONE 'America/Lima')::time <> '00:00:00'
                      OR (plazo_levantamiento IS NOT NULL
                          AND (plazo_levantamiento AT TIME ZONE 'America/Lima')::time <> '00:00:00'))
        THEN
            RAISE EXCEPTION 'La conversion de ac_observaciones no dejo las fechas a medianoche de Lima; se aborta sin tocar nada.';
        END IF;
    END IF;

    IF (SELECT data_type FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'ac_observacion_fotos' AND column_name = 'created_at')
       = 'timestamp without time zone'
    THEN
        ALTER TABLE ac_observacion_fotos
            ALTER COLUMN created_at TYPE timestamptz USING created_at AT TIME ZONE 'UTC';

        ALTER TABLE ac_observacion_fotos ALTER COLUMN created_at SET DEFAULT now();
    END IF;
END $$;

-- Verificación (debe devolver las 5 columnas como "timestamp with time zone"):
--
-- SELECT table_name, column_name, data_type, column_default
-- FROM information_schema.columns
-- WHERE table_schema = 'public'
--   AND table_name IN ('ac_observaciones', 'ac_observacion_fotos')
--   AND data_type LIKE 'timestamp%'
-- ORDER BY table_name, ordinal_position;
