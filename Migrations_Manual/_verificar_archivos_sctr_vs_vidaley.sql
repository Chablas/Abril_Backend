-- SOLO LECTURA. Corrige el diagnóstico anterior: esta vez trae el archivo_url
-- REAL de cada ítem (SCTR y Vida Ley) para cada trabajador de Oficina
-- Central/Staff, en vez de asumir nada por coincidencia de fechas.
--
-- Compara explícitamente si es el MISMO archivo o uno DISTINTO para cada
-- trabajador, así se puede confirmar (no suponer) si el mismo PDF se usó
-- para ambos ítems o si son documentos distintos.

SELECT
    p.document_identity_code AS dni,
    p.full_name              AS nombre,
    c.contributor_name       AS empresa,
    CASE w.obra_oficina_staff_id WHEN 1 THEN 'Obra' WHEN 2 THEN 'Staff' WHEN 3 THEN 'Oficina Central' WHEN 4 THEN 'Personal Externo' END AS obra_oficina,
    hs.estado                AS estado_sctr,
    hs.vigencia              AS vigencia_sctr,
    hs.archivo_url           AS archivo_sctr,
    hv.estado                AS estado_vidaley,
    hv.vigencia              AS vigencia_vidaley,
    hv.archivo_url           AS archivo_vidaley,
    CASE
        WHEN hs.archivo_url IS NULL OR hv.archivo_url IS NULL THEN 'NO COMPARABLE (falta uno de los dos)'
        WHEN hs.archivo_url = hv.archivo_url THEN 'MISMO ARCHIVO EN AMBOS ITEMS'
        ELSE 'ARCHIVOS DISTINTOS'
    END AS comparacion
FROM workers w
LEFT JOIN person p ON p.person_id = w.person_id
LEFT JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
LEFT JOIN contributor c ON c.contributor_id = v.empresa_id
LEFT JOIN ss_item_trabajador its ON its.es_sctr_vidaley = true AND its.activo = true AND its.nombre ILIKE '%SCTR%'
LEFT JOIN ss_hab_trabajador hs ON hs.worker_id = w.id AND hs.item_id = its.id
LEFT JOIN ss_item_trabajador itv ON itv.es_sctr_vidaley = true AND itv.activo = true AND itv.nombre ILIKE '%Vida%'
LEFT JOIN ss_hab_trabajador hv ON hv.worker_id = w.id AND hv.item_id = itv.id
WHERE w.obra_oficina_staff_id IN (2, 3)
  AND (hs.archivo_url IS NOT NULL OR hv.archivo_url IS NOT NULL)
ORDER BY comparacion DESC, empresa, nombre;
