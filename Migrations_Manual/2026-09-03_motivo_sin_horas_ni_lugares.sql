-- ============================================================================
-- 2026-09-03 · Motivos de salida que NO piden horas, lugares ni trayectos
--
-- Nace de "Licencia sin goce de haber": es una ausencia de día completo, no un
-- desplazamiento. No tiene hora de salida/retorno, no tiene origen ni destino y
-- nunca son varios trayectos. En vez de hardcodear ese motivo, se agrega un
-- interruptor por motivo en Configuración → Motivos, igual que los otros tres
-- (requiere_adjunto, es_hora_estimada, requiere_motivo_adicional).
--
--   ga_motivo_salida.pide_horas_lugares
--     true  (default)  → comportamiento de siempre: el formulario pide hora de
--                        salida/retorno, origen y destino, y deja agregar más
--                        trayectos.
--     false            → el formulario oculta horas y lugares y no deja agregar
--                        trayectos: la solicitud queda con UN solo trayecto que
--                        solo lleva el motivo.
--
-- Por eso ga_solicitud_trayecto.hora_salida deja de ser NOT NULL: es la única
-- columna del trayecto que todavía lo era. hora_retorno, lugar_origen_id/libre y
-- lugar_destino_id/libre ya eran nulables.
--
-- No se toca ninguna fila existente: el DEFAULT true deja a los 15 motivos
-- actuales exactamente como estaban. El motivo "L.s.g.h" se desmarca desde la
-- pantalla de Configuración → Motivos (Editar), no hace falta SQL.
--
-- Idempotente: se puede correr más de una vez.
-- ============================================================================

BEGIN;

-- ── 1. El interruptor en el catálogo de motivos ─────────────────────────────
ALTER TABLE ga_motivo_salida
    ADD COLUMN IF NOT EXISTS pide_horas_lugares boolean NOT NULL DEFAULT true;

COMMENT ON COLUMN ga_motivo_salida.pide_horas_lugares IS
    'Si false, al elegir este motivo la solicitud no pide horas, ni lugares, ni '
    'trayectos adicionales (ej. Licencia sin goce de haber). Default true = '
    'comportamiento normal de una salida.';

-- ── 2. hora_salida deja de ser obligatoria ──────────────────────────────────
-- Los trayectos de un motivo con pide_horas_lugares = false se guardan sin hora.
ALTER TABLE ga_solicitud_trayecto
    ALTER COLUMN hora_salida DROP NOT NULL;

-- ── 3. Guarda: no puede haber retorno sin salida ────────────────────────────
-- Un trayecto o declara sus dos horas (o salida + "sin retorno"), o no declara
-- ninguna. Tener hora_retorno con hora_salida en NULL sería data corrupta que
-- el formulario no puede producir. Todas las filas actuales cumplen (hora_salida
-- venía NOT NULL), así que se crea validado de una.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'chk_ga_solicitud_trayecto_retorno_sin_salida'
          AND conrelid = 'ga_solicitud_trayecto'::regclass
    ) THEN
        ALTER TABLE ga_solicitud_trayecto
            ADD CONSTRAINT chk_ga_solicitud_trayecto_retorno_sin_salida
            CHECK (hora_retorno IS NULL OR hora_salida IS NOT NULL);
    END IF;
END $$;

COMMIT;

-- ── Verificación ────────────────────────────────────────────────────────────
-- Los 15 motivos deben salir todos con pide_horas_lugares = true:
--   SELECT id, descripcion, pide_horas_lugares FROM ga_motivo_salida ORDER BY id;
-- hora_salida debe salir con is_nullable = YES:
--   SELECT column_name, is_nullable FROM information_schema.columns
--    WHERE table_name = 'ga_solicitud_trayecto' AND column_name = 'hora_salida';
