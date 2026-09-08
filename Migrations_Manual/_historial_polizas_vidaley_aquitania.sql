-- SOLO LECTURA. A diferencia de ss_hab_documento_version (que no registra
-- nada cuando el archivo se sobreescribe vía el flujo masivo de
-- SctrVidaLeyRepository), cada póliza mensual en ss_sctr_vidaley SÍ guarda
-- su propio archivo_url. Si Paola Quispe (pquispe@abril.pe) subió el Vida
-- Ley correcto en un mes anterior a septiembre 2026, debe aparecer acá como
-- una póliza más antigua para el mismo trabajador, con un archivo distinto
-- a "20260901_AQUITANIA.pdf".

SELECT
    p.document_identity_code AS dni,
    p.full_name              AS nombre,
    s.id                     AS poliza_id,
    s.anio,
    s.mes,
    s.tipo,
    s.estado,
    s.vigencia,
    s.archivo_url,
    s.archivo_url2,
    s.created_at,
    s.updated_at
FROM ss_sctr_vidaley s
JOIN ss_sctr_vidaley_worker svw ON svw.sctr_vidaley_id = s.id
JOIN workers w ON w.id = svw.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
LEFT JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
LEFT JOIN contributor c ON c.contributor_id = v.empresa_id
WHERE s.tipo = 'VIDA_LEY'
  AND c.contributor_name ILIKE '%Aquitania%'
ORDER BY nombre, s.anio DESC, s.mes DESC;
