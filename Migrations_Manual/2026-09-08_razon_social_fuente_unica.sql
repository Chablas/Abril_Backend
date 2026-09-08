-- ============================================================================
-- Razón social: alinear la ficha con la vinculación  (PASO 3 de PLAN-RAZON-SOCIAL.md)
-- ============================================================================
--
-- QUÉ ARREGLA
-- Hasta el 2026-09-08 había dos fuentes de razón social que no coincidían:
--   * `workers.contributor_id`  — la escribía SOLO el pre-ingreso de GTH
--   * `worker_vinculaciones.empresa_id` — la escriben el alta de SSOMA y todos
--     los cambios de empresa de Habilitación
-- El alta creaba el Worker sin `contributor_id` y los cambios de empresa no lo
-- tocaban, así que la ficha se desactualizaba con cada alta y cada cambio. Al
-- 2026-09-08, de los 2 700 trabajadores adentro en prod:
--   * 1 095 tenían la ficha en NULL con la vinculación bien puesta
--   *    90 discrepaban
--   *     9 no tenían ninguna vinculación
--   *     1 no tenía ningún periodo laboral
--
-- Este script deja las dos fuentes diciendo lo mismo. Gana SIEMPRE la
-- vinculación abierta: es la que escriben los procesos vivos.
--
-- ORDEN RESPECTO AL DESPLIEGUE
-- Se puede correr ANTES o DESPUÉS del deploy, da igual: el código nuevo ya lee
-- la vinculación y solo cae a la ficha cuando no hay ninguna. Correrlo primero
-- no arregla nada por sí solo; correrlo después no rompe nada.
--
-- ES RE-CORRIBLE. Correrlo dos veces no cambia nada la segunda vez.
--
-- CÓMO CORRERLO (desde PowerShell, con el túnel a prod abierto)
--   $cs = (Get-Content ".\Abril_Backend\appsettings.Production.json" -Raw -Encoding UTF8 | ConvertFrom-Json).Database.PostgreSQL
--   $h = @{}; foreach ($p in $cs.Split(';')) { if ($p -match '^([^=]+)=(.*)$') { $h[$matches[1].Trim()] = $matches[2] } }
--   $env:PGPASSWORD = $h['Password']; $env:PGCLIENTENCODING = 'UTF8'
--   & psql -h localhost -p 5544 -U $h['Username'] -d $h['Database'] -f .\Abril_Backend\Migrations_Manual\2026-09-08_razon_social_fuente_unica.sql
--
-- ALCANCE: solo trabajadores ADENTRO (`workers_estado.esta_adentro`). Las fichas
-- de retirados y de pre-ingreso no se tocan: su `contributor_id` es histórico o
-- todavía no tiene vinculación con la que compararse.
--
-- Si quisieras limitarte al universo que consume cupo (31 filas en vez de
-- 1 185), agrégale a los pasos 1 y 2:
--     AND coalesce(w.obra_oficina_staff_id, 0) IN (2, 3, 4)
-- No hace falta para que el conteo quede bien —el código ya lee la vinculación—,
-- pero sí para que los otros módulos que aún leen `workers.contributor_id`
-- (EMO, Salidas, Adjudicaciones…) dejen de mostrar la empresa equivocada.
-- ============================================================================

\set ON_ERROR_STOP on

BEGIN;

-- ── Foto de antes ───────────────────────────────────────────────────────────
WITH ab AS (
  SELECT DISTINCT ON (worker_id) worker_id, empresa_id
  FROM worker_vinculaciones
  WHERE fecha_fin IS NULL
  ORDER BY worker_id, created_at DESC, id DESC
),
adentro AS (
  SELECT w.id, w.contributor_id
  FROM workers w
  JOIN workers_estado we USING (workers_estado_id)
  WHERE w.state AND we.esta_adentro
)
SELECT 'ANTES' AS momento,
       count(*)                                                                    AS adentro,
       count(*) FILTER (WHERE a.contributor_id IS NULL AND ab.empresa_id IS NOT NULL) AS ficha_en_null,
       count(*) FILTER (WHERE a.contributor_id IS NOT NULL AND ab.empresa_id IS NOT NULL
                          AND a.contributor_id <> ab.empresa_id)                   AS discrepan,
       count(*) FILTER (WHERE ab.worker_id IS NULL)                                AS sin_vinculacion_abierta,
       count(*) FILTER (WHERE NOT EXISTS (SELECT 1 FROM workers_periodo_laboral pl
                                          WHERE pl.worker_id = a.id AND pl.state))  AS sin_ningun_periodo
FROM adentro a
LEFT JOIN ab ON ab.worker_id = a.id;


