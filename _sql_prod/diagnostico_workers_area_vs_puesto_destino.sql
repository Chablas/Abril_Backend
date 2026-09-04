-- =============================================================================
-- DIAGNOSTICO (solo lectura): workers.area_scope_id  vs  puesto.area_destino_scope_id
--
-- Objetivo: antes de bajar workers.area_scope_id, verificar que el area guardada
-- en la ficha sea la misma que se derivaria de
--     workers.puesto_id -> puesto.area_destino_scope_id
-- (el area a la que VA el contratado, NO la del solicitante).
--
-- ALCANCE de las consultas 2, 3 y 4:
--     w.state                                        -- ficha viva (state=false = duplicado fusionado)
--     AND (w.obra_oficina_staff_id IN (2, 3)          -- Staff / Oficina Central: hoy gestionan area
--          OR w.area_scope_id IS NOT NULL)            -- + cualquier ficha que YA tenga area
-- El segundo OR no es adorno: en prod hay 2 fichas con area y obra_oficina_staff_id
-- NULL, que un filtro por tipo dejaria fuera del analisis (consulta 1 lo delata).
--
-- Sin tildes a proposito: asi corre igual por -c inline en PowerShell (cp1252).
-- =============================================================================


-- -----------------------------------------------------------------------------
-- 1) CONTROL DE PREMISA: quien tiene area_scope_id hoy, por tipo de trabajador.
--    Valida (o rompe) el supuesto "solo Staff y Oficina Central tienen area".
-- -----------------------------------------------------------------------------
SELECT
    w.obra_oficina_staff_id                                        AS tipo_id,
    COALESCE(oo.name, '(sin tipo)')                                AS tipo,
    COUNT(*)                                                       AS fichas,
    COUNT(w.area_scope_id)                                         AS con_area_en_ficha,
    COUNT(*) FILTER (WHERE w.area_scope_id IS NULL)                AS sin_area_en_ficha,
    COUNT(w.puesto_id)                                             AS con_puesto,
    COUNT(p.area_destino_scope_id)                                 AS con_area_en_puesto
FROM workers w
LEFT JOIN workers_obra_oficina_staff oo ON oo.workers_obra_oficina_staff_id = w.obra_oficina_staff_id
LEFT JOIN puesto p                      ON p.puesto_id = w.puesto_id
WHERE w.state
GROUP BY 1, 2
ORDER BY 1;


-- -----------------------------------------------------------------------------
-- 2) RESUMEN: como queda cada ficha al derivar el area del puesto.
--    Es el conteo que decide si la columna se puede bajar sin decisiones de GTH.
-- -----------------------------------------------------------------------------
WITH RECURSIVE arbol AS (
    SELECT s.area_scope_id,
           i.area_item_name::text                     AS ruta,
           ARRAY[s.area_scope_id]                     AS ancestros,
           s.state AND s.active                       AS usable
    FROM area_scope s
    JOIN area_item  i ON i.area_item_id = s.area_item_id
    WHERE s.area_scope_parent_id IS NULL
    UNION ALL
    SELECT h.area_scope_id,
           a.ruta || ' > ' || i.area_item_name,
           a.ancestros || h.area_scope_id,
           h.state AND h.active
    FROM area_scope h
    JOIN area_item  i ON i.area_item_id = h.area_item_id
    JOIN arbol      a ON a.area_scope_id = h.area_scope_parent_id
),
base AS (
    SELECT
        w.id                                                       AS ficha_id,
        w.area_scope_id                                            AS area_ficha_id,
        p.area_destino_scope_id                                    AS area_puesto_id,
        w.puesto_id,
        af.ancestros                                               AS ancestros_ficha,
        ap.ancestros                                               AS ancestros_puesto,
        we.esta_adentro
    FROM workers w
    LEFT JOIN puesto         p  ON p.puesto_id = w.puesto_id
    LEFT JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
    LEFT JOIN arbol          af ON af.area_scope_id = w.area_scope_id
    LEFT JOIN arbol          ap ON ap.area_scope_id = p.area_destino_scope_id
    WHERE w.state
      AND (w.obra_oficina_staff_id IN (2, 3) OR w.area_scope_id IS NOT NULL)
)
SELECT
    CASE
        WHEN puesto_id IS NULL                                     THEN '5. SIN_PUESTO (no hay de donde derivar)'
        WHEN area_ficha_id IS NULL AND area_puesto_id IS NULL      THEN '6. SIN_AREA_POR_NINGUN_LADO'
        WHEN area_ficha_id IS NULL                                 THEN '3. FICHA_SIN_AREA (gana la del puesto)'
        WHEN area_puesto_id IS NULL                                THEN '4. PUESTO_SIN_DESTINO (la ficha PIERDE su area)'
        WHEN area_ficha_id = area_puesto_id                        THEN '1. COINCIDE'
        ELSE                                                            '2. DIFIERE'
    END                                                            AS diagnostico,
    COALESCE(
        CASE
            WHEN area_ficha_id IS NULL OR area_puesto_id IS NULL
              OR area_ficha_id = area_puesto_id                    THEN NULL
            WHEN area_puesto_id = ANY (ancestros_ficha)            THEN 'PUESTO_ES_ANCESTRO (apunta mas arriba)'
            WHEN area_ficha_id  = ANY (ancestros_puesto)           THEN 'PUESTO_ES_DESCENDIENTE (apunta mas abajo)'
            ELSE                                                        'RAMA_DISTINTA (contradiccion real)'
        END, '-')                                                  AS relacion,
    COUNT(*)                                                       AS fichas,
    COUNT(*) FILTER (WHERE esta_adentro)                           AS adentro,
    COUNT(*) FILTER (WHERE NOT esta_adentro)                       AS fuera_o_preingreso
