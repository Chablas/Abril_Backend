-- ============================================================================
-- 2026-09-03 · DEMO: alinear el área de los trabajadores con PRODUCCIÓN
--
-- ⚠ Este script es SOLO para la base de la VPS de demo (demo.abril.pe).
--    NO correrlo en producción: en prod estos datos ya están bien (la guarda
--    del DROP devuelve 0 fichas).
--
-- ── Por qué ─────────────────────────────────────────────────────────────────
-- Demo es un clon de prod de antes de que se regularizaran las áreas, así que
-- la guarda del PASO 2 de `Migrations_Manual/2026-09-03_workers_drop_area_scope_id.sql`
-- falla ahí con 30 fichas activas no-obra.
--
-- En prod esas 30 se arreglaron de DOS maneras distintas, y por eso este script
-- tiene que hacer las dos:
--
--   a) cambiándole el PUESTO a la ficha — los 7 de Contact Center pasaron al
--      puesto 225 OPERADOR DE CONTACT CENTER (destino Ventas 63) en vez de uno
--      de Marketing, y la ficha 12944 pasó del puesto 111 COORDINADOR DE
--      MARKETING (que sigue sin destino) al 343 JEFE DE MARKETING DIGITAL;
--   b) cambiándole el ÁREA DE DESTINO al puesto — los puestos BIM (23, 162,
--      201, 353) apuntan a Ingeniería BIM 53 y no a Unidad de Proyectos 41.
--
-- Los ids de puesto y de área van explícitos, leídos de PROD el 2026-09-03: el
-- nombre del área no identifica un nodo (hay dos «Producción» en el árbol) y el
-- nombre del puesto tampoco (MODELADOR BIM existe dos veces, como INGENIERO
-- 201 y como ARQUITECTO 353).
--
-- Re-corrible: todos los pasos son idempotentes.
-- ============================================================================

BEGIN;

-- ── Catálogo de referencia: los puestos tal como están en PROD ──────────────
CREATE TEMP TABLE tmp_puesto_prod (
    puesto_id        int PRIMARY KEY,
    nombre           text,
    categoria_id     int,
    area_solicitante int,
    area_destino     int,
    orden            int
) ON COMMIT DROP;

INSERT INTO tmp_puesto_prod VALUES
    (  2, 'ABOGADO DE GESTIONES INMOBILIARIAS',    43,   59,  59,  0),
    (  3, 'ABOGADO DE GESTIONES INMOBILIARIAS JR', 43,   59,  59,  0),
    ( 10, 'AGENTE DE SEGURIDAD',                   32,   88,  54,  0),
    ( 14, 'ALMACENERO',                            44,   60,  60,  0),
    ( 23, 'ARQUITECTO BIM',                        24,   41,  53,  0),
    ( 38, 'ASISTENTE ADMINISTRATIVO DE COBRANZAS',  3,   55,  55,  0),
    ( 43, 'ASISTENTE DE CALIDAD',                   3,   42,  42,  0),
    ( 47, 'ASISTENTE DE CUMPLIMIENTO',              3,   59,  59,  0),
    ( 49, 'ASISTENTE DE GERENCIA GENERAL',          3,   88,  88,  0),
    ( 58, 'ASISTENTE DE TESORERIA',                 3,   56,  56,  0),
    ( 63, 'ASISTENTE LEGAL',                        3,   59,  59,  0),
    ( 66, 'AUXILIAR ADMINISTRATIVO',               18,   56,  56,  0),
    ( 88, 'BARISTA EJECUTIVO',                      5,   17,  54,  0),
    (162, 'INGENIERO DE PLANEAMIENTO BIM',         27,   41,  53,  0),
    (163, 'INGENIERO DE PRODUCCIÓN',               27,   17,  76,  0),
    (169, 'INGENIERO PRACTICANTE',                  4,   76,  76,  0),
    (201, 'MODELADOR BIM',                         27,   41,  53,  0),
    (225, 'OPERADOR DE CONTACT CENTER',            21,   63,  63,  0),
    (282, 'PREVENCIONISTA DE RIESGOS JR',          35,   44,  52,  0),
    (324, 'TESORERA',                              46,   56,  56,  0),
    (343, 'JEFE DE MARKETING DIGITAL',             17,   75,  75, 13),
    (353, 'MODELADOR BIM',                         24,   41,  53, 19);

-- ── Ficha → puesto y área, tal como están en PROD ───────────────────────────
CREATE TEMP TABLE tmp_ficha_prod (
    ficha_id      int PRIMARY KEY,
    puesto_id     int,
    area_scope_id int
) ON COMMIT DROP;

