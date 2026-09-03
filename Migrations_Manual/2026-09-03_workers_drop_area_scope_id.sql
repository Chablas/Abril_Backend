-- ============================================================================
-- 2026-09-03 · Baja de workers.area_scope_id
--
-- ── Por qué ─────────────────────────────────────────────────────────────────
-- `workers.area_scope_id` y el área que se deriva de
--     workers.puesto_id → puesto.area_destino_scope_id
-- son el MISMO dato guardado dos veces, y desde el 2026-09-02 el área ya no se
-- elige en ninguna pantalla de Trabajadores: el modal la muestra de solo lectura
-- y la deriva del puesto. O sea, el dato dejó de capturarse. El único lector que
-- quedaba escribiéndola de verdad era el formulario de Configuración →
-- Trabajadores, que también pasa a derivarla.
--
-- La regla de auditoría del ARCHITECTURE.md protege FILAS (soft delete con
-- `state`), no columnas: cuando un dato deja de capturarse, el DROP COLUMN es la
-- salida (precedente: Migrations/Manual/20260813_GthDropPuntajesEvaluacion.sql).
--
-- Postgres se lleva con la columna, sin nombrarlos, la FK
-- `workers_area_scope_id_fkey` y el índice `idx_workers_area_scope_id`. No hay
-- vistas ni triggers que dependan de ella (verificado en dev y en prod).
--
-- ── ⚠ ORDEN OBLIGATORIO ─────────────────────────────────────────────────────
-- El PASO 3 va **SOLO DESPUÉS DE DESPLEGAR** el backend que ya no lee la
-- columna. Prod corre el último commit desplegado, que puede ser de varios días
-- atrás: si se bota la columna antes, todo endpoint que aún la seleccione muere
-- con 42703 y la pantalla sale con el 500 genérico. Ya pasó el 2026-08-25 con
-- `puesto_area_scope`.
--
--   1. Correr PASO 1 (respaldo) y PASO 2 (verificación) → se puede hoy.
--   2. Desplegar backend + frontend.
--   3. Recién ahí correr el PASO 3.
--
-- Correr en PROD **y en la VPS de demo** (demo.abril.pe es un clon con su propia
-- base: si se le olvida, demo queda leyendo una columna que su backend nuevo ya
-- no conoce... o al revés).
--
-- Re-corrible: los tres pasos son idempotentes.
-- ============================================================================


-- ============================================================================
-- PASO 1 · RESPALDO — antes del deploy
-- ----------------------------------------------------------------------------
-- El dato en sí no se pierde (queda derivable del puesto), pero las fichas que
-- HOY se contradicen con su puesto guardan una decisión de GTH que el árbol no
-- puede reconstruir. Se congela tal cual para poder auditar después qué área
-- tenía cada ficha el día del corte.
-- ============================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS workers_area_scope_historico (
    worker_id           integer      PRIMARY KEY,
    area_scope_id       integer      NOT NULL,
    puesto_id           integer,
    area_destino_puesto integer,
    coincidia           boolean      NOT NULL,
    congelado_el        timestamptz  NOT NULL DEFAULT now()
);

COMMENT ON TABLE workers_area_scope_historico IS
    'Foto de workers.area_scope_id al 2026-09-03, justo antes de bajar la columna. '
    'El área vigente se lee por workers.puesto_id -> puesto.area_destino_scope_id; '
    'esta tabla sólo sirve para auditar qué decía la ficha antes del corte.';

INSERT INTO workers_area_scope_historico
    (worker_id, area_scope_id, puesto_id, area_destino_puesto, coincidia)
SELECT
    w.id,
    w.area_scope_id,
    w.puesto_id,
    p.area_destino_scope_id,
    w.area_scope_id IS NOT DISTINCT FROM p.area_destino_scope_id
FROM workers w
LEFT JOIN puesto p ON p.puesto_id = w.puesto_id
WHERE w.area_scope_id IS NOT NULL          -- incluye state=false: es histórico
ON CONFLICT (worker_id) DO NOTHING;

COMMIT;


-- ============================================================================
-- PASO 2 · VERIFICACIÓN — antes del deploy
-- ----------------------------------------------------------------------------
-- Aborta si alguna ficha ACTIVA y NO-OBRA que HOY TIENE área terminaría con otra
-- (o sin ninguna) al derivarla del puesto. Es la guarda que evita que el DROP
-- mueva gente de área en silencio: aprobadores de salidas, revisores de área,
-- filtros de EMO/SCTR y Actas de Reunión salen todos de ahí.
--
-- Tres cosas quedan fuera A PROPÓSITO, porque el negocio las acepta (decisión del
-- 2026-09-03):
--   · las fichas que GANAN un área que hoy no tienen — no se pierde nada;
--   · Obra, que no gestiona área;
--   · las fichas no activas (hoy, 818 retirados), de las que 8 pierden el área
--     porque su puesto no tiene destino y 5 cambian de rama.
--
-- No escribe nada: si pasa, no imprime nada; si falla, RAISE EXCEPTION con la
-- lista de fichas. Al 2026-09-03 en prod pasa (0 fichas).
-- ============================================================================

DO $$
DECLARE
    v_n     integer;
    v_lista text;
BEGIN
    SELECT COUNT(*),
           string_agg(format('ficha %s (%s)', x.id, x.motivo), '; ' ORDER BY x.id)
    INTO v_n, v_lista
    FROM (
        SELECT w.id,
               CASE
                   WHEN w.puesto_id IS NULL              THEN 'sin puesto: pierde el area ' || w.area_scope_id
                   WHEN p.area_destino_scope_id IS NULL  THEN 'puesto ' || w.puesto_id || ' sin destino: pierde el area ' || w.area_scope_id
                   ELSE 'area ' || w.area_scope_id || ' -> ' || p.area_destino_scope_id
               END AS motivo
        FROM workers w
        JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
        LEFT JOIN puesto   p  ON p.puesto_id = w.puesto_id
        WHERE w.state
          AND we.esta_adentro
          AND w.obra_oficina_staff_id IS DISTINCT FROM 1     -- NULL entra: hay fichas con área y sin tipo
          AND w.area_scope_id IS NOT NULL                    -- las que GANAN area no son un problema
          AND w.area_scope_id IS DISTINCT FROM p.area_destino_scope_id
    ) x;

    IF v_n > 0 THEN
        RAISE EXCEPTION
            'Quedan % fichas ACTIVAS no-obra que PIERDEN o CAMBIAN de area al derivarla del puesto. Regularizar antes del DROP: %',
            v_n, v_lista;
    END IF;
END $$;


-- ============================================================================
-- PASO 3 · DROP — ⚠ SOLO DESPUÉS DE DESPLEGAR el backend que ya no lee la columna
-- ----------------------------------------------------------------------------
-- La FK `workers_area_scope_id_fkey` y el índice `idx_workers_area_scope_id`
-- caen solos con la columna. Los DROP explícitos de abajo son por si en alguna
-- base se llaman distinto — son IF EXISTS y no estorban.
-- ============================================================================

BEGIN;

ALTER TABLE workers DROP CONSTRAINT IF EXISTS workers_area_scope_id_fkey;
DROP INDEX IF EXISTS idx_workers_area_scope_id;

ALTER TABLE workers DROP COLUMN IF EXISTS area_scope_id;

COMMIT;
