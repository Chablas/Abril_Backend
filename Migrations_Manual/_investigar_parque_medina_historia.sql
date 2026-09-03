SELECT w.id, w.created_at, w.updated_at, w.contrata_casa, w.puesto_id, w.obra_oficina_staff_id
FROM workers w
WHERE w.id = 13129;

SELECT h.id AS hab_id, it.nombre AS item, h.estado, h.vigencia, h.archivo_url,
       h.aprobado_por, h.fecha_aprobacion, h.created_at, h.updated_at
FROM ss_hab_trabajador h
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE h.worker_id = 13129
ORDER BY it.nombre;

-- Eventos del trabajador (altas, cambios de obra, etc.)
SELECT * FROM worker_eventos WHERE worker_id = 13129 ORDER BY created_at;
