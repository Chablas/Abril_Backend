-- Estado actual real de SILVA MENDOZA KARINA (DNI 72694399) para Vida ley,
-- y en qué otras pólizas (de qué mes) aparece, para saber si rechazar la
-- póliza vieja de agosto afectaría algo de setiembre.
SELECT h.id AS hab_id, h.estado, h.vigencia, h.updated_at
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
JOIN person p ON p.person_id = w.person_id
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE p.document_identity_code = '72694399'
  AND it.nombre = 'Vida ley';

SELECT s.id AS poliza_id, s.tipo, s.anio, s.mes, s.estado, s.vigencia
FROM ss_sctr_vidaley s
JOIN ss_sctr_vidaley_worker svw ON svw.sctr_vidaley_id = s.id
JOIN workers w ON w.id = svw.worker_id
JOIN person p ON p.person_id = w.person_id
WHERE p.document_identity_code = '72694399'
  AND s.tipo = 'VIDA_LEY'
ORDER BY s.anio DESC, s.mes DESC;