FROM base
GROUP BY 1, 2
ORDER BY 1, 2;


-- -----------------------------------------------------------------------------
-- 3) DETALLE: una fila por ficha, con las dos areas en formato ruta. El nombre
--    suelto del area NO identifica un nodo ("Produccion" y "Unidad de Proyectos"
--    existen en dos ramas vivas distintas), por eso va la ruta completa.
--    Los diagnosticos que necesitan decision van primero.
--    Para ver solo lo que no cuadra: descomentar el WHERE del final.
-- -----------------------------------------------------------------------------
WITH RECURSIVE arbol AS (
    SELECT s.area_scope_id,
           i.area_item_name::text                     AS ruta,
           ARRAY[s.area_scope_id]                     AS ancestros,
           s.state AND s.active                       AS usable
    FROM area_scope s
    JOIN area_item  i ON i.area_item_id = s.area_item_id
    WHERE s.area_scope_parent_id IS NULL
    UNION ALL
    SELECT h.area_scope_id,
           a.ruta || ' > ' || i.area_item_name,
           a.ancestros || h.area_scope_id,
           h.state AND h.active
    FROM area_scope h
    JOIN area_item  i ON i.area_item_id = h.area_item_id
    JOIN arbol      a ON a.area_scope_id = h.area_scope_parent_id
),
base AS (
    SELECT
        w.id                                                       AS ficha_id,
        w.id_trabajador,
        pe.document_identity_code                                  AS dni,
        COALESCE(pe.full_name, w.apellido_nombre)                  AS trabajador,
        COALESCE(oo.name, '(sin tipo)')                            AS tipo,
        we.nombre                                                  AS estado,
        we.esta_adentro,
        w.puesto_id,
        p.nombre                                                   AS puesto,
        p.active                                                   AS puesto_activo,
        w.area_scope_id                                            AS area_ficha_id,
        af.ruta                                                    AS area_ficha,
        p.area_destino_scope_id                                    AS area_puesto_id,
        ap.ruta                                                    AS area_puesto,
        ap.usable                                                  AS area_puesto_usable,
        af.ancestros                                               AS ancestros_ficha,
        ap.ancestros                                               AS ancestros_puesto
    FROM workers w
    LEFT JOIN person                     pe ON pe.person_id = w.person_id
    LEFT JOIN workers_obra_oficina_staff oo ON oo.workers_obra_oficina_staff_id = w.obra_oficina_staff_id
    LEFT JOIN workers_estado             we ON we.workers_estado_id = w.workers_estado_id
    LEFT JOIN puesto                     p  ON p.puesto_id = w.puesto_id
    LEFT JOIN arbol                      af ON af.area_scope_id = w.area_scope_id
    LEFT JOIN arbol                      ap ON ap.area_scope_id = p.area_destino_scope_id
    WHERE w.state
      AND (w.obra_oficina_staff_id IN (2, 3) OR w.area_scope_id IS NOT NULL)
)
SELECT
    CASE
        WHEN puesto_id IS NULL                                     THEN '5. SIN_PUESTO'
        WHEN area_ficha_id IS NULL AND area_puesto_id IS NULL      THEN '6. SIN_AREA_POR_NINGUN_LADO'
        WHEN area_ficha_id IS NULL                                 THEN '3. FICHA_SIN_AREA'
        WHEN area_puesto_id IS NULL                                THEN '4. PUESTO_SIN_DESTINO'
        WHEN area_ficha_id = area_puesto_id                        THEN '1. COINCIDE'
        ELSE                                                            '2. DIFIERE'
    END                                                            AS diagnostico,
    CASE
        WHEN area_ficha_id IS NULL OR area_puesto_id IS NULL
          OR area_ficha_id = area_puesto_id                        THEN NULL
        WHEN area_puesto_id = ANY (ancestros_ficha)                THEN 'PUESTO_ES_ANCESTRO'
        WHEN area_ficha_id  = ANY (ancestros_puesto)               THEN 'PUESTO_ES_DESCENDIENTE'
        ELSE                                                            'RAMA_DISTINTA'
    END                                                            AS relacion,
    ficha_id,
    id_trabajador,
    dni,
    trabajador,
    tipo,
    estado,
    puesto_id,
    puesto,
    puesto_activo,
    area_ficha_id,
    area_ficha,
    area_puesto_id,
    area_puesto,
    area_puesto_usable
