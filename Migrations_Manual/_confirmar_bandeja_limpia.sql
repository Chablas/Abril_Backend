SELECT s.id, s.tipo, s.anio, s.mes, s.estado
FROM ss_sctr_vidaley s
WHERE s.estado IN ('Enviado', 'En revision', 'Parcial')
  AND (s.anio < 2026 OR (s.anio = 2026 AND s.mes <= 8));
