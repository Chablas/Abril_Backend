-- =============================================================================
-- QUIENES HAY QUE REGULARIZAR antes de bajar workers.area_scope_id
-- (solo lectura, re-corrible)
--
-- Regla nueva:  area de la ficha  :=  workers.puesto_id -> puesto.area_destino_scope_id
-- Al bajar workers.area_scope_id, TODA ficha pasa a leer su area por ahi. Este
-- script lista a quien le cambiaria (o le desapareceria) el area, ordenado por
-- prioridad de negocio:
--
--   P1  ACTIVO  + no-obra   -> hay que arreglarlo SI o SI antes del DROP
--   P2  ACTIVO  + obra      -> revisar, pero obra no gestiona area
--   P3  no activo (retirado / pre-ingreso / etc.), cualquier tipo -> historico
--
-- "no-obra" = obra_oficina_staff_id <> 1 (Obra) o NULL. NULL entra a proposito:
-- en prod hay fichas vivas con area y sin tipo, que un filtro IN (2,3) esconde.
--
-- Sin tildes a proposito: asi corre igual por -c inline en PowerShell (cp1252).
-- =============================================================================


-- -----------------------------------------------------------------------------
-- 0) PREMISA: "ningun trabajador de obra tiene area_scope_id". Verificar.
--    Una fila de Obra con con_area > 0 rompe el supuesto del usuario.
-- -----------------------------------------------------------------------------
SELECT
    COALESCE(oo.name, '(sin tipo)')                                     AS tipo,
    COUNT(*)                                                            AS fichas_vivas,
    COUNT(*) FILTER (WHERE we.esta_adentro)                             AS activas,
    COUNT(w.area_scope_id)                                              AS con_area_en_ficha,
    COUNT(*) FILTER (WHERE w.area_scope_id IS NOT NULL AND we.esta_adentro) AS con_area_y_activas,
    COUNT(w.puesto_id)                                                  AS con_puesto,
    COUNT(p.area_destino_scope_id)                                      AS con_destino_en_puesto
FROM workers w
LEFT JOIN workers_obra_oficina_staff oo ON oo.workers_obra_oficina_staff_id = w.obra_oficina_staff_id
LEFT JOIN workers_estado             we ON we.workers_estado_id = w.workers_estado_id
LEFT JOIN puesto                     p  ON p.puesto_id = w.puesto_id
WHERE w.state
GROUP BY 1
ORDER BY 1;