-- ── 1) Ficha en NULL → copiar la de la vinculación abierta ──────────────────
-- Los 1 095 casos del alta: el Worker nació sin razón social y solo la
-- vinculación la tiene. No se toca cuando la vinculación abierta la tiene en
-- NULL: ahí la ficha es el único dato que hay y perderlo sería peor.
WITH ab AS (
  SELECT DISTINCT ON (worker_id) worker_id, empresa_id
  FROM worker_vinculaciones
  WHERE fecha_fin IS NULL
  ORDER BY worker_id, created_at DESC, id DESC
)
UPDATE workers w
   SET contributor_id = ab.empresa_id,
       updated_at     = now()
  FROM ab, workers_estado we
 WHERE ab.worker_id = w.id
   AND we.workers_estado_id = w.workers_estado_id
   AND w.state AND we.esta_adentro
   AND w.contributor_id IS NULL
   AND ab.empresa_id IS NOT NULL;


-- ── 2) Discrepan → gana la vinculación abierta ──────────────────────────────
-- Los 90 casos del cambio de empresa: la ficha quedó congelada en la empresa
-- anterior. Habilitación escribió la vinculación y nadie bajó el dato a la
-- ficha, así que la vinculación es la reciente.
WITH ab AS (
  SELECT DISTINCT ON (worker_id) worker_id, empresa_id
  FROM worker_vinculaciones
  WHERE fecha_fin IS NULL
  ORDER BY worker_id, created_at DESC, id DESC
)
UPDATE workers w
   SET contributor_id = ab.empresa_id,
       updated_at     = now()
  FROM ab, workers_estado we
 WHERE ab.worker_id = w.id
   AND we.workers_estado_id = w.workers_estado_id
   AND w.state AND we.esta_adentro
   AND w.contributor_id IS NOT NULL
   AND ab.empresa_id IS NOT NULL
   AND w.contributor_id <> ab.empresa_id;


-- ── 3) Sin ninguna vinculación → crearla desde la ficha ─────────────────────
-- Los 9 de la carga masiva del 2026-08-10 (ids 14914–14922): tienen ficha con
-- razón social y periodo laboral abierto, pero nunca se les creó la
-- vinculación, así que son invisibles para Habilitación y para todo SSOMA.
--
--   fecha_inicio  = la del periodo laboral abierto (su ingreso real)
--   categoria_id  = snapshot desde el puesto, igual que el alta de SSOMA
--   proyecto_id   = NULL: no hay de dónde sacarlo, nunca tuvieron vinculación
--   obra_oficina_staff_id = NULL: el bueno es el de la ficha (el de la
--                           vinculación está sin poblar en 948 filas de prod)
INSERT INTO worker_vinculaciones (worker_id, empresa_id, categoria_id, fecha_inicio, created_at)
SELECT w.id,
       w.contributor_id,
       pu.categoria_id,
       pl.fecha_ingreso,
       now()
  FROM workers w
  JOIN workers_estado we USING (workers_estado_id)
  JOIN workers_periodo_laboral pl
    ON pl.worker_id = w.id AND pl.state AND pl.fecha_retiro IS NULL
  LEFT JOIN puesto pu ON pu.puesto_id = w.puesto_id
 WHERE w.state AND we.esta_adentro
   AND w.contributor_id IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM worker_vinculaciones v WHERE v.worker_id = w.id);


-- ── 4) Sin ningún periodo laboral → abrirle uno ─────────────────────────────
-- Un solo caso en prod (worker 15380): ficha activa, con vinculación abierta,
-- pero sin una sola fila en workers_periodo_laboral, así que el sistema no sabe
-- desde cuándo está. Se toma como ingreso la fecha en que se le abrió la
-- vinculación, que es el dato más cercano que existe.
--
-- OJO — esto NO cubre a los 18 trabajadores que sí tienen periodos pero todos
-- CERRADOS (están ACTIVO con fecha_retiro puesta). Ese es otro problema: alguien
-- los retiró y volvieron sin que se les reabriera el periodo. Reabrirlo desde
-- acá sería inventarles una fecha de reingreso, así que va aparte y con
-- decisión humana. La lista sale con el SELECT del final de este archivo.
INSERT INTO workers_periodo_laboral (worker_id, fecha_ingreso, created_date_time)
SELECT w.id,
       min(v.fecha_inicio),
       now()
  FROM workers w
  JOIN workers_estado we USING (workers_estado_id)
  JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
 WHERE w.state AND we.esta_adentro
   AND NOT EXISTS (SELECT 1 FROM workers_periodo_laboral pl WHERE pl.worker_id = w.id)
 GROUP BY w.id;


