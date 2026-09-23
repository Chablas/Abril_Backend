-- ============================================================================
-- worker_vinculaciones como subperiodo laboral  (PASO 4 de PLAN-RAZON-SOCIAL.md)
-- ============================================================================
--
-- QUÉ ES
-- El plan proponía crear una tabla nueva `workers_subperiodo_laboral`, copiarle
-- las vinculaciones y escribir en las dos durante meses. Se decidió (2026-09-17)
-- NO crearla: `worker_vinculaciones` ya ES el subperiodo — cada fila es un tramo
-- con razón social, proyecto y puesto fijos, y "Cambiar obra / puesto de
-- trabajo" ya cierra el tramo y abre otro. Le faltaban dos cosas para poder
-- contestar "qué puesto/área/proyecto tenía en tal periodo":
--
--   * workers_periodo_laboral_id — a qué periodo laboral pertenece el tramo.
--     Deducirlo por fechas no alcanza: en prod 943 vinculaciones empiezan ~2 días
--     ANTES del ingreso registrado y no caen dentro de ningún periodo.
--   * puesto_id — el puesto del tramo. Hasta hoy solo se guardaba el NOMBRE
--     (`puesto`, texto) y en 878 de 6 101 filas. El ÁREA NO se guarda: sale del
--     puesto (puesto.area_destino_scope_id), decisión del 2026-09-17.
--
-- Y `obra_oficina_staff_id`, que ya existía pero solo estaba poblada en 103
-- filas, se llena desde la ficha.
--
-- Nadie lee todavía estas columnas (eso es el Paso 5): el sistema no cambia.
--
-- ORDEN RESPECTO AL DESPLIEGUE — CORRERLO DOS VECES
--   1) ANTES del deploy. El backend nuevo mapea las dos columnas en EF: si el
--      deploy llega primero, TODA consulta de EF sobre worker_vinculaciones
--      (Habilitación, SSOMA, Control de Acceso…) cae con 42703.
--   2) OTRA VEZ DESPUÉS del deploy. Llena las filas que el backend viejo haya
--      creado entre la primera corrida y el deploy.
-- El backend viejo convive sin problema con el esquema nuevo: no ve las columnas.
--
-- ES RE-CORRIBLE. Cada sentencia solo toca lo que falta; la segunda corrida
-- sobre una base al día no cambia nada.
--
-- SIN TRANSACCIÓN GLOBAL, a propósito: en prod los scripts van sentencia por
-- sentencia (ver obra_oficina_resto_sin_transaccion.sql). No hace falta: cada
-- paso deja la tabla consistente por sí solo.
--
-- NO toca updated_at: no cambia ningún dato que ya existiera, solo completa.
--
-- CÓMO CORRERLO (desde PowerShell, con el túnel a prod abierto)
--   $cs = (Get-Content ".\Abril_Backend\appsettings.Production.json" -Raw -Encoding UTF8 | ConvertFrom-Json).Database.PostgreSQL
--   $h = @{}; foreach ($p in $cs.Split(';')) { if ($p -match '^([^=]+)=(.*)$') { $h[$matches[1].Trim()] = $matches[2] } }
--   $env:PGPASSWORD = $h['Password']; $env:PGCLIENTENCODING = 'UTF8'
--   & psql -h localhost -p 5544 -U $h['Username'] -d $h['Database'] -f .\Abril_Backend\Migrations_Manual\2026-09-17_worker_vinculaciones_subperiodo.sql
-- ============================================================================

\set ON_ERROR_STOP on


-- ── 1) Columnas ─────────────────────────────────────────────────────────────
ALTER TABLE worker_vinculaciones ADD COLUMN IF NOT EXISTS workers_periodo_laboral_id integer;
ALTER TABLE worker_vinculaciones ADD COLUMN IF NOT EXISTS puesto_id integer;

CREATE INDEX IF NOT EXISTS ix_worker_vinculaciones_periodo_laboral
    ON worker_vinculaciones (workers_periodo_laboral_id);
CREATE INDEX IF NOT EXISTS ix_worker_vinculaciones_puesto
    ON worker_vinculaciones (puesto_id);


-- ── 2) Llaves foráneas ──────────────────────────────────────────────────────
-- La del periodo es COMPUESTA (periodo + worker): un tramo solo puede colgar de
-- un periodo del MISMO trabajador. Con una FK simple nada impediría enganchar la
-- vinculación de uno al periodo de otro. Para eso el periodo necesita el par
-- (id, worker_id) como único — lo es por definición, porque el id ya es la PK.
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_workers_periodo_laboral_id_worker') THEN
    ALTER TABLE workers_periodo_laboral
      ADD CONSTRAINT uq_workers_periodo_laboral_id_worker UNIQUE (workers_periodo_laboral_id, worker_id);
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_worker_vinculaciones_periodo_laboral') THEN
    ALTER TABLE worker_vinculaciones
      ADD CONSTRAINT fk_worker_vinculaciones_periodo_laboral
      FOREIGN KEY (workers_periodo_laboral_id, worker_id)
      REFERENCES workers_periodo_laboral (workers_periodo_laboral_id, worker_id);
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_worker_vinculaciones_puesto') THEN
    ALTER TABLE worker_vinculaciones
      ADD CONSTRAINT fk_worker_vinculaciones_puesto
      FOREIGN KEY (puesto_id) REFERENCES puesto (puesto_id);
  END IF;
