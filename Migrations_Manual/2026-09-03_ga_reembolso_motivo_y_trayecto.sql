-- ============================================================================
-- Salidas: reembolso de movilidad configurable por motivo y por trayecto
-- ============================================================================
-- El reembolso de movilidad de una salida deja de ser conocimiento tribal y
-- pasa a ser data. Dos flags nuevos, con semantica ASIMETRICA a proposito:
--
--   ga_motivo_salida.es_reembolsable  -> CONCEDE el reembolso (default false).
--   ga_trayecto.es_reembolsable       -> lo QUITA (default true).
--
-- Es decir: corresponde reembolso cuando el motivo lo concede Y el par
-- (origen, destino) elegido no esta marcado como excepcion. Un trayecto nunca
-- concede reembolso por su cuenta si el motivo no lo da. Hoy la unica excepcion
-- es Oficina Central <-> Bosque Real, que la empresa cubre con movilidad propia.
--
-- Ambos flags se editan desde Gestion Administrativa -> Configuracion
-- (pestanas Motivos y Trayectos) con un checkbox "Reembolsable".
--
-- Idempotente: se puede correr mas de una vez.
-- ============================================================================

BEGIN;

-- ── 1. Flag del motivo ──────────────────────────────────────────────────────
-- Default false: el reembolso es la excepcion, no la norma. Los motivos
-- historicos quedan sin reembolso salvo los que se prenden en el paso 3.

ALTER TABLE ga_motivo_salida
    ADD COLUMN IF NOT EXISTS es_reembolsable boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN ga_motivo_salida.es_reembolsable IS
  'Si true, una salida con este motivo genera reembolso de movilidad. El par (origen, destino) elegido puede anularlo via ga_trayecto.es_reembolsable, nunca al reves.';

-- ── 2. Flag del trayecto ────────────────────────────────────────────────────
-- Default true: un trayecto del catalogo existe justamente porque tiene monto
-- de movilidad. Marcarlo en false es declararlo excepcion.

ALTER TABLE ga_trayecto
    ADD COLUMN IF NOT EXISTS es_reembolsable boolean NOT NULL DEFAULT true;

COMMENT ON COLUMN ga_trayecto.es_reembolsable IS
  'Si false, ninguna salida por este par (origen, destino) genera reembolso, aunque el motivo elegido si lo permita. Solo puede QUITAR el reembolso que concede el motivo.';

-- ── 3. Motivos que arrancan como reembolsables ──────────────────────────────
-- Por descripcion y no por id: los ids no son los mismos en dev y en prod.
--
-- Los patrones usan "_" (comodin de un caracter) donde va una tilde a proposito:
-- asi el script queda 100% ASCII y no depende de que el archivo llegue en UTF-8
-- al momento de ejecutarlo. "comit_ de obra" matchea "Comité de obra".

UPDATE ga_motivo_salida
   SET es_reembolsable = true
 WHERE NOT es_reembolsable
   AND lower(descripcion) LIKE ANY (ARRAY[
         'comit_ de obra',               -- Comité de obra
         'feria',
         'fft',
         'gesti_n con vecinos',          -- Gestión con vecinos
         'gestiones administrativas',
         'supervisi_n de actividades',   -- Supervisión de actividades
         'visita a obra',
         'visita a salas de venta'
       ]);

-- Guarda: si alguno de los 8 motivos no matcheo (renombrado, o el archivo llego
-- con la codificacion rota), abortar en vez de dejar la mitad de la data puesta.
DO $$
DECLARE
    faltantes text;
BEGIN
    SELECT string_agg(p, ', ')
      INTO faltantes
      FROM unnest(ARRAY[
            'comit_ de obra', 'feria', 'fft', 'gesti_n con vecinos',
            'gestiones administrativas', 'supervisi_n de actividades',
            'visita a obra', 'visita a salas de venta'
           ]) AS p
     WHERE NOT EXISTS (
            SELECT 1 FROM ga_motivo_salida m
             WHERE lower(m.descripcion) LIKE p AND m.es_reembolsable);

    IF faltantes IS NOT NULL THEN
        RAISE EXCEPTION 'No se encontro motivo reembolsable para: %', faltantes;
    END IF;