-- -----------------------------------------------------------------------------
-- 1) RESUMEN POR PRIORIDAD: cuantas fichas hay que tocar y de que tipo.
--    Alcance = TODAS las fichas vivas (no se filtra por tipo: el DROP les pega
--    a todas). "OK" = el area no cambia al derivarla del puesto.
-- -----------------------------------------------------------------------------
WITH RECURSIVE arbol AS (
    SELECT s.area_scope_id,
           i.area_item_name::text                     AS ruta,
           ARRAY[s.area_scope_id]                     AS ancestros
    FROM area_scope s
    JOIN area_item  i ON i.area_item_id = s.area_item_id
    WHERE s.area_scope_parent_id IS NULL
    UNION ALL
    SELECT h.area_scope_id,
           a.ruta || ' > ' || i.area_item_name,
           a.ancestros || h.area_scope_id
    FROM area_scope h
    JOIN area_item  i ON i.area_item_id = h.area_item_id
    JOIN arbol      a ON a.area_scope_id = h.area_scope_parent_id
),
base AS (
    SELECT
        w.id                                                            AS ficha_id,
        w.area_scope_id                                                 AS area_ficha_id,
        p.area_destino_scope_id                                         AS area_puesto_id,
        w.puesto_id,
        COALESCE(we.esta_adentro, false)                                AS activo,
        w.obra_oficina_staff_id = 1                                     AS es_obra,
        af.ancestros                                                    AS ancestros_ficha,
        ap.ancestros                                                    AS ancestros_puesto
    FROM workers w
    LEFT JOIN puesto         p  ON p.puesto_id = w.puesto_id
    LEFT JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
    LEFT JOIN arbol          af ON af.area_scope_id = w.area_scope_id
    LEFT JOIN arbol          ap ON ap.area_scope_id = p.area_destino_scope_id
    WHERE w.state
),
clasificada AS (
    SELECT
        CASE
            WHEN activo AND NOT COALESCE(es_obra, false) THEN 'P1 ACTIVO no-obra'
            WHEN activo                                  THEN 'P2 ACTIVO obra'
            ELSE                                              'P3 no activo'
        END                                                             AS prioridad,
        CASE
            WHEN area_ficha_id IS NOT DISTINCT FROM area_puesto_id      THEN 'OK (no cambia)'
            WHEN area_ficha_id IS NULL                                  THEN 'GANA area (hoy no tiene)'
            WHEN puesto_id IS NULL                                      THEN 'PIERDE area: ficha SIN PUESTO'
            WHEN area_puesto_id IS NULL                                 THEN 'PIERDE area: puesto SIN DESTINO'
            WHEN area_puesto_id = ANY (ancestros_ficha)                 THEN 'CAMBIA: puesto es ANCESTRO (pierde detalle)'
            WHEN area_ficha_id  = ANY (ancestros_puesto)                THEN 'CAMBIA: puesto es DESCENDIENTE (gana detalle)'
            ELSE                                                             'CAMBIA: RAMA DISTINTA'
        END                                                             AS efecto,
        *
    FROM base
)
SELECT prioridad, efecto, COUNT(*) AS fichas
FROM clasificada
GROUP BY 1, 2
ORDER BY 1, (efecto LIKE 'OK%') DESC, 3 DESC;


-- -----------------------------------------------------------------------------
-- 2) LISTA DE REGULARIZACION: una fila por ficha que NO queda igual.
--    Ordenada P1 -> P3. Las dos areas van con la ruta completa porque el nombre
--    suelto no identifica un nodo ("Produccion" existe en 2 ramas vivas).
-- -----------------------------------------------------------------------------
WITH RECURSIVE arbol AS (
    SELECT s.area_scope_id,
           i.area_item_name::text                     AS ruta,
           ARRAY[s.area_scope_id]                     AS ancestros
    FROM area_scope s
    JOIN area_item  i ON i.area_item_id = s.area_item_id
    WHERE s.area_scope_parent_id IS NULL
    UNION ALL
    SELECT h.area_scope_id,
           a.ruta || ' > ' || i.area_item_name,
           a.ancestros || h.area_scope_id
    FROM area_scope h
    JOIN area_item  i ON i.area_item_id = h.area_item_id
    JOIN arbol      a ON a.area_scope_id = h.area_scope_parent_id
),
base AS (
    SELECT
        w.id                                                            AS ficha_id,
        w.id_trabajador,
        pe.document_identity_code                                       AS dni,
        COALESCE(pe.full_name, w.apellido_nombre)                       AS trabajador,
        COALESCE(oo.name, '(sin tipo)')                                 AS tipo,
        we.nombre                                                       AS estado,
        COALESCE(we.esta_adentro, false)                                AS activo,
        w.obra_oficina_staff_id = 1                                     AS es_obra,
        w.puesto_id,
        p.nombre                                                        AS puesto,
        p.active                                                        AS puesto_activo,
        w.area_scope_id                                                 AS area_ficha_id,
        af.ruta                                                         AS area_ficha,
        p.area_destino_scope_id                                         AS area_puesto_id,
        ap.ruta                                                         AS area_puesto,
        af.ancestros                                                    AS ancestros_ficha,
        ap.ancestros                                                    AS ancestros_puesto
    FROM workers w
    LEFT JOIN person                     pe ON pe.person_id = w.person_id
    LEFT JOIN workers_obra_oficina_staff oo ON oo.workers_obra_oficina_staff_id = w.obra_oficina_staff_id
    LEFT JOIN workers_estado             we ON we.workers_estado_id = w.workers_estado_id
    LEFT JOIN puesto                     p  ON p.puesto_id = w.puesto_id
    LEFT JOIN arbol                      af ON af.area_scope_id = w.area_scope_id
    LEFT JOIN arbol                      ap ON ap.area_scope_id = p.area_destino_scope_id
    WHERE w.state
)
SELECT
    CASE
        WHEN activo AND NOT COALESCE(es_obra, false) THEN 'P1 ACTIVO no-obra'
        WHEN activo                                  THEN 'P2 ACTIVO obra'
        ELSE                                              'P3 no activo'
    END                                                                 AS prioridad,
    CASE
        WHEN area_ficha_id IS NULL                                      THEN 'GANA area'
        WHEN puesto_id IS NULL                                          THEN 'PIERDE: sin puesto'
        WHEN area_puesto_id IS NULL                                     THEN 'PIERDE: puesto sin destino'
        WHEN area_puesto_id = ANY (ancestros_ficha)                     THEN 'CAMBIA: ancestro'
        WHEN area_ficha_id  = ANY (ancestros_puesto)                    THEN 'CAMBIA: descendiente'
        ELSE                                                                 'CAMBIA: rama distinta'
    END                                                                 AS efecto,
    ficha_id, id_trabajador, dni, trabajador, tipo, estado,
    puesto_id, puesto, puesto_activo,
    area_ficha_id, area_ficha,
    area_puesto_id, area_puesto
