-- Verifica el estado actual después del UPDATE de sincronización: cuántos
-- quedan sin fecha (deberían ser solo los 93 sin póliza con fecha), y de los
-- que ya tienen fecha, cuántos están realmente vigentes (>= hoy) vs ya vencidos
-- (la póliza copiada podría estar vencida si nadie renovó después).
SELECT
    it.nombre AS item,
    COUNT(*) FILTER (WHERE h.vigencia IS NULL) AS aun_sin_fecha,
    COUNT(*) FILTER (WHERE h.vigencia IS NOT NULL AND h.vigencia >= now()) AS con_fecha_vigente,
    COUNT(*) FILTER (WHERE h.vigencia IS NOT NULL AND h.vigencia < now()) AS con_fecha_pero_vencida
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE it.nombre IN ('SCTR', 'Vida ley')
  AND h.estado = 'Aprobado'
  AND lower(trim(w.contrata_casa)) <> 'casa'
  AND we.nombre = 'Activo'
GROUP BY it.nombre;