INSERT INTO tmp_ficha_prod VALUES
    (11862,  14, 60), (11957,  10, 54), (12092, 225, 63), (12907,  38, 55),
    (12944, 343, 75), (12951, 324, 56), (12963, 225, 63), (13066,   3, 59),
    (13262, 225, 63), (13273,  23, 53), (13354, 169, 76), (13410, 225, 63),
    (13421,  43, 42), (13450, 225, 63), (13489,  63, 59), (13514, 162, 53),
    (13515,  47, 59), (13526, 225, 63), (13548, 163, 76), (13714, 162, 53),
    (13718,  49, 88), (13823,   2, 59), (13847,  66, 56), (13852, 162, 53),
    (13853,  88, 54), (13856, 225, 63), (14025, 282, 52), (14228, 201, 53),
    (14442,  58, 56), (15273, 353, 53);


-- ── PASO 0 · Guardas: que exista en demo todo lo que se va a referenciar ────
-- Si falta un nodo de área o una categoría, el script aborta con el detalle en
-- vez de morir con un error de llave foránea que no dice nada.
DO $$
DECLARE v_falta text;
BEGIN
    SELECT string_agg(DISTINCT 'area_scope ' || x.id, ', ')
    INTO v_falta
    FROM (
        SELECT area_destino     AS id FROM tmp_puesto_prod WHERE area_destino     IS NOT NULL
        UNION
        SELECT area_solicitante      FROM tmp_puesto_prod WHERE area_solicitante IS NOT NULL
    ) x
    WHERE NOT EXISTS (SELECT 1 FROM area_scope s WHERE s.area_scope_id = x.id);
    IF v_falta IS NOT NULL THEN
        RAISE EXCEPTION 'Faltan nodos del arbol de areas en demo: %', v_falta;
    END IF;

    SELECT string_agg(DISTINCT 'categoria ' || p.categoria_id, ', ')
    INTO v_falta
    FROM tmp_puesto_prod p
    WHERE NOT EXISTS (SELECT 1 FROM categoria c WHERE c.categoria_id = p.categoria_id);
    IF v_falta IS NOT NULL THEN
        RAISE EXCEPTION 'Faltan categorias en demo: %', v_falta;
    END IF;

    SELECT string_agg('ficha ' || f.ficha_id, ', ' ORDER BY f.ficha_id)
    INTO v_falta
    FROM tmp_ficha_prod f
    WHERE NOT EXISTS (SELECT 1 FROM workers w WHERE w.id = f.ficha_id AND w.state);
    IF v_falta IS NOT NULL THEN
        RAISE WARNING 'Estas fichas de prod no existen (o estan eliminadas) en demo y se saltan: %', v_falta;
    END IF;

    -- Un puesto que en demo existe con OTRO id pero el mismo nombre + categoria +
    -- area solicitante chocaria contra el indice unico
    -- `ux_puesto_nombre_categoria_area_solicitante_vivo` (que compara los NULL como
    -- iguales) al insertarlo. Se avisa aca con el detalle en vez de dejar que
    -- reviente el INSERT con un mensaje que no dice cual es.
    SELECT string_agg(
               format('puesto %s %L choca con el %s que ya existe en demo',
                      t.puesto_id, t.nombre, p.puesto_id),
               '; ' ORDER BY t.puesto_id)
    INTO v_falta
    FROM tmp_puesto_prod t
    JOIN puesto p
      ON p.state
     AND p.nombre = t.nombre
     AND p.categoria_id = t.categoria_id
     AND p.area_solicitante_scope_id IS NOT DISTINCT FROM t.area_solicitante
     AND p.puesto_id <> t.puesto_id
    WHERE NOT EXISTS (SELECT 1 FROM puesto q WHERE q.puesto_id = t.puesto_id);
    IF v_falta IS NOT NULL THEN
        RAISE EXCEPTION
            'Hay puestos duplicados entre demo y prod: %. Resolver a mano cual se queda antes de re-correr.',
            v_falta;
    END IF;
END $$;


-- ── PASO 1 · Crear en demo los puestos que solo existen en prod ─────────────
-- Son los que se crearon después del clon (343 y 353 tienen `orden` distinto de
-- 0, señal de que nacieron con el padrón nuevo). Se insertan con su id explícito
-- porque las fichas del PASO 3 apuntan a ese id.
-- `created_date_time` se omite a propósito: es timestamptz con default now(), y
-- escribirlo como `now() AT TIME ZONE 'UTC'` guardaría el instante equivocado
-- (esa expresión devuelve un timestamp SIN zona, que Postgres reinterpreta con
-- la zona de la sesión al insertarlo en una columna CON zona).
INSERT INTO puesto (puesto_id, nombre, categoria_id,
                    area_solicitante_scope_id, area_destino_scope_id,
                    orden, active, state)