FROM base
WHERE area_ficha_id IS DISTINCT FROM area_puesto_id
ORDER BY 1, 2, area_puesto, trabajador;


-- -----------------------------------------------------------------------------
-- 3) PUESTOS SIN DESTINO que usan fichas ACTIVAS no-obra. Arreglar el puesto
--    resuelve de golpe a todas sus fichas: es el camino mas barato de P1.
-- -----------------------------------------------------------------------------
SELECT
    p.puesto_id,
    p.nombre                                                            AS puesto,
    p.active                                                            AS puesto_activo,
    p.area_solicitante_scope_id                                         AS area_solicitante_id,
    COUNT(*)                                                            AS fichas_activas_no_obra,
    COUNT(w.area_scope_id)                                              AS fichas_que_perderian_area,
    STRING_AGG(DISTINCT w.area_scope_id::text, ', ')                    AS areas_hoy_en_esas_fichas
FROM workers w
JOIN puesto          p  ON p.puesto_id = w.puesto_id
JOIN workers_estado  we ON we.workers_estado_id = w.workers_estado_id
WHERE w.state
  AND we.esta_adentro
  AND (w.obra_oficina_staff_id IS DISTINCT FROM 1)
  AND p.area_destino_scope_id IS NULL
GROUP BY 1, 2, 3, 4
ORDER BY 6 DESC, 5 DESC, 2;


-- -----------------------------------------------------------------------------
-- 4) FICHAS ACTIVAS NO-OBRA SIN PUESTO: no hay de donde derivar el area. Si
--    ademas tienen area hoy, la pierden. Se arreglan asignando puesto.
-- -----------------------------------------------------------------------------
SELECT
    w.id                                                                AS ficha_id,
    w.id_trabajador,
    pe.document_identity_code                                           AS dni,
    COALESCE(pe.full_name, w.apellido_nombre)                           AS trabajador,
    COALESCE(oo.name, '(sin tipo)')                                     AS tipo,
    we.nombre                                                           AS estado,
    w.area_scope_id                                                     AS area_ficha_id,
    w.area                                                              AS area_texto_legacy,
    w.subarea                                                           AS subarea_texto_legacy
FROM workers w
LEFT JOIN person                     pe ON pe.person_id = w.person_id
LEFT JOIN workers_obra_oficina_staff oo ON oo.workers_obra_oficina_staff_id = w.obra_oficina_staff_id
JOIN      workers_estado             we ON we.workers_estado_id = w.workers_estado_id
WHERE w.state
  AND we.esta_adentro
  AND (w.obra_oficina_staff_id IS DISTINCT FROM 1)
  AND w.puesto_id IS NULL
ORDER BY 4;