-- ── Foto de después: las tres primeras columnas deben quedar en 0 ───────────
WITH ab AS (
  SELECT DISTINCT ON (worker_id) worker_id, empresa_id
  FROM worker_vinculaciones
  WHERE fecha_fin IS NULL
  ORDER BY worker_id, created_at DESC, id DESC
),
adentro AS (
  SELECT w.id, w.contributor_id
  FROM workers w
  JOIN workers_estado we USING (workers_estado_id)
  WHERE w.state AND we.esta_adentro
)
SELECT 'DESPUES' AS momento,
       count(*)                                                                    AS adentro,
       count(*) FILTER (WHERE a.contributor_id IS NULL AND ab.empresa_id IS NOT NULL) AS ficha_en_null,
       count(*) FILTER (WHERE a.contributor_id IS NOT NULL AND ab.empresa_id IS NOT NULL
                          AND a.contributor_id <> ab.empresa_id)                   AS discrepan,
       count(*) FILTER (WHERE ab.worker_id IS NULL)                                AS sin_vinculacion_abierta,
       count(*) FILTER (WHERE NOT EXISTS (SELECT 1 FROM workers_periodo_laboral pl
                                          WHERE pl.worker_id = a.id AND pl.state))  AS sin_ningun_periodo
FROM adentro a
LEFT JOIN ab ON ab.worker_id = a.id;


-- ── Criterio de aceptación §6.2 del plan: debe dar 0 ────────────────────────
WITH v AS (
  SELECT DISTINCT ON (worker_id) worker_id, empresa_id
  FROM worker_vinculaciones
  ORDER BY worker_id, (fecha_fin IS NULL) DESC, created_at DESC, id DESC
)
SELECT count(*) AS deben_ser_cero
  FROM workers w
  JOIN workers_estado we USING (workers_estado_id)
  LEFT JOIN v ON v.worker_id = w.id
 WHERE w.state AND we.esta_adentro
   AND coalesce(w.obra_oficina_staff_id, 0) IN (2, 3, 4)
   AND w.contributor_id IS DISTINCT FROM v.empresa_id;


-- ── Conteo por razón social: tiene que cuadrar con la pantalla ──────────────
WITH universo AS (
  SELECT coalesce(
           (SELECT v.empresa_id FROM worker_vinculaciones v
             WHERE v.worker_id = w.id AND v.fecha_fin IS NULL
             ORDER BY v.created_at DESC, v.id DESC LIMIT 1),
           w.contributor_id) AS empresa_id
    FROM workers w
    JOIN workers_estado we USING (workers_estado_id)
   WHERE w.state AND we.esta_adentro
     AND coalesce(w.obra_oficina_staff_id, 0) IN (2, 3, 4)
     AND w.categoria_maestra_id IS DISTINCT FROM 2
)
SELECT c.contributor_name,
       count(u.empresa_id)                        AS ocupados,
       greatest(0, 20 - count(u.empresa_id))      AS cupos
  FROM contributor c
  LEFT JOIN universo u ON u.empresa_id = c.contributor_id
 WHERE c.state AND c.active AND c.operativo
 GROUP BY c.contributor_id, c.contributor_name
 ORDER BY 2 DESC, 1;


-- ── Pendiente para decisión humana: ACTIVOS con el periodo laboral cerrado ──
-- No los toca este script. Son gente marcada ACTIVO cuyo último periodo laboral
-- tiene fecha_retiro. Si de verdad están adentro, hay que reabrirles el periodo
-- (o abrirles uno nuevo desde su reingreso); si ya no están, hay que retirarlos.
SELECT w.id AS worker_id,
       p.full_name,
       (SELECT max(pl.fecha_retiro) FROM workers_periodo_laboral pl
         WHERE pl.worker_id = w.id AND pl.state)                       AS ultimo_retiro,
       (SELECT min(v.fecha_inicio) FROM worker_vinculaciones v
         WHERE v.worker_id = w.id AND v.fecha_fin IS NULL)             AS vinculacion_abierta_desde
  FROM workers w
  JOIN workers_estado we USING (workers_estado_id)
  LEFT JOIN person p ON p.person_id = w.person_id
 WHERE w.state AND we.esta_adentro
   AND EXISTS     (SELECT 1 FROM workers_periodo_laboral pl WHERE pl.worker_id = w.id AND pl.state)
   AND NOT EXISTS (SELECT 1 FROM workers_periodo_laboral pl
                    WHERE pl.worker_id = w.id AND pl.state AND pl.fecha_retiro IS NULL)
 ORDER BY w.id;

COMMIT;
