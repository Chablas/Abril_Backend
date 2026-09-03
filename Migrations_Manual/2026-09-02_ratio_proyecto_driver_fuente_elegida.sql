-- Permite al responsable elegir, por proyecto y por driver (HH / TRABAJADORES), CUAL de los 3
-- valores usar para el ratio (Proyectado | Calculado/acumulado real | Manual/real final), o
-- ninguno (excluido). La eleccion queda guardada y no se pisa en recalculos posteriores,
-- igual que incluido_manual.

ALTER TABLE ss_ratio_proyecto_driver
    ADD COLUMN IF NOT EXISTS cantidad_proyectado numeric,
    ADD COLUMN IF NOT EXISTS fuente_cantidad varchar(20);

-- fuente_cantidad: 'PROYECTADO' | 'CALCULADO' | 'MANUAL' | NULL (ninguno elegido todavia)
