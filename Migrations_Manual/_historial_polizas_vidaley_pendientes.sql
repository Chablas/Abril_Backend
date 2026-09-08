-- SOLO LECTURA. Historial completo de pólizas Vida Ley para los
-- trabajadores de Bahia de Oro, Lares, Neo Inversiones y Seshat que todavía
-- muestran el archivo de lote sospechoso en ss_hab_trabajador. Mismo
-- propósito que _buscar_historial_vidaley_aquitania.sql: encontrar, para
-- cada uno, la póliza Vida Ley anterior (con archivo distinto al del lote
-- actual) para poder restaurar el archivo correcto.

SELECT
    per.document_identity_code AS dni,
    per.full_name              AS nombre,
    c.contributor_name         AS empresa,
    s.id                       AS poliza_id,
    s.anio,
    s.mes,
    s.archivo_url,
    s.updated_at
FROM ss_sctr_vidaley s
JOIN ss_sctr_vidaley_worker svw ON svw.sctr_vidaley_id = s.id
JOIN workers w ON w.id = svw.worker_id
LEFT JOIN person per ON per.person_id = w.person_id
LEFT JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
LEFT JOIN contributor c ON c.contributor_id = v.empresa_id
WHERE s.tipo = 'VIDA_LEY'
  AND per.document_identity_code IN (
    '10713842','71660883','74031156','76325920','70124175','42377772','46579206','70565134',
    '70982675','71542068','72410294','72726217',
    '72531024',
    '46557961','43091178','70274904','73381575','76841365'
  )
ORDER BY nombre, s.anio DESC, s.mes DESC;
