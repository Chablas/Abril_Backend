-- SOLO LECTURA. Generaliza el hallazgo de Aquitania a TODAS las empresas:
-- busca trabajadores Staff/Oficina Central cuya póliza Vida Ley MÁS
-- RECIENTE tiene un archivo que no está nombrado como Vida Ley (no contiene
-- "vida" ni "_VL" en el nombre), mientras que SÍ existe una póliza Vida Ley
-- anterior del mismo trabajador cuyo archivo sí tiene ese patrón — la misma
-- señal que confirmó el caso Aquitania: el archivo bueno existe, solo quedó
-- tapado por una carga posterior con el documento equivocado.
--
-- No es 100% preciso (depende de que el archivo correcto haya sido nombrado
-- con esa convención), pero es el mismo criterio ya validado a mano.

WITH polizas_vl AS (
    SELECT
        s.id, s.anio, s.mes, s.archivo_url, s.updated_at,
        svw.worker_id,
        (s.archivo_url ILIKE '%vida%' OR s.archivo_url ILIKE '%_vl%' OR s.archivo_url ILIKE '%_vl_%') AS nombre_parece_vidaley
    FROM ss_sctr_vidaley s
    JOIN ss_sctr_vidaley_worker svw ON svw.sctr_vidaley_id = s.id
    WHERE s.tipo = 'VIDA_LEY'
),
ultima AS (
    SELECT DISTINCT ON (worker_id)
        worker_id, id AS poliza_id, archivo_url, anio, mes, nombre_parece_vidaley
    FROM polizas_vl
    ORDER BY worker_id, anio DESC, mes DESC, updated_at DESC
),
anterior_buena AS (
    SELECT DISTINCT ON (p.worker_id)
        p.worker_id, p.id AS poliza_id_anterior, p.archivo_url AS archivo_anterior, p.anio, p.mes
    FROM polizas_vl p
    WHERE p.nombre_parece_vidaley
    ORDER BY p.worker_id, p.anio DESC, p.mes DESC, p.updated_at DESC
)
SELECT
    per.document_identity_code AS dni,
    per.full_name            AS nombre,
    c.contributor_name       AS empresa,
    CASE w.obra_oficina_staff_id WHEN 1 THEN 'Obra' WHEN 2 THEN 'Staff' WHEN 3 THEN 'Oficina Central' WHEN 4 THEN 'Personal Externo' END AS obra_oficina,
    u.anio || '-' || u.mes    AS periodo_actual_sospechoso,
    u.archivo_url             AS archivo_actual_sospechoso,
    ab.anio || '-' || ab.mes  AS periodo_anterior_bueno,
    ab.archivo_anterior       AS archivo_recuperable
FROM ultima u
JOIN anterior_buena ab ON ab.worker_id = u.worker_id
JOIN workers w ON w.id = u.worker_id
LEFT JOIN person per ON per.person_id = w.person_id
LEFT JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
LEFT JOIN contributor c ON c.contributor_id = v.empresa_id
WHERE NOT u.nombre_parece_vidaley
  AND w.obra_oficina_staff_id IN (2, 3)
ORDER BY empresa, nombre;
