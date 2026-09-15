-- Corrección final: MÁXIMO ABRIL (project_id 11) se perdió del anillo (orden 5 vacío), y las
-- parejas de setiembre quedaron generadas como ciclo de 6 en vez de ciclo de 8 (ya se unificó
-- todo en un solo anillo). Este script:
--   1) Asegura que MÁXIMO ABRIL esté en el anillo, orden 5.
--   2) Regenera las 8 parejas de setiembre 2026 como un solo ciclo de 8.
-- Ejecutar en pgAdmin.

BEGIN;

DO $$
DECLARE
    v_anillo integer;
BEGIN
    SELECT id INTO v_anillo FROM ss_inspeccion_cruzada_anillo LIMIT 1;

    INSERT INTO ss_inspeccion_cruzada_rotacion (anillo_id, proyecto_id, orden)
    VALUES (v_anillo, 11, 5)
    ON CONFLICT (proyecto_id) DO UPDATE SET orden = 5, anillo_id = v_anillo;

    DELETE FROM ss_inspeccion_cruzada_programacion
    WHERE anio = 2026 AND mes = 9 AND anillo_id = v_anillo;

    INSERT INTO ss_inspeccion_cruzada_programacion (anio, mes, anillo_id, proyecto_inspector_id, proyecto_inspeccionado_id)
    SELECT 2026, 9, v_anillo, r1.proyecto_id, r2.proyecto_id
    FROM ss_inspeccion_cruzada_rotacion r1
    JOIN ss_inspeccion_cruzada_rotacion r2
      ON r2.anillo_id = v_anillo AND r2.orden = (r1.orden + 1) % 8
    WHERE r1.anillo_id = v_anillo;

    -- El cursor debe reflejar que setiembre (offset 1) ya se generó para el anillo de 8.
    UPDATE ss_inspeccion_cruzada_cursor
    SET offset_valor = 1, ultimo_anio = 2026, ultimo_mes = 9, updated_at = now()
    WHERE anillo_id = v_anillo;
END $$;

-- Verificación: debe mostrar 8 filas, un solo ciclo conectando los 8 proyectos.
SELECT pi.project_description AS inspector, pd.project_description AS inspeccionado
FROM ss_inspeccion_cruzada_programacion prog
JOIN project pi ON pi.project_id = prog.proyecto_inspector_id
JOIN project pd ON pd.project_id = prog.proyecto_inspeccionado_id
WHERE prog.anio = 2026 AND prog.mes = 9
ORDER BY pi.project_description;

COMMIT;
