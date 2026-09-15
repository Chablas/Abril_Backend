-- Octubre quedó con parejas duplicadas/inconsistentes por una condición de carrera al navegar
-- rápido entre meses (dos llamadas generaron el mes con desplazamientos distintos). Se borra
-- Octubre 2026 y se regenera limpio, usando offset = SiguienteOffset(1, 8) = 2, a partir del
-- cursor de setiembre. Ejecutar en pgAdmin.

BEGIN;

DO $$
DECLARE
    v_anillo integer;
BEGIN
    SELECT id INTO v_anillo FROM ss_inspeccion_cruzada_anillo LIMIT 1;

    DELETE FROM ss_inspeccion_cruzada_programacion
    WHERE anio = 2026 AND mes = 10 AND anillo_id = v_anillo;

    INSERT INTO ss_inspeccion_cruzada_programacion (anio, mes, anillo_id, proyecto_inspector_id, proyecto_inspeccionado_id)
    SELECT 2026, 10, v_anillo, r1.proyecto_id, r2.proyecto_id
    FROM ss_inspeccion_cruzada_rotacion r1
    JOIN ss_inspeccion_cruzada_rotacion r2
      ON r2.anillo_id = v_anillo AND r2.orden = (r1.orden + 2) % 8
    WHERE r1.anillo_id = v_anillo;

    UPDATE ss_inspeccion_cruzada_cursor
    SET offset_valor = 2, ultimo_anio = 2026, ultimo_mes = 10, updated_at = now()
    WHERE anillo_id = v_anillo;
END $$;

-- Verificación: debe mostrar exactamente 8 filas, cada proyecto como inspector una sola vez.
SELECT pi.project_description AS inspector, pd.project_description AS inspeccionado
FROM ss_inspeccion_cruzada_programacion prog
JOIN project pi ON pi.project_id = prog.proyecto_inspector_id
JOIN project pd ON pd.project_id = prog.proyecto_inspeccionado_id
WHERE prog.anio = 2026 AND prog.mes = 10
ORDER BY pi.project_description;

COMMIT;
