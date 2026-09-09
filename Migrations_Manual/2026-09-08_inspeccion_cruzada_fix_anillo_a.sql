-- Corrección del Anillo A: KAURI ya no existe como proyecto activo, y "MAXIMO" en project es
-- en realidad "MÁXIMO ABRIL" (id 11). El anillo queda en 5 proyectos:
--   CEDRO 33 → GRAN MANZANO → BOSQUE REAL → SAUCE ZEN (id 9) → MÁXIMO ABRIL (id 11) → CEDRO 33
-- Ejecutar DESPUÉS de 2026-09-08_inspeccion_cruzada_programacion.sql, en pgAdmin.

BEGIN;

DO $$
DECLARE
    v_anillo_a integer;
BEGIN
    SELECT id INTO v_anillo_a FROM ss_inspeccion_cruzada_anillo WHERE nombre = 'Anillo A';

    -- Se agregan al final de la cola actual (orden 3 y 4 — CEDRO33/GRANMANZANO/BOSQUEREAL ya
    -- ocupan 0/1/2).
    INSERT INTO ss_inspeccion_cruzada_rotacion (anillo_id, proyecto_id, orden)
    VALUES (v_anillo_a, 9, 3), (v_anillo_a, 11, 4)
    ON CONFLICT (proyecto_id) DO NOTHING;

    -- Las 3 parejas de setiembre que faltaban por el hueco de KAURI.
    INSERT INTO ss_inspeccion_cruzada_programacion (anio, mes, anillo_id, proyecto_inspector_id, proyecto_inspeccionado_id)
    SELECT 2026, 9, v_anillo_a, r1.proyecto_id, r2.proyecto_id
    FROM ss_inspeccion_cruzada_rotacion r1
    JOIN ss_inspeccion_cruzada_rotacion r2
      ON r2.anillo_id = v_anillo_a AND r2.orden = (r1.orden + 1) % 5
    WHERE r1.anillo_id = v_anillo_a
      AND NOT EXISTS (
          SELECT 1 FROM ss_inspeccion_cruzada_programacion p
          WHERE p.anio = 2026 AND p.mes = 9 AND p.anillo_id = v_anillo_a
            AND p.proyecto_inspector_id = r1.proyecto_id AND p.proyecto_inspeccionado_id = r2.proyecto_id
      );
END $$;

-- Verificación: debe mostrar 5 filas para Anillo A (CEDRO33→GRANMANZANO→BOSQUEREAL→SAUCEZEN→MÁXIMOABRIL→CEDRO33).
SELECT a.nombre AS anillo, pi.project_description AS inspector, pd.project_description AS inspeccionado
FROM ss_inspeccion_cruzada_programacion prog
JOIN ss_inspeccion_cruzada_anillo a ON a.id = prog.anillo_id
JOIN project pi ON pi.project_id = prog.proyecto_inspector_id
JOIN project pd ON pd.project_id = prog.proyecto_inspeccionado_id
WHERE prog.anio = 2026 AND prog.mes = 9 AND a.nombre = 'Anillo A'
ORDER BY pi.project_description;

COMMIT;