END $$;


-- ── 3) A qué periodo pertenece cada tramo ───────────────────────────────────
-- Regla para la data vieja (en prod, 6 101 filas):
--   a) el periodo que contiene la fecha de inicio                  (~4 950)
--      — si hay dos (29 filas de periodos superpuestos), el más reciente;
--   b) si ninguno la contiene, el periodo más cercano en días        (~975)
--      — casi todos empiezan 2 días antes del ingreso registrado;
--   c) NULL cuando no hay periodo al que pertenecer:
--      * la ficha no tiene ningún periodo (~156; fichas fusionadas y la carga masiva)
--      * el tramo empieza DESPUÉS del último retiro y no hay periodo abierto (~16).
--        Incluye a los ACTIVOS con el periodo cerrado que siguen pendientes de
--        decisión: si después se les reabre el periodo, re-correr este script.
--
-- Una fila ABIERTA no se engancha a un periodo que ya tenga otra fila abierta:
-- lo prohíbe la restricción del paso 6, y así la segunda corrida no revienta.
WITH elegido AS (
  SELECT v.id,
         v.fecha_fin,
         (SELECT pl.workers_periodo_laboral_id
            FROM workers_periodo_laboral pl
           WHERE pl.worker_id = v.worker_id
             AND pl.state
           ORDER BY
             (v.fecha_inicio BETWEEN pl.fecha_ingreso AND coalesce(pl.fecha_retiro, 'infinity'::date)) DESC,
             CASE WHEN v.fecha_inicio < pl.fecha_ingreso THEN pl.fecha_ingreso - v.fecha_inicio
                  WHEN v.fecha_inicio > pl.fecha_retiro  THEN v.fecha_inicio - pl.fecha_retiro
                  ELSE 0 END,
             pl.fecha_ingreso DESC,
             pl.workers_periodo_laboral_id DESC
           LIMIT 1) AS periodo_id
    FROM worker_vinculaciones v
   WHERE v.workers_periodo_laboral_id IS NULL
     AND EXISTS (SELECT 1 FROM workers_periodo_laboral pl
                  WHERE pl.worker_id = v.worker_id
                    AND pl.state
                    AND (pl.fecha_retiro IS NULL OR v.fecha_inicio <= pl.fecha_retiro))
)
UPDATE worker_vinculaciones v
   SET workers_periodo_laboral_id = e.periodo_id
  FROM elegido e
 WHERE e.id = v.id
   AND e.periodo_id IS NOT NULL
   AND (e.fecha_fin IS NOT NULL
        OR NOT EXISTS (SELECT 1 FROM worker_vinculaciones o
                        WHERE o.workers_periodo_laboral_id = e.periodo_id
                          AND o.fecha_fin IS NULL
                          AND o.id <> v.id));


-- ── 4) Puesto de cada tramo ─────────────────────────────────────────────────
--   * Tramo ABIERTO → el puesto de la ficha. Es el vigente por definición, y es
--     lo que van a leer los módulos cuando dejen `workers.puesto_id` (Paso 5).
--   * Tramo CERRADO → en este orden:
--       1) el nombre guardado en `puesto` + la categoría congelada, si identifican
--          un único puesto del catálogo;
--       2) solo el nombre, si identifica un único puesto;
--       3) sin nombre guardado, el puesto de la ficha — salvo que la categoría
--          congelada del tramo lo contradiga (en prod, ~1 000 tramos cerrados
--          tenían otra categoría: ahí la ficha de hoy no dice nada del puesto de
--          entonces y se deja NULL en vez de inventarlo).
-- Los abiertos se vuelven a alinear en cada corrida (no solo los NULL): si entre
-- la primera corrida y el deploy el backend viejo le cambió el puesto a una ficha,
-- la segunda corrida lo recoge. Después del deploy ya coinciden siempre.
WITH calculado AS (
  SELECT v.id,
         CASE
           WHEN v.fecha_fin IS NULL THEN w.puesto_id
           ELSE coalesce(
             (SELECT min(pu.puesto_id) FROM puesto pu
               WHERE pu.state
                 AND upper(btrim(pu.nombre)) = upper(btrim(v.puesto))
                 AND pu.categoria_id = v.categoria_id
              HAVING count(*) = 1),
             (SELECT min(pu.puesto_id) FROM puesto pu
               WHERE pu.state
                 AND upper(btrim(pu.nombre)) = upper(btrim(v.puesto))
              HAVING count(*) = 1),
             CASE WHEN v.puesto IS NULL
                   AND (v.categoria_id IS NULL OR v.categoria_id = pf.categoria_id)
                  THEN w.puesto_id END)
         END AS puesto_id
    FROM worker_vinculaciones v
    JOIN workers w ON w.id = v.worker_id
    LEFT JOIN puesto pf ON pf.puesto_id = w.puesto_id
   WHERE v.fecha_fin IS NULL OR v.puesto_id IS NULL
)
UPDATE worker_vinculaciones v
   SET puesto_id = c.puesto_id
  FROM calculado c
 WHERE c.id = v.id
   AND v.puesto_id IS DISTINCT FROM c.puesto_id;