END $$;

-- ── 4. Excepcion Oficina Central <-> Bosque Real ────────────────────────────
-- Por nombre y no por id (los ids no coinciden entre dev y prod) y en las dos
-- direcciones. Ojo: "Oficina Central" existe DOS veces en ga_lugar (una fija y
-- una como proyecto) y cual de las dos esta activa cambia por ambiente, asi que
-- se resuelven todas las que se llamen asi.

CREATE TEMP TABLE tmp_pares_sin_reembolso ON COMMIT DROP AS
WITH lugares AS (
    SELECT l.id,
           l.activo,
           upper(trim(COALESCE(l.nombre, p.project_description))) AS nombre
      FROM ga_lugar l
      LEFT JOIN project p ON p.project_id = l.project_id
     WHERE l.tipo <> 'libre'
),
oficina AS (SELECT id, activo FROM lugares WHERE nombre = 'OFICINA CENTRAL'),
bosque  AS (SELECT id, activo FROM lugares WHERE nombre = 'BOSQUE REAL')
SELECT o.id AS origen, b.id AS destino, (o.activo AND b.activo) AS ambos_activos
  FROM oficina o CROSS JOIN bosque b
UNION ALL
SELECT b.id, o.id, (o.activo AND b.activo)
  FROM oficina o CROSS JOIN bosque b;

-- Guarda: sin los dos lugares no hay nada que marcar y el resto seria un no-op
-- silencioso.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM tmp_pares_sin_reembolso) THEN
        RAISE EXCEPTION 'No se encontraron los lugares OFICINA CENTRAL / BOSQUE REAL en ga_lugar';
    END IF;
END $$;

-- 4.a Los que ya existen en el catalogo se marcan (incluidos los que apuntan a
--     un lugar inactivo: el trayecto sigue visible en la pantalla de config).
UPDATE ga_trayecto t
   SET es_reembolsable = false
  FROM tmp_pares_sin_reembolso p
 WHERE t.lugar_origen_id = p.origen
   AND t.lugar_destino_id = p.destino
   AND t.es_reembolsable;

-- 4.b Los que faltan se crean, pero solo entre lugares activos: un trayecto
--     hacia un lugar inactivo nunca se puede elegir en el formulario, asi que
--     seria una fila muerta. Monto 0 porque no genera movilidad a reembolsar.
INSERT INTO ga_trayecto (lugar_origen_id, lugar_destino_id, monto, es_reembolsable, activo, created_at)
SELECT p.origen, p.destino, 0.00, false, true, now()
  FROM tmp_pares_sin_reembolso p
 WHERE p.ambos_activos
   AND NOT EXISTS (
        SELECT 1 FROM ga_trayecto t
         WHERE t.lugar_origen_id = p.origen
           AND t.lugar_destino_id = p.destino);

COMMIT;

-- ── Verificacion ────────────────────────────────────────────────────────────
-- SELECT id, descripcion, activo, es_reembolsable
--   FROM ga_motivo_salida ORDER BY descripcion;
--
-- SELECT t.id,
--        COALESCE(lo.nombre, po.project_description) AS origen,
--        COALESCE(ld.nombre, pd.project_description) AS destino,
--        t.monto, t.activo, t.es_reembolsable
--   FROM ga_trayecto t
--   JOIN ga_lugar lo ON lo.id = t.lugar_origen_id
--   LEFT JOIN project po ON po.project_id = lo.project_id
--   JOIN ga_lugar ld ON ld.id = t.lugar_destino_id
--   LEFT JOIN project pd ON pd.project_id = ld.project_id
--  WHERE NOT t.es_reembolsable
--  ORDER BY t.id;
