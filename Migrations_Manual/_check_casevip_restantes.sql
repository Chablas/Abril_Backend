-- Pólizas CASEVIP/ZUÑIGA (VIDA_LEY) que siguen "Enviado"/"En revision" tras
-- el cierre, con detalle de por qué no se cerraron (algún worker con
-- póliza de setiembre+, o no cumplió la condición).
SELECT s.id AS poliza_id, s.anio, s.mes, s.estado, c.contributor_name AS empresa,
       p.document_identity_code AS dni, p.full_name AS nombre,
       h.estado AS estado_hab_actual, h.vigencia,
       EXISTS (
           SELECT 1 FROM ss_sctr_vidaley s2
           JOIN ss_sctr_vidaley_worker svw2 ON svw2.sctr_vidaley_id = s2.id
           WHERE svw2.worker_id = svw.worker_id
             AND s2.tipo = 'VIDA_LEY'
             AND (s2.anio > 2026 OR (s2.anio = 2026 AND s2.mes > 8))
       ) AS tiene_poliza_setiembre_o_mas
FROM ss_sctr_vidaley s
LEFT JOIN contributor c ON c.contributor_id = s.empresa_id
JOIN ss_sctr_vidaley_worker svw ON svw.sctr_vidaley_id = s.id
JOIN workers w ON w.id = svw.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
LEFT JOIN ss_hab_trabajador h ON h.worker_id = w.id
    AND h.item_id = (SELECT id FROM ss_item_trabajador WHERE nombre ILIKE '%Vida%' LIMIT 1)
WHERE s.tipo = 'VIDA_LEY'
  AND s.estado IN ('Enviado','En revision')
  AND (s.anio < 2026 OR (s.anio = 2026 AND s.mes <= 8))
ORDER BY s.anio DESC, s.mes DESC, empresa, nombre;