-- ── 5) Obra / Staff / Oficina Central de cada tramo ─────────────────────────
-- La buena es la de la ficha (0 discrepancias medidas en prod); la de la
-- vinculación casi nunca se llenó.
--   * Tramo ABIERTO → siempre la de la ficha.
--   * Tramo CERRADO → se respeta la que ya tuviera (se escribió en su momento, es
--     histórica de verdad) y solo se completa la que falta.
UPDATE worker_vinculaciones v
   SET obra_oficina_staff_id = w.obra_oficina_staff_id
  FROM workers w
 WHERE w.id = v.worker_id
   AND w.obra_oficina_staff_id IS NOT NULL
   AND v.obra_oficina_staff_id IS DISTINCT FROM w.obra_oficina_staff_id
   AND (v.fecha_fin IS NULL OR v.obra_oficina_staff_id IS NULL);


-- ── 6) Un solo tramo abierto por periodo ────────────────────────────────────
-- EXCLUDE y no un índice único parcial, porque tiene que ser DEFERRABLE: cambiar
-- de obra/puesto cierra el tramo vigente y abre el nuevo en el MISMO SaveChanges,
-- y un índice único se evalúa sentencia por sentencia — si EF manda el INSERT
-- antes que el UPDATE, reventaría con un 23505 aunque la transacción termine bien.
-- Diferida, se valida al COMMIT. Los tramos sin periodo (NULL) no se restringen.
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ex_worker_vinculaciones_una_abierta_por_periodo') THEN
    ALTER TABLE worker_vinculaciones
      ADD CONSTRAINT ex_worker_vinculaciones_una_abierta_por_periodo
      EXCLUDE USING btree (workers_periodo_laboral_id WITH =)
      WHERE (fecha_fin IS NULL)
      DEFERRABLE INITIALLY DEFERRED;
  END IF;
END $$;


-- ── Verificación ────────────────────────────────────────────────────────────
-- Las tres últimas columnas tienen que dar 0.
SELECT count(*)                                                               AS filas,
       count(*) FILTER (WHERE v.workers_periodo_laboral_id IS NOT NULL)       AS con_periodo,
       count(*) FILTER (WHERE v.workers_periodo_laboral_id IS NULL
                          AND NOT EXISTS (SELECT 1 FROM workers_periodo_laboral pl
                                           WHERE pl.worker_id = v.worker_id AND pl.state)) AS sin_periodo_ficha_sin_periodos,
       count(*) FILTER (WHERE v.workers_periodo_laboral_id IS NULL
                          AND EXISTS (SELECT 1 FROM workers_periodo_laboral pl
                                       WHERE pl.worker_id = v.worker_id AND pl.state))     AS sin_periodo_tras_ultimo_retiro,
       count(*) FILTER (WHERE v.puesto_id IS NOT NULL)                        AS con_puesto,
       count(*) FILTER (WHERE v.fecha_fin IS NOT NULL AND v.puesto_id IS NULL) AS cerradas_sin_puesto,
       count(*) FILTER (WHERE v.fecha_fin IS NULL
                          AND v.puesto_id IS DISTINCT FROM w.puesto_id)       AS abiertas_puesto_distinto_ficha_debe_ser_0,
       count(*) FILTER (WHERE v.fecha_fin IS NULL
                          AND w.obra_oficina_staff_id IS NOT NULL
                          AND v.obra_oficina_staff_id IS DISTINCT FROM w.obra_oficina_staff_id) AS abiertas_clasif_distinta_ficha_debe_ser_0,
       (SELECT count(*) FROM (SELECT workers_periodo_laboral_id FROM worker_vinculaciones
                               WHERE fecha_fin IS NULL AND workers_periodo_laboral_id IS NOT NULL
                               GROUP BY 1 HAVING count(*) > 1) x)             AS periodos_con_2_abiertas_debe_ser_0
  FROM worker_vinculaciones v
  JOIN workers w ON w.id = v.worker_id;
