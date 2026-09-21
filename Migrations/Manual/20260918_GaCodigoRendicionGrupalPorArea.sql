-- ============================================================================
-- Gestión Administrativa · Salidas — Código de la rendición grupal por área
-- Fecha: 2026-09-18
--
-- La rendición grupal (el Consolidado del S10 y su planilla de reembolso) deja
-- el código CON-AAAA-NNNN y pasa a CONS-<ÁREA>-AAAA-NNN, con correlativo propio
-- por área y año: CONS-GTH-2026-001, CONS-SSOMA-2026-001, ...
--
--   1. area_item.abreviatura: la sigla de cada área. Se siembra una propuesta;
--      se corrige a mano con un UPDATE (es lo único que lee el código).
--   2. ga_consolidado_s10.area_scope_id: el área del consolidado = la del
--      consolidador (su ficha vigente → puesto → área de destino). Se hereda
--      al reemplazar, junto con el código.
--   3. Se recodifican los consolidados que ya existen. Las versiones de un
--      mismo grupo (las que se reemplazaron) comparten el código viejo y
--      comparten también el nuevo.
--
-- Correr ANTES de desplegar el backend: EF lee area_item y ga_consolidado_s10
-- enteras (área y consolidado caen con 42703 en toda la aplicación).
--
-- Idempotente: se puede correr varias veces. Si la columna codigo todavía no
-- existía (el script del CON-AAAA-NNNN no se había corrido), también la crea.
-- ============================================================================

BEGIN;

-- ── 1. Abreviatura de cada área ─────────────────────────────────────────────
ALTER TABLE area_item ADD COLUMN IF NOT EXISTS abreviatura varchar(10);

COMMENT ON COLUMN area_item.abreviatura IS
    'Sigla del área. Arma el código de la rendición grupal: CONS-<abreviatura>-AAAA-NNN.';

-- Dos áreas vivas con la misma sigla harían ambiguo el código.
CREATE UNIQUE INDEX IF NOT EXISTS ux_area_item_abreviatura_viva
    ON area_item (upper(abreviatura))
    WHERE state AND abreviatura IS NOT NULL;

-- Propuesta inicial. Solo toca las que no tienen sigla: re-correr el script no
-- pisa una sigla corregida a mano. Si un nombre se repite, gana el que cuelga
-- de un nodo vivo del árbol.
WITH propuesta(nombre, abreviatura) AS (
    VALUES
        ('Gerencia General',              'GG'),
        ('Gerencia de Administración',    'GADM'),
        ('Gerencia de Marketing',         'GMKT'),
        ('Gerencia de Proyectos',         'GPROY'),
        ('Administración',                'ADM'),
        ('Administración de Obra',        'ADMO'),
        ('Almacenero',                    'ALM'),
        ('Arquitectura',                  'ARQ'),
        ('Arquitectura Comercial',        'ARQC'),
        ('Calidad',                       'CAL'),
        ('Contabilidad',                  'CONT'),
        ('Costos y Presupuestos',         'CYP'),
        ('Finanzas',                      'FIN'),
        ('Gestión del Talento Humano',    'GTH'),
        ('Ingeniería BIM',                'IBIM'),
        ('Legal',                         'LEG'),
        ('Logística',                     'LOG'),
        ('Marketing',                     'MKT'),
        ('Planeamiento',                  'PLAN'),
        ('Planeamiento BIM',              'PBIM'),
        ('Post Venta',                    'PV'),
        ('Producción',                    'PROD'),
        ('Proyectos',                     'PROY'),
        ('Residencia',                    'RES'),
        ('SSOMA',                         'SSOMA'),
        ('Tecnología de la Información',  'TI'),
        ('Trámites Documentarios',        'TD'),
        ('Unidad de Proyectos',           'UDP'),
        ('Ventas',                        'VTA')
),
candidatos AS (
    SELECT DISTINCT ON (upper(p.abreviatura)) ai.area_item_id, p.abreviatura
    FROM propuesta p
    JOIN area_item ai
      ON ai.state
     AND lower(ai.area_item_name) = lower(p.nombre)
    WHERE ai.abreviatura IS NULL
      AND NOT EXISTS (
          SELECT 1 FROM area_item x
          WHERE x.state AND upper(x.abreviatura) = upper(p.abreviatura))
    ORDER BY upper(p.abreviatura),
             EXISTS (SELECT 1 FROM area_scope s
                     WHERE s.state AND s.area_item_id = ai.area_item_id) DESC,
             ai.area_item_id
)
UPDATE area_item ai
SET abreviatura = c.abreviatura
FROM candidatos c
WHERE ai.area_item_id = c.area_item_id;

-- ── 2. Área del consolidado ─────────────────────────────────────────────────
ALTER TABLE ga_consolidado_s10
    ADD COLUMN IF NOT EXISTS area_scope_id integer,
    -- Por si el script del código CON-AAAA-NNNN no se corrió todavía.
    ADD COLUMN IF NOT EXISTS codigo text,
    ADD COLUMN IF NOT EXISTS anio   integer,
    ADD COLUMN IF NOT EXISTS numero integer;

