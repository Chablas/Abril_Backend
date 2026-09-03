-- Verifica los 4 trabajadores de la póliza VIDA_LEY / ALFA CO / Agosto 2026
-- (En revision): estado real actual y si tienen alguna póliza de SETIEMBRE
-- que se pudiera ver afectada al aprobar/rechazar esta póliza vieja.
SELECT p.document_identity_code AS dni, p.full_name AS nombre,
       h.estado AS estado_hab_actual, h.vigencia AS vigencia_hab_actual
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
JOIN person p ON p.person_id = w.person_id
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE p.document_identity_code IN ('76640953','60828187','70831064','08158300')
  AND it.nombre = 'Vida ley';

SELECT p.document_identity_code AS dni, s.id AS poliza_id, s.anio, s.mes, s.estado, s.vigencia
FROM ss_sctr_vidaley s
JOIN ss_sctr_vidaley_worker svw ON svw.sctr_vidaley_id = s.id
JOIN workers w ON w.id = svw.worker_id
JOIN person p ON p.person_id = w.person_id
WHERE p.document_identity_code IN ('76640953','60828187','70831064','08158300')
  AND s.tipo = 'VIDA_LEY'
ORDER BY dni, s.anio DESC, s.mes DESC;
