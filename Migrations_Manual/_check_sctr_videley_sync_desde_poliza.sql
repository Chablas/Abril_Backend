-- Para cada worker con Aprobado+sin fecha (activo, contratista), busca su
-- póliza más reciente Aprobada (misma empresa/tipo) que SÍ tenga Vigencia.
-- Si existe, es un simple problema de sincronización (se puede copiar la
-- fecha de la póliza al trabajador) y no revisión manual documento por
-- documento.
WITH pendientes AS (
    SELECT h.id AS hab_id, h.worker_id, it.nombre AS item, w.id AS wid,
           v.empresa_id
    FROM ss_hab_trabajador h
    JOIN workers w ON w.id = h.worker_id
    LEFT JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
    JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
    JOIN ss_item_trabajador it ON it.id = h.item_id
    WHERE it.nombre IN ('SCTR', 'Vida ley')
      AND h.estado = 'Aprobado'
      AND h.vigencia IS NULL
      AND lower(trim(w.contrata_casa)) <> 'casa'
      AND we.nombre = 'Activo'
),
mejor_poliza AS (
    SELECT p.wid, p.hab_id, p.item,
        (SELECT s.vigencia
         FROM ss_sctr_vidaley s
         JOIN ss_sctr_vidaley_worker svw ON svw.sctr_vidaley_id = s.id
         WHERE svw.worker_id = p.wid
           AND s.tipo = (CASE WHEN p.item = 'Vida ley' THEN 'VIDA_LEY' ELSE 'SCTR' END)
           AND s.estado = 'Aprobado'
           AND s.vigencia IS NOT NULL
         ORDER BY s.anio DESC, s.mes DESC, s.vigencia DESC
         LIMIT 1) AS vigencia_poliza
    FROM pendientes p
)
SELECT
    item,
    COUNT(*) AS total,
    COUNT(*) FILTER (WHERE vigencia_poliza IS NOT NULL) AS sincronizable_desde_poliza,
    COUNT(*) FILTER (WHERE vigencia_poliza IS NULL) AS sin_poliza_con_fecha
FROM mejor_poliza
GROUP BY item;