COMMENT ON COLUMN ga_consolidado_s10.area_scope_id IS
    'Área del consolidado (la del consolidador). Arma el código CONS-<área>-AAAA-NNN; se hereda al reemplazar.';

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid = 'ga_consolidado_s10'::regclass
          AND conname  = 'fk_ga_consolidado_s10_area_scope'
    ) THEN
        ALTER TABLE ga_consolidado_s10
            ADD CONSTRAINT fk_ga_consolidado_s10_area_scope
            FOREIGN KEY (area_scope_id) REFERENCES area_scope(area_scope_id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_ga_consolidado_s10_area_scope_id
    ON ga_consolidado_s10 (area_scope_id);

-- El correlativo ya no es único por año sino por área y año: lo único que
-- queda único es el código entero, entre los vigentes.
DROP INDEX IF EXISTS ux_ga_consolidado_s10_anio_numero;

CREATE UNIQUE INDEX IF NOT EXISTS ux_ga_consolidado_s10_codigo
    ON ga_consolidado_s10 (codigo)
    WHERE state AND codigo IS NOT NULL;

-- Área de los consolidados que ya existen: la del consolidador que los subió,
-- por su ficha vigente (una persona puede tener varias por reingreso).
WITH ficha AS (
    SELECT DISTINCT ON (p.user_id) p.user_id, pu.area_destino_scope_id
    FROM person p
    JOIN workers w  ON w.person_id = p.person_id AND w.state
    LEFT JOIN puesto pu ON pu.puesto_id = w.puesto_id
    WHERE p.user_id IS NOT NULL
    ORDER BY p.user_id,
             EXISTS (SELECT 1 FROM worker_vinculaciones v
                     WHERE v.worker_id = w.id
                       AND (v.fecha_fin IS NULL OR v.fecha_fin >= current_date)) DESC,
             (w.workers_estado_id = 1) DESC,
             w.id DESC
)
UPDATE ga_consolidado_s10 c
SET area_scope_id = f.area_destino_scope_id
FROM ficha f
WHERE f.user_id = c.uploaded_by_id
  AND c.area_scope_id IS NULL
  AND f.area_destino_scope_id IS NOT NULL;

-- ── 3. Recodificar al formato nuevo ─────────────────────────────────────────
-- Todo lo que no está ya en CONS-: los CON-AAAA-NNNN y los que no tenían código.
-- Las versiones de un mismo grupo comparten el código viejo → un solo número
-- nuevo por grupo. El correlativo sigue al más alto que ya exista con ese
-- prefijo y año (por si el script se re-corre después de subir consolidados).
WITH base AS (
    SELECT c.id,
           c.codigo AS codigo_viejo,
           c.uploaded_at,
           COALESCE(c.anio,
                    EXTRACT(YEAR FROM c.uploaded_at AT TIME ZONE 'America/Lima')::int) AS anio,
           'CONS' || COALESCE('-' || upper(ai.abreviatura), '') AS prefijo
    FROM ga_consolidado_s10 c
    LEFT JOIN area_scope s  ON s.area_scope_id = c.area_scope_id
    LEFT JOIN area_item  ai ON ai.area_item_id = s.area_item_id
    WHERE c.codigo IS NULL OR c.codigo NOT LIKE 'CONS%'
),
grupos AS (
    SELECT COALESCE(codigo_viejo, 'id:' || id) AS grupo,
           min(prefijo)     AS prefijo,
           min(anio)        AS anio,
           min(uploaded_at) AS desde
    FROM base
    GROUP BY 1
),
numerados AS (
    SELECT g.grupo, g.prefijo, g.anio,
           row_number() OVER (PARTITION BY g.prefijo, g.anio ORDER BY g.desde, g.grupo)
           + COALESCE((SELECT max(x.numero) FROM ga_consolidado_s10 x
                       WHERE x.codigo LIKE g.prefijo || '-' || g.anio || '-%'), 0) AS numero
    FROM grupos g
)
UPDATE ga_consolidado_s10 c
SET codigo = n.prefijo || '-' || n.anio || '-' || lpad(n.numero::text, 3, '0'),
    anio   = n.anio,
    numero = n.numero
FROM base b
JOIN numerados n ON n.grupo = COALESCE(b.codigo_viejo, 'id:' || b.id)
WHERE c.id = b.id;

COMMIT;

-- Para revisar las siglas y corregirlas:
--   SELECT area_item_id, area_item_name, abreviatura FROM area_item WHERE state ORDER BY area_item_name;
--   UPDATE area_item SET abreviatura = 'XYZ' WHERE area_item_id = ...;
-- (Cambiar una sigla no recodifica lo ya emitido: solo los consolidados nuevos.)
