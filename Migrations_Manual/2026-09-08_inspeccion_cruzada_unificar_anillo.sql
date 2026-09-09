-- A pedido: los 8 proyectos deben rotar entre TODOS, no quedar en 2 grupos fijos (con solo 2
-- proyectos en el Anillo B, BUGAMBILIAS y 9 NOGALES estaban condenados a inspeccionarse siempre
-- entre ellos, sin variar nunca). Se unifica todo en un solo anillo de 8.
-- Setiembre queda tal como ya se generó (es historial); desde el próximo mes que se abra el
-- calendario, el sistema genera la rotación sobre los 8 proyectos juntos.
-- Ejecutar en pgAdmin DESPUÉS de los scripts anteriores.

BEGIN;

DO $$
DECLARE
    v_anillo_a integer;
    v_anillo_b integer;
BEGIN
    SELECT id INTO v_anillo_a FROM ss_inspeccion_cruzada_anillo WHERE nombre = 'Anillo A';
    SELECT id INTO v_anillo_b FROM ss_inspeccion_cruzada_anillo WHERE nombre = 'Anillo B';

    -- Mueve BUGAMBILIAS y 9 NOGALES al Anillo A, al final de la cola (orden 6 y 7 — el A ya
    -- tiene 0..5 ocupados).
    UPDATE ss_inspeccion_cruzada_rotacion
    SET anillo_id = v_anillo_a,
        orden = orden + 6
    WHERE anillo_id = v_anillo_b;

    -- Septiembre ya generado queda como historial, pero se reetiqueta al anillo unificado —
    -- si no, el FK de ss_inspeccion_cruzada_programacion.anillo_id impediría borrar el Anillo B.
    UPDATE ss_inspeccion_cruzada_programacion
    SET anillo_id = v_anillo_a
    WHERE anillo_id = v_anillo_b;

    -- Elimina el cursor y el anillo B, que ya no existen como grupo separado.
    DELETE FROM ss_inspeccion_cruzada_cursor WHERE anillo_id = v_anillo_b;
    DELETE FROM ss_inspeccion_cruzada_anillo WHERE id = v_anillo_b;

    -- Renombra el anillo restante para que ya no aluda a "A" (ahora es el único).
    UPDATE ss_inspeccion_cruzada_anillo SET nombre = 'Inspecciones Cruzadas' WHERE id = v_anillo_a;
END $$;

-- Verificación: debe mostrar 8 proyectos, orden 0 a 7, todos en el mismo anillo.
SELECT r.orden, p.project_description AS proyecto
FROM ss_inspeccion_cruzada_rotacion r
JOIN project p ON p.project_id = r.proyecto_id
ORDER BY r.orden;

COMMIT;
