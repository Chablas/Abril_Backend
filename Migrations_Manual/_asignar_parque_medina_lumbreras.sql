-- PARQUE MEDINA JAFFET JULINHO (worker_id 13129) ya tiene 3 asignaciones de
-- apoyo activas a LUMBRERAS (empresa_id 567) via ss_hab_worker_proyecto:
-- KAURI (7), CAMELIA (6), CEDRO 33 (8). Nunca tuvo vinculación PRINCIPAL
-- (worker_vinculaciones), que es lo que lee la pantalla de Trabajadores para
-- mostrar la empresa/obra. Se la creamos apuntando a KAURI (confirmado).

INSERT INTO worker_vinculaciones (worker_id, empresa_id, proyecto_id, categoria_id, fecha_inicio, created_at)
SELECT 13129, 567, 7,
       (SELECT categoria_id FROM puesto WHERE puesto_id = 317),
       CURRENT_DATE,
       now();

-- Sincronizar su SCTR (queda "Aprobado" sin fecha) con la vigencia real de
-- la póliza de Lumbreras ya aprobada más reciente.
UPDATE ss_hab_trabajador h
SET vigencia = (
        SELECT s.vigencia
        FROM ss_sctr_vidaley s
        JOIN ss_sctr_vidaley_worker svw ON svw.sctr_vidaley_id = s.id
        WHERE svw.worker_id = 13129
          AND s.tipo = 'SCTR'
          AND s.estado = 'Aprobado'
          AND s.vigencia IS NOT NULL
        ORDER BY s.anio DESC, s.mes DESC, s.vigencia DESC
        LIMIT 1
    ),
    updated_at = now()
WHERE h.worker_id = 13129
  AND h.item_id = (SELECT id FROM ss_item_trabajador WHERE nombre = 'SCTR')
  AND h.estado = 'Aprobado'
  AND h.vigencia IS NULL;
