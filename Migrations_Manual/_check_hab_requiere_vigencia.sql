-- Ver qué items realmente requieren vigencia (para descartar falsos positivos:
-- items donde Vigencia=NULL es esperado/correcto porque no vencen nunca).
SELECT id, nombre, requiere_vigencia, aplica_a
FROM ss_item_trabajador
ORDER BY nombre;

-- Repetimos el conteo de "Aprobado + Vigencia NULL" pero SOLO para items
-- donde requiere_vigencia = true (estos SI son bug real, ej. SCTR, EMO).
SELECT
    CASE WHEN lower(trim(w.contrata_casa)) = 'casa' THEN 'Casa' ELSE 'Contratista' END AS tipo,
    it.nombre AS item,
    COUNT(*) AS cantidad
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE h.estado = 'Aprobado'
  AND h.vigencia IS NULL
  AND it.requiere_vigencia = true
GROUP BY 1, 2
ORDER BY cantidad DESC;