FROM base
-- WHERE area_ficha_id IS DISTINCT FROM area_puesto_id   -- <- solo lo que NO cuadra
ORDER BY
    CASE
        WHEN puesto_id IS NULL                                 THEN 5
        WHEN area_ficha_id IS NULL AND area_puesto_id IS NULL  THEN 6
        WHEN area_ficha_id IS NULL                             THEN 3
        WHEN area_puesto_id IS NULL                            THEN 4
        WHEN area_ficha_id = area_puesto_id                    THEN 1
        ELSE                                                        2
    END DESC,
    esta_adentro DESC,
    area_puesto,
    trabajador;


-- -----------------------------------------------------------------------------
-- 4) PUESTOS A ARREGLAR: puestos usados por fichas del alcance que NO tienen
--    area de destino. Cada uno le borraria el area a su ficha al derivarla, y
--    si esta activo tambien bloquea el alta de Staff / Oficina Central.
-- -----------------------------------------------------------------------------
SELECT
    p.puesto_id,
    p.nombre                                                       AS puesto,
    p.active                                                       AS puesto_activo,
    p.area_solicitante_scope_id                                    AS area_solicitante_id,
    COUNT(*)                                                       AS fichas_afectadas,
    COUNT(w.area_scope_id)                                         AS fichas_que_perderian_area,
    STRING_AGG(DISTINCT w.area_scope_id::text, ', ')               AS areas_hoy_en_esas_fichas
FROM workers w
JOIN puesto p ON p.puesto_id = w.puesto_id
WHERE w.state
  AND (w.obra_oficina_staff_id IN (2, 3) OR w.area_scope_id IS NOT NULL)
  AND p.area_destino_scope_id IS NULL
GROUP BY 1, 2, 3, 4
ORDER BY 6 DESC, 5 DESC, 2;


-- =============================================================================
-- RESULTADO EN PROD — 2026-09-02 (271 fichas en el alcance)
--
--   1. COINCIDE ................................................. 209  (199 adentro)
--   2. DIFIERE / PUESTO_ES_ANCESTRO ..............................   7  (7)
--   2. DIFIERE / PUESTO_ES_DESCENDIENTE ..........................   4  (4)
--   2. DIFIERE / RAMA_DISTINTA ...................................  24  (18)
--   3. FICHA_SIN_AREA (hereda la del puesto) .....................   9  (8)
--   4. PUESTO_SIN_DESTINO (perderia el area) .....................  10  (1)
--   5. SIN_PUESTO / 6. SIN_AREA_POR_NINGUN_LADO ..................   0
--
-- Los 9 puestos sin area de destino: 7 ADMINISTRACION (inactivo),
-- 44 ASISTENTE DE CAMPO (inactivo), 112 COORDINADOR DE POST-VENTA,
-- 122 DISENADOR GRAFICO, 125 DISENADOR GRAFICO SR, 220 OFICINA TECNICA,
-- 275 PRACTICANTE DE GTH, 276 PRACTICANTE LEGAL, 296 SUBGERENTE DE FINANZAS.
-- Ninguno tiene area solicitante tampoco.
-- =============================================================================
