-- SOLO LECTURA. Verifica si BACA ARIAS SCOOT ADERLI (DNI 70052564) tiene más
-- de un registro en workers, y cuál de ellos tiene la vinculación activa
-- que usa el frontend para mostrarlo en la lista de Trabajadores. Si hay más
-- de un worker_id, puede que estemos corrigiendo un hab_trabajador que no es
-- el que la pantalla realmente lee.

SELECT
    w.id AS worker_id,
    w.workers_estado_id,
    we.nombre AS estado_worker,
    w.contrata_casa,
    w.obra_oficina_staff_id,
    vinc.id AS vinculacion_id,
    vinc.fecha_fin AS vinculacion_fecha_fin,
    vinc.created_at AS vinculacion_created_at,
    h.id AS hab_trabajador_id,
    h.archivo_url,
    h.vigencia
FROM person per
JOIN workers w ON w.person_id = per.person_id
LEFT JOIN worker_vinculaciones vinc ON vinc.worker_id = w.id
LEFT JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
LEFT JOIN ss_item_trabajador it ON it.es_sctr_vidaley = true AND it.activo = true AND it.nombre ILIKE '%Vida%'
LEFT JOIN ss_hab_trabajador h ON h.worker_id = w.id AND h.item_id = it.id
WHERE per.document_identity_code = '70052564'
ORDER BY w.id, vinc.created_at DESC;