SELECT t.puesto_id, t.nombre, t.categoria_id,
       t.area_solicitante, t.area_destino,
       t.orden, true, true
FROM tmp_puesto_prod t
WHERE NOT EXISTS (SELECT 1 FROM puesto p WHERE p.puesto_id = t.puesto_id);

-- `puesto.puesto_id` es GENERATED BY DEFAULT AS IDENTITY: insertar ids explícitos
-- NO adelanta la secuencia, y el primer puesto que cree la app moriría con
-- 23505 llave duplicada. Se realinea siempre, aunque no se haya insertado nada.
SELECT setval('public.puesto_puesto_id_seq', GREATEST((SELECT MAX(puesto_id) FROM puesto), 1));


-- ── PASO 2 · Área de destino de los puestos, igual que en prod ──────────────
-- Es el arreglo (b): los puestos BIM y compañía apuntaban al área equivocada.
-- Solo se toca `area_destino_scope_id`. El área SOLICITANTE se deja como está a
-- propósito: entra en el índice único
-- `ux_puesto_nombre_categoria_area_solicitante_vivo` y moverla podría chocar con
-- otra fila viva del mismo nombre + categoría, que es un problema distinto y no
-- hace falta resolverlo para bajar la columna.
UPDATE puesto p
SET    area_destino_scope_id = t.area_destino,
       updated_date_time     = now()
FROM   tmp_puesto_prod t
WHERE  p.puesto_id = t.puesto_id
  AND  p.area_destino_scope_id IS DISTINCT FROM t.area_destino;


-- ── PASO 3 · Puesto de las 30 fichas, igual que en prod ─────────────────────
-- Es el arreglo (a). Cambiar el puesto cambia también la CATEGORÍA de la ficha
-- (sale de `puesto.categoria_id`), que es justo lo que se quiere: en prod esa
-- gente ya está en el puesto correcto.
UPDATE workers w
SET    puesto_id  = f.puesto_id,
       updated_at = now()
FROM   tmp_ficha_prod f
WHERE  w.id = f.ficha_id
  AND  w.state
  AND  w.puesto_id IS DISTINCT FROM f.puesto_id;


-- ── PASO 4 · Re-derivar el área de la ficha desde su puesto ─────────────────
-- Lo que quede desalineado después de los pasos 2 y 3 se re-deriva: el área de
-- la ficha pasa a ser la de destino de su puesto, que es exactamente el valor
-- que va a leerse una vez bajada la columna.
--
-- Alcance = el mismo que mira la guarda: fichas vivas, ACTIVAS y no-obra. Los
-- retirados NO se tocan (conservan su área histórica, igual que en prod), y
-- nunca se pone NULL: un puesto sin destino deja la ficha como estaba.
UPDATE workers w
SET    area_scope_id = p.area_destino_scope_id,
       updated_at    = now()
FROM   puesto p, workers_estado we
WHERE  p.puesto_id = w.puesto_id
  AND  we.workers_estado_id = w.workers_estado_id
  AND  w.state
  AND  we.esta_adentro
  AND  w.obra_oficina_staff_id IS DISTINCT FROM 1
  AND  p.area_destino_scope_id IS NOT NULL
  AND  w.area_scope_id IS DISTINCT FROM p.area_destino_scope_id;


-- ── PASO 5 · Verificación: la misma guarda del script del DROP ──────────────
-- Si pasa, no imprime nada y el COMMIT deja demo lista para el DROP.
-- Si falla, aborta TODO el script (está dentro de la misma transacción) y dice
-- qué fichas quedaron sueltas.
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
                   WHEN w.puesto_id IS NULL             THEN 'sin puesto: pierde el area ' || w.area_scope_id
                   WHEN p.area_destino_scope_id IS NULL THEN 'puesto ' || w.puesto_id || ' sin destino: pierde el area ' || w.area_scope_id
                   ELSE 'area ' || w.area_scope_id || ' -> ' || p.area_destino_scope_id
               END AS motivo
        FROM workers w
        JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
        LEFT JOIN puesto   p  ON p.puesto_id = w.puesto_id
        WHERE w.state
          AND we.esta_adentro
          AND w.obra_oficina_staff_id IS DISTINCT FROM 1
          AND w.area_scope_id IS NOT NULL
          AND w.area_scope_id IS DISTINCT FROM p.area_destino_scope_id
    ) x;

    IF v_n > 0 THEN
        RAISE EXCEPTION
            'Demo sigue con % fichas desalineadas despues de la alineacion. Revisar a mano: %',
            v_n, v_lista;
    END IF;

    RAISE NOTICE 'Demo alineada: 0 fichas activas no-obra pierden o cambian de area.';
END $$;

COMMIT;
