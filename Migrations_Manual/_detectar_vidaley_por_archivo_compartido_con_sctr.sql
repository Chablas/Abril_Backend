-- SOLO LECTURA. Detección precisa (no heurística por nombre): busca
-- trabajadores Staff/Oficina Central cuya póliza Vida Ley MÁS RECIENTE usa
-- un archivo_url que TAMBIÉN existe, exactamente igual, en una póliza tipo
-- SCTR — es decir, el mismo archivo fue cargado para los dos tipos. Esto
-- prueba de forma directa el mismo bug ya confirmado en Aquitania (poliza
-- 3065), sin depender de que el nombre del archivo contenga "vida"/"VL".
--
-- Para cada caso, trae también la póliza Vida Ley INMEDIATAMENTE ANTERIOR
-- (por fecha) que NO comparte archivo con ninguna póliza SCTR, como
-- candidato a archivo correcto para restaurar.

WITH polizas_vl AS (
    SELECT s.id, s.anio, s.mes, s.archivo_url, s.updated_at, svw.worker_id
    FROM ss_sctr_vidaley s
    JOIN ss_sctr_vidaley_worker svw ON svw.sctr_vidaley_id = s.id
    WHERE s.tipo = 'VIDA_LEY'
),
archivos_sctr AS (
    SELECT DISTINCT archivo_url FROM ss_sctr_vidaley WHERE tipo = 'SCTR' AND archivo_url IS NOT NULL
),
polizas_vl_marcadas AS (
    SELECT p.*, (p.archivo_url IN (SELECT archivo_url FROM archivos_sctr)) AS archivo_es_sctr
    FROM polizas_vl p
),
ultima AS (
    SELECT DISTINCT ON (worker_id)
        worker_id, id AS poliza_id, archivo_url, anio, mes, archivo_es_sctr
    FROM polizas_vl_marcadas
    ORDER BY worker_id, anio DESC, mes DESC, updated_at DESC
),
anterior_limpia AS (
    SELECT DISTINCT ON (worker_id)
        worker_id, id AS poliza_id_anterior, archivo_url AS archivo_anterior, anio, mes
    FROM polizas_vl_marcadas
    WHERE NOT archivo_es_sctr
    ORDER BY worker_id, anio DESC, mes DESC, updated_at DESC
)
SELECT
    per.document_identity_code AS dni,
    per.full_name              AS nombre,
    c.contributor_name         AS empresa,
    CASE w.obra_oficina_staff_id WHEN 1 THEN 'Obra' WHEN 2 THEN 'Staff' WHEN 3 THEN 'Oficina Central' WHEN 4 THEN 'Personal Externo' END AS obra_oficina,
    h.id                        AS hab_trabajador_id,
    h.archivo_url               AS archivo_actual_en_hab,
    u.anio || '-' || u.mes      AS periodo_sospechoso,
    u.archivo_url               AS archivo_sospechoso,
    an.anio || '-' || an.mes    AS periodo_anterior_limpio,
    an.archivo_anterior         AS archivo_recuperable
FROM ultima u
JOIN anterior_limpia an ON an.worker_id = u.worker_id
JOIN workers w ON w.id = u.worker_id
LEFT JOIN person per ON per.person_id = w.person_id
LEFT JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
LEFT JOIN contributor c ON c.contributor_id = v.empresa_id
LEFT JOIN ss_item_trabajador it ON it.es_sctr_vidaley = true AND it.activo = true AND it.nombre ILIKE '%Vida%'
LEFT JOIN ss_hab_trabajador h ON h.worker_id = w.id AND h.item_id = it.id
WHERE u.archivo_es_sctr
  AND w.obra_oficina_staff_id IN (2, 3)
ORDER BY empresa, nombre;
