-- DIAGNÓSTICO SOLO LECTURA: pólizas tipo VIDA_LEY cargadas para trabajadores
-- Oficina Central / Staff, mientras el formulario forzaba tipo='VIDA_LEY' sin
-- importar el documento subido (bug corregido en sctr-subir.ts). El objetivo
-- es que un humano revise el archivo_url de cada póliza y decida si es:
--   a) Vida Ley genuina (D.Leg. 688) -> no requiere acción.
--   b) En realidad una constancia SCTR (Ley 26790, "trabajo de riesgo") que
--      quedó mal etiquetada -> crear/duplicar también como póliza SCTR.
--
-- No modifica nada. Devuelve una fila por póliza VIDA_LEY con sus workers y
-- el estado actual que tienen en el ítem SCTR (11), para ver de un vistazo
-- si además les falta SCTR.

SELECT
    s.id                    AS poliza_vidaley_id,
    s.estado                AS estado_poliza,
    s.anio,
    s.mes,
    s.fecha_inicio,
    s.vigencia              AS vigencia_poliza,
    s.archivo_url,
    c.contributor_name      AS empresa,
    pr.project_description  AS proyecto,
    p.document_identity_code AS dni,
    p.full_name             AS nombre,
    CASE w.obra_oficina_staff_id WHEN 1 THEN 'Obra' WHEN 2 THEN 'Staff' WHEN 3 THEN 'Oficina Central' WHEN 4 THEN 'Personal Externo' END AS obra_oficina,
    hv.estado               AS estado_item_vidaley,
    hv.vigencia             AS vigencia_item_vidaley,
    hs.estado               AS estado_item_sctr_actual,
    hs.vigencia             AS vigencia_item_sctr_actual,
    CASE
        WHEN hs.id IS NULL THEN 'SIN ITEM SCTR — candidato a revisar si el documento aplica'
        WHEN hs.estado = 'Aprobado' AND hs.vigencia IS NOT NULL THEN 'YA TIENE SCTR APROBADO — probablemente no necesita nada'
        ELSE 'ITEM SCTR EXISTE PERO INCOMPLETO: ' || COALESCE(hs.estado, 'null')
    END AS diagnostico
FROM ss_sctr_vidaley s
JOIN ss_sctr_vidaley_worker svw ON svw.sctr_vidaley_id = s.id
JOIN workers w ON w.id = svw.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
LEFT JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
LEFT JOIN contributor c ON c.contributor_id = v.empresa_id
LEFT JOIN project pr ON pr.project_id = v.proyecto_id
LEFT JOIN ss_item_trabajador itv ON itv.es_sctr_vidaley = true AND itv.activo = true AND itv.nombre ILIKE '%Vida%'
LEFT JOIN ss_hab_trabajador hv ON hv.worker_id = w.id AND hv.item_id = itv.id
LEFT JOIN ss_item_trabajador its ON its.es_sctr_vidaley = true AND its.activo = true AND its.nombre ILIKE '%SCTR%'
LEFT JOIN ss_hab_trabajador hs ON hs.worker_id = w.id AND hs.item_id = its.id
WHERE s.tipo = 'VIDA_LEY'
  AND w.obra_oficina_staff_id IN (2, 3) -- Staff (2) y Oficina Central (3), ver ObraOficinaStaffIds.cs
ORDER BY s.anio DESC, s.mes DESC, nombre;
