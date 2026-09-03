-- Limpieza final: cierra TODAS las pólizas SCTR/VidaLey de agosto-2026 o
-- antes que sigan "Enviado"/"En revision"/"Parcial", sin excepción — cerrar
-- la etiqueta de la póliza (ss_sctr_vidaley.Estado) NUNCA toca datos de
-- trabajador, así que no hay riesgo de afectar setiembre por esta parte.
-- Para ss_hab_trabajador (2b) SÍ se mantiene la protección real: solo se
-- alinea a "Rechazado" al trabajador cuyo estado actual siga abierto
-- (Enviado/En revision/Falta) — si ya está "Aprobado" (por setiembre o por
-- el sync anterior), NUNCA se toca.

-- 1) Vista previa de pólizas a cerrar (ya no filtra por "sin reciente")
SELECT s.id AS poliza_id, s.tipo, s.anio, s.mes, s.estado, c.contributor_name AS empresa
FROM ss_sctr_vidaley s
LEFT JOIN contributor c ON c.contributor_id = s.empresa_id
WHERE s.estado IN ('Enviado', 'En revision', 'Parcial')
  AND (s.anio < 2026 OR (s.anio = 2026 AND s.mes <= 8))
ORDER BY s.anio DESC, s.mes DESC;

-- 2a) UPDATE #1: cerrar TODAS esas pólizas (solo la etiqueta, cero riesgo)
UPDATE ss_sctr_vidaley s
SET estado = 'Rechazado',
    obs_abril = COALESCE(obs_abril, '') || ' [Cerrada automáticamente: período vencido]',
    updated_at = now()
WHERE s.estado IN ('Enviado', 'En revision', 'Parcial')
  AND (s.anio < 2026 OR (s.anio = 2026 AND s.mes <= 8));

-- 2b) UPDATE #2: alinear a "Rechazado" SOLO a los trabajadores de esas
--     pólizas cuyo estado actual siga abierto (Enviado/En revision/Falta).
--     Nunca toca a quien ya esté "Aprobado" (setiembre o sync previo).
WITH polizas_cerradas_ahora AS (
    SELECT s.id AS poliza_id, s.tipo
    FROM ss_sctr_vidaley s
    WHERE s.estado = 'Rechazado'
      AND s.obs_abril ILIKE '%Cerrada automáticamente: período vencido%'
      AND (s.anio < 2026 OR (s.anio = 2026 AND s.mes <= 8))
),
workers_a_rechazar AS (
    SELECT DISTINCT pc.tipo, svw.worker_id
    FROM polizas_cerradas_ahora pc
    JOIN ss_sctr_vidaley_worker svw ON svw.sctr_vidaley_id = pc.poliza_id
)
UPDATE ss_hab_trabajador h
SET estado = 'Rechazado',
    obs_abril = COALESCE(h.obs_abril, '') || ' [Rechazado automáticamente: póliza vencida sin renovación]',
    updated_at = now()
FROM workers_a_rechazar war
WHERE h.worker_id = war.worker_id
  AND h.item_id = (SELECT it.id FROM ss_item_trabajador it
                    WHERE (CASE WHEN war.tipo = 'VIDA_LEY' THEN it.nombre ILIKE '%Vida%' ELSE it.nombre ILIKE '%SCTR%' END)
                    LIMIT 1)
  AND h.estado IN ('Enviado', 'En revision', 'Falta');
