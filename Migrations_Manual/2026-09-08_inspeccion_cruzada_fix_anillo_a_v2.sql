-- KAURÍ (con tilde, project_id 7) sí existe — el anillo A vuelve a ser de 6 proyectos, no 5.
-- Este script corrige lo que dejó 2026-09-08_inspeccion_cruzada_fix_anillo_a.sql: reordena
-- SAUCE ZEN / MÁXIMO ABRIL para dejar espacio a KAURÍ e inserta las parejas correctas.
-- Orden final: CEDRO 33(0) → GRAN MANZANO(1) → BOSQUE REAL(2) → KAURÍ(3) → SAUCE ZEN(4) →
-- MÁXIMO ABRIL(5) → CEDRO 33.
-- Ejecutar en pgAdmin DESPUÉS de los dos scripts anteriores.

BEGIN;

DO $$
DECLARE
    v_anillo_a integer;
BEGIN
    SELECT id INTO v_anillo_a FROM ss_inspeccion_cruzada_anillo WHERE nombre = 'Anillo A';

    -- Corre las posiciones de SAUCE ZEN (9) y MÁXIMO ABRIL (11) un puesto para abrir el 3.
    UPDATE ss_inspeccion_cruzada_rotacion SET orden = 4 WHERE anillo_id = v_anillo_a AND proyecto_id = 9;
    UPDATE ss_inspeccion_cruzada_rotacion SET orden = 5 WHERE anillo_id = v_anillo_a AND proyecto_id = 11;

    INSERT INTO ss_inspeccion_cruzada_rotacion (anillo_id, proyecto_id, orden)
    VALUES (v_anillo_a, 7, 3)
    ON CONFLICT (proyecto_id) DO NOTHING;

    -- Borra las parejas de setiembre generadas con el orden viejo (5 miembros) para este anillo.
    DELETE FROM ss_inspeccion_cruzada_programacion
    WHERE anio = 2026 AND mes = 9 AND anillo_id = v_anillo_a;

    -- Regenera las 6 parejas correctas del anillo de 6.
    INSERT INTO ss_inspeccion_cruzada_programacion (anio, mes, anillo_id, proyecto_inspector_id, proyecto_inspeccionado_id)
    SELECT 2026, 9, v_anillo_a, r1.proyecto_id, r2.proyecto_id
    FROM ss_inspeccion_cruzada_rotacion r1
    JOIN ss_inspeccion_cruzada_rotacion r2
      ON r2.anillo_id = v_anillo_a AND r2.orden = (r1.orden + 1) % 6
    WHERE r1.anillo_id = v_anillo_a;
END $$;

-- Verificación: debe mostrar 6 filas.
SELECT pi.project_description AS inspector, pd.project_description AS inspeccionado
FROM ss_inspeccion_cruzada_programacion prog
JOIN ss_inspeccion_cruzada_anillo a ON a.id = prog.anillo_id
JOIN project pi ON pi.project_id = prog.proyecto_inspector_id
JOIN project pd ON pd.project_id = prog.proyecto_inspeccionado_id
WHERE prog.anio = 2026 AND prog.mes = 9 AND a.nombre = 'Anillo A'
ORDER BY pi.project_description;

COMMIT;
