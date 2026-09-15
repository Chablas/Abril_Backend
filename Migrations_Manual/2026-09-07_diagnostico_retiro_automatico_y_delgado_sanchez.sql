-- ============================================================================
-- DIAGNÓSTICO (solo lectura) — 2 preguntas:
--   1) ¿Desde cuándo está Rechazado el EMO/Vida Ley de Delgado Sánchez Shoylin Rivaldo?
--   2) ¿Alguna vez se ejecutó de verdad el retiro automático (tiene su propia
--      tabla de log, ss_retiro_automatico_log)?
-- ============================================================================

-- 1) Historial de versiones (auditoría) de los ítems de Delgado Sánchez
SELECT it.nombre AS item, v.version, v.estado_al_subir, v.estado_anterior,
       v.created_at, v.motivo_rechazo
FROM ss_hab_documento_version v
JOIN ss_hab_trabajador h ON h.id = v.hab_trabajador_id
JOIN ss_item_trabajador it ON it.id = h.item_id
JOIN workers w ON w.id = h.worker_id
JOIN person p ON p.person_id = w.person_id
WHERE p.full_name ILIKE '%DELGADO%SANCHEZ%SHOYLIN%'
   OR p.full_name ILIKE '%SANCHEZ%DELGADO%SHOYLIN%'
ORDER BY v.created_at DESC
LIMIT 30;

-- Estado actual (por si el historial de versiones no cubre todo)
SELECT it.nombre AS item, h.estado, h.vigencia, h.updated_at
FROM ss_hab_trabajador h
JOIN ss_item_trabajador it ON it.id = h.item_id
JOIN workers w ON w.id = h.worker_id
JOIN person p ON p.person_id = w.person_id
WHERE p.full_name ILIKE '%DELGADO%SANCHEZ%SHOYLIN%'
ORDER BY it.id;

-- 2) ¿Alguna vez corrió el retiro automático? (tabla propia de log)
SELECT COUNT(*) AS total_ejecuciones_historicas,
       MIN(ejecutado_en) AS primera_vez,
       MAX(ejecutado_en) AS ultima_vez
FROM ss_retiro_automatico_log;

-- Detalle de las últimas 20 (si hay)
SELECT id, worker_id, empresa_id, motivo, tipo_retiro, dias_gracia,
       entregables_vencidos, ejecutado_en
FROM ss_retiro_automatico_log
ORDER BY ejecutado_en DESC
LIMIT 20;
