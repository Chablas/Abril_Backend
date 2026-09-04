-- Igual que el 2b anterior pero usando los IDs de póliza directos (evita
-- problemas de coincidencia de texto con tildes en obs_abril).
WITH polizas_cerradas_ahora AS (
    SELECT id AS poliza_id, tipo
    FROM ss_sctr_vidaley
    WHERE id IN (1913, 2065, 2067, 1183, 1383, 360, 357, 339)
),
workers_a_rechazar AS (
    SELECT DISTINCT pc.tipo, svw.worker_id
    FROM polizas_cerradas_ahora pc
    JOIN ss_sctr_vidaley_worker svw ON svw.sctr_vidaley_id = pc.poliza_id
)
UPDATE ss_hab_trabajador h
SET estado = 'Rechazado',
    obs_abril = COALESCE(h.obs_abril, '') || ' [Rechazado automaticamente: poliza vencida sin renovacion]',
    updated_at = now()
FROM workers_a_rechazar war
WHERE h.worker_id = war.worker_id
  AND h.item_id = (SELECT it.id FROM ss_item_trabajador it
                    WHERE (CASE WHEN war.tipo = 'VIDA_LEY' THEN it.nombre ILIKE '%Vida%' ELSE it.nombre ILIKE '%SCTR%' END)
                    LIMIT 1)
  AND h.estado IN ('Enviado', 'En revision', 'Falta');
