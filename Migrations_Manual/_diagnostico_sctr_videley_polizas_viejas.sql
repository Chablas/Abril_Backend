-- Diagnóstico SOLO LECTURA de pólizas SCTR/VidaLey no resueltas (Enviado/En
-- revision), clasificando a sus trabajadores contra el estado REAL actual en
-- ss_hab_trabajador (que es compartido entre todas las pólizas del mismo
-- trabajador+ítem, no versionado por mes). El objetivo es separar "ruido"
-- (la póliza vieja quedó sin resolver pero el trabajador ya está vigente por
-- una póliza más nueva) de "pendiente real".

-- 1) Resumen: pólizas no resueltas por mes/año/tipo/estado, con cantidad de
--    trabajadores y cuántas están vacías (0 trabajadores = candidatas a limpiar
--    directo, sin tocar ss_hab_trabajador).
SELECT
    s.tipo,
    s.anio,
    s.mes,
    s.estado,
    COUNT(DISTINCT s.id) AS polizas,
    COUNT(svw.worker_id) AS filas_worker,
    COUNT(DISTINCT s.id) FILTER (WHERE svw.worker_id IS NULL) AS polizas_vacias
FROM ss_sctr_vidaley s
LEFT JOIN ss_sctr_vidaley_worker svw ON svw.sctr_vidaley_id = s.id
WHERE s.estado IN ('Enviado', 'En revision', 'Parcial')
GROUP BY 1, 2, 3, 4
ORDER BY s.anio DESC, s.mes DESC, s.tipo, s.estado;

-- 2) Detalle por trabajador: para cada póliza no resuelta, compara contra el
--    estado ACTUAL de ss_hab_trabajador (la fila real y única que manda hoy).
SELECT
    s.id AS poliza_id,
    s.tipo,
    s.anio,
    s.mes,
    s.estado AS estado_poliza,
    c.contributor_name AS empresa,
    pr.project_description AS proyecto,
    p.document_identity_code AS dni,
    p.full_name AS nombre,
    h.estado AS estado_hab_actual,
    h.vigencia AS vigencia_hab_actual,
    CASE
        WHEN h.id IS NULL THEN 'SIN REGISTRO EN ss_hab_trabajador (raro, revisar)'
        WHEN h.estado = 'Aprobado' AND h.vigencia IS NOT NULL AND h.vigencia >= now()
            THEN 'YA VIGENTE (otra póliza más nueva lo resolvió) — ESTA PÓLIZA ES RUIDO'
        WHEN h.estado = 'Aprobado' AND h.vigencia IS NULL
            THEN 'BUG CONFIRMADO: Aprobado sin fecha'
        WHEN h.estado = 'Aprobado' AND h.vigencia < now()
            THEN 'APROBADO PERO VENCIDO'
        WHEN h.estado IN ('Enviado', 'En revision') THEN 'PENDIENTE REAL (consistente con la póliza)'
        ELSE 'OTRO: ' || COALESCE(h.estado, 'null')
    END AS diagnostico
FROM ss_sctr_vidaley s
JOIN ss_sctr_vidaley_worker svw ON svw.sctr_vidaley_id = s.id
JOIN workers w ON w.id = svw.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
LEFT JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
LEFT JOIN contributor c ON c.contributor_id = v.empresa_id
LEFT JOIN project pr ON pr.project_id = v.proyecto_id
LEFT JOIN ss_item_trabajador it ON it.es_sctr_vidaley = true AND it.activo = true
    AND (CASE WHEN s.tipo = 'VIDA_LEY' THEN it.nombre ILIKE '%Vida%' ELSE it.nombre ILIKE '%SCTR%' END)
LEFT JOIN ss_hab_trabajador h ON h.worker_id = w.id AND h.item_id = it.id
WHERE s.estado IN ('Enviado', 'En revision', 'Parcial')
ORDER BY s.anio DESC, s.mes DESC, s.tipo, diagnostico, nombre;
