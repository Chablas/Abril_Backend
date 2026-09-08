-- ============================================================================
-- Salidas: codigo unico SOL-AAAA-NNNN por solicitud
-- ============================================================================
-- Hasta ahora la solicitud no tenia identificador propio: la pantalla mostraba el
-- numero de fila y los correos un correlativo POR TRABAJADOR calculado al vuelo
-- (`count(*) where worker_id = X and id <= Y`). Ese numero cambiaba de significado
-- segun quien mirara y no servia para referirse a una salida entre areas.
--
-- Ahora cada solicitud lleva `codigo` = SOL-AAAA-NNNN, con el correlativo
-- reiniciandose por ano (ano en hora Peru, igual que el REQ-AAAA-NNNN de GTH).
--
-- ⚠ ORDEN DE EJECUCION
--   PARTE 1  → ANTES de desplegar el backend (el backend nuevo lee `codigo`).
--   deploy
--   PARTE 2  → SOLO DESPUES del deploy (ver la nota al pie).
--
-- Idempotente: las dos partes se pueden correr mas de una vez.
-- ============================================================================


-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║ PARTE 1 — antes del deploy                                               ║
-- ╚══════════════════════════════════════════════════════════════════════════╝

BEGIN;

-- ── 1. Columnas ─────────────────────────────────────────────────────────────
-- NULLABLES a proposito: mientras no se despliegue el backend nuevo, el que esta
-- corriendo sigue insertando solicitudes sin conocer estas columnas. Un NOT NULL
-- acá tumbaria todo INSERT hasta el deploy. Se cierra en la PARTE 2.

ALTER TABLE ga_solicitud_salida
    ADD COLUMN IF NOT EXISTS codigo varchar(20),
    ADD COLUMN IF NOT EXISTS anio   integer,
    ADD COLUMN IF NOT EXISTS numero integer;

COMMENT ON COLUMN ga_solicitud_salida.codigo IS
  'Codigo unico SOL-AAAA-NNNN. Es el identificador que ve el trabajador (pantallas, correos y planilla); id es interno.';
COMMENT ON COLUMN ga_solicitud_salida.anio IS
  'Ano del codigo (AAAA) en hora Peru. El correlativo numero se reinicia con el.';
COMMENT ON COLUMN ga_solicitud_salida.numero IS
  'Correlativo (NNNN) dentro del ano.';

-- ── 2. Backfill ─────────────────────────────────────────────────────────────
-- Se numera por ano de creacion en hora Peru y en orden de creacion, asi el codigo
-- respeta el orden real en que se pidieron las salidas.
--
-- El arranque de cada ano NO es 1 fijo sino el maximo ya usado + 1: si una corrida
-- anterior (o el backend) ya numero parte del ano, esta vuelta continua en vez de
-- repetir numeros y chocar contra el indice unico.

WITH sin_codigo AS (
    SELECT id,
           created_at,
           extract(year FROM created_at AT TIME ZONE 'America/Lima')::int AS anio
      FROM ga_solicitud_salida
     WHERE codigo IS NULL
),
ocupado AS (
    SELECT anio, max(numero) AS ultimo
      FROM ga_solicitud_salida
     WHERE codigo IS NOT NULL AND anio IS NOT NULL
     GROUP BY anio
),
numeradas AS (
    SELECT sc.id,
           sc.anio,
           (COALESCE(o.ultimo, 0)
            + row_number() OVER (PARTITION BY sc.anio ORDER BY sc.created_at, sc.id))::int AS numero
      FROM sin_codigo sc
      LEFT JOIN ocupado o ON o.anio = sc.anio
)
UPDATE ga_solicitud_salida s
   SET anio   = n.anio,
       numero = n.numero,
       codigo = 'SOL-' || n.anio::text || '-' || lpad(n.numero::text, 4, '0')
  FROM numeradas n
 WHERE s.id = n.id;

-- ── 3. Unicidad ─────────────────────────────────────────────────────────────
-- Un indice unico normal deja pasar varios NULL, asi que convive con las filas que
-- el backend viejo pueda insertar entre esta parte y el deploy. NO se filtra por
-- ningun estado: una solicitud cancelada sigue ocupando su numero (el codigo ya
-- salio por correo y no se puede reasignar).

CREATE UNIQUE INDEX IF NOT EXISTS uq_ga_solicitud_salida_codigo
    ON ga_solicitud_salida (codigo);

-- Lo consulta el backend al armar el correlativo del ano.
CREATE INDEX IF NOT EXISTS ix_ga_solicitud_salida_anio
    ON ga_solicitud_salida (anio);

COMMIT;

-- ── Verificacion de la PARTE 1 ──────────────────────────────────────────────
-- SELECT anio, count(*), min(numero), max(numero), min(codigo), max(codigo)
--   FROM ga_solicitud_salida GROUP BY anio ORDER BY anio;
-- SELECT count(*) AS sin_codigo FROM ga_solicitud_salida WHERE codigo IS NULL;


-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║ PARTE 2 — SOLO DESPUES DE DESPLEGAR EL BACKEND                           ║
-- ╚══════════════════════════════════════════════════════════════════════════╝
-- Corriendo esto ANTES del deploy, el backend que sigue en produccion no conoce
-- las columnas y todo INSERT de solicitud muere con 23502 (not-null violation).
-- Recien cuando el backend desplegado genera el codigo se puede exigir.
--
-- Repite el backfill primero: las solicitudes creadas por el backend viejo entre
-- la PARTE 1 y el deploy quedaron sin codigo y sin el, el SET NOT NULL falla.

/*
BEGIN;

WITH sin_codigo AS (
    SELECT id,
           created_at,
           extract(year FROM created_at AT TIME ZONE 'America/Lima')::int AS anio
      FROM ga_solicitud_salida
     WHERE codigo IS NULL
),
ocupado AS (
    SELECT anio, max(numero) AS ultimo
      FROM ga_solicitud_salida
     WHERE codigo IS NOT NULL AND anio IS NOT NULL
     GROUP BY anio
),
numeradas AS (
    SELECT sc.id,
           sc.anio,
           (COALESCE(o.ultimo, 0)
            + row_number() OVER (PARTITION BY sc.anio ORDER BY sc.created_at, sc.id))::int AS numero
      FROM sin_codigo sc
      LEFT JOIN ocupado o ON o.anio = sc.anio
)
UPDATE ga_solicitud_salida s
   SET anio   = n.anio,
       numero = n.numero,
       codigo = 'SOL-' || n.anio::text || '-' || lpad(n.numero::text, 4, '0')
  FROM numeradas n
 WHERE s.id = n.id;

ALTER TABLE ga_solicitud_salida
    ALTER COLUMN codigo SET NOT NULL,
    ALTER COLUMN anio   SET NOT NULL,
    ALTER COLUMN numero SET NOT NULL;

COMMIT;
*/
