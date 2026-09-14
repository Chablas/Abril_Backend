-- ============================================================================
-- Gestión Administrativa · Salidas — «número de guía» pasa a «número de reembolso»
-- Fecha: 2026-09-14
--
-- Homologación de vocabulario: el dato que el S10 devuelve al registrar las
-- planillas se llamaba «número de guía» en la UI, en el código y en la base.
-- Pasa a llamarse «número de reembolso» (del S10) en las cuatro pantallas que
-- lo muestran (Rendiciones, Gestión de Rendiciones, Reembolsos y Correcciones
-- S10) y en los correos. Este script alinea la base con ese nombre.
--
--   ga_consolidado_s10.numero_guia  → numero_reembolso
--   ga_correccion_s10.numero_guia   → numero_reembolso
--   ga_correccion_s10.guia_anulada  → numero_reembolso_anulado
--
-- Es SOLO un renombre: no se crea ni se borra ninguna columna, no se pierde
-- ninguna fila y los tipos, NOT NULL y DEFAULT se conservan tal cual. La
-- columna vieja no queda como copia porque no hay dato que auditar — el
-- contenido es el mismo y viaja con la columna.
--
-- El CHECK de ga_consolidado_s10 se renombra junto con su columna: Postgres
-- reescribe solo la definición, pero el nombre del constraint quedaría
-- diciendo "guia" para siempre.
--
-- Idempotente: se puede correr varias veces. Aplicar en dev y prod.
--
-- ⚠ Correr JUNTO con el deploy del backend: el modelo EF pasa a mapear
--   NumeroReembolso → numero_reembolso, así que la versión vieja del backend
--   no funciona contra el esquema nuevo ni la nueva contra el viejo.
-- ============================================================================

BEGIN;

-- ── 1. ga_consolidado_s10 ───────────────────────────────────────────────────

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'ga_consolidado_s10' AND column_name = 'numero_guia'
    ) AND NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'ga_consolidado_s10' AND column_name = 'numero_reembolso'
    ) THEN
        ALTER TABLE ga_consolidado_s10 RENAME COLUMN numero_guia TO numero_reembolso;
    END IF;

    IF EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid = 'ga_consolidado_s10'::regclass
          AND conname  = 'chk_ga_consolidado_s10_numero_guia'
    ) THEN
        ALTER TABLE ga_consolidado_s10
            RENAME CONSTRAINT chk_ga_consolidado_s10_numero_guia
                           TO chk_ga_consolidado_s10_numero_reembolso;
    END IF;
END $$;

COMMENT ON COLUMN ga_consolidado_s10.numero_reembolso IS
    'Numero de reembolso que devuelve el S10 al registrar las planillas. Es texto: no es un '
    'correlativo nuestro y puede traer letras y separadores. NULL solo en los consolidados '
    'subidos antes de que el formulario pidiera el dato. Se llamo numero_guia hasta 2026-09-14.';

-- ── 2. ga_correccion_s10 ────────────────────────────────────────────────────

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'ga_correccion_s10' AND column_name = 'numero_guia'
    ) AND NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'ga_correccion_s10' AND column_name = 'numero_reembolso'
    ) THEN
        ALTER TABLE ga_correccion_s10 RENAME COLUMN numero_guia TO numero_reembolso;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'ga_correccion_s10' AND column_name = 'guia_anulada'
    ) AND NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'ga_correccion_s10' AND column_name = 'numero_reembolso_anulado'
    ) THEN
        ALTER TABLE ga_correccion_s10 RENAME COLUMN guia_anulada TO numero_reembolso_anulado;
    END IF;
END $$;

COMMENT ON COLUMN ga_correccion_s10.numero_reembolso IS
    'Numero de reembolso del consolidado observado, copiado al solicitar. Es el dato con el que '
    'el ERP ubica el registro en el S10. Se llamo numero_guia hasta 2026-09-14.';

COMMENT ON COLUMN ga_correccion_s10.numero_reembolso_anulado IS
    'True cuando el ERP anulo el registro en vez de corregirlo: hace falta un numero de reembolso '
    'NUEVO y el anterior queda bloqueado al recargar el consolidado (HU-ERP-03 / CA-19). '
    'Se llamo guia_anulada hasta 2026-09-14.';

-- ── 3. Guarda: nada quedo a medias ──────────────────────────────────────────
DO $$
DECLARE
    faltan text;
BEGIN
    SELECT string_agg(t || '.' || c, ', ')
      INTO faltan
      FROM (VALUES
            ('ga_consolidado_s10', 'numero_reembolso'),
            ('ga_correccion_s10',  'numero_reembolso'),
            ('ga_correccion_s10',  'numero_reembolso_anulado')
           ) AS esperado(t, c)
     WHERE NOT EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_name = esperado.t AND column_name = esperado.c);

    IF faltan IS NOT NULL THEN
        RAISE EXCEPTION 'El renombre no se completo: faltan %', faltan;
    END IF;
END $$;

COMMIT;
