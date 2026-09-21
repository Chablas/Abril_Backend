-- ============================================================================
-- Gestión Administrativa — La firma del consolidado puede bajar a la obra.
--
-- Qué cambia y por qué
--
-- En un área marcada "filtrar por proyecto" conviven dos revisores: el de la
-- OBRA (el residente, o el que se haya asignado a ese proyecto en Revisores de
-- Áreas) y el del ÁREA entera. Hasta ahora el de la obra aprobaba las salidas y
-- el del área firmaba el consolidado y la planilla grupal, sin excepción.
--
-- Producción necesita poder invertir eso: que el mismo revisor de la obra que
-- aprueba las salidas firme también el consolidado. Esta columna es ese
-- interruptor, por área, y solo se ofrece en las áreas que ya filtran por
-- proyecto.
--
-- No crea ninguna lista nueva: cuando está en true, el firmante sale de los
-- revisores POR PROYECTO que ya existen en Revisores de Áreas. Si el documento
-- mezcla obras no hay un revisor de obra único y se sigue usando el del área.
--
-- ORDEN DE EJECUCIÓN
--   • ANTES de desplegar. El backend nuevo lee la columna en cada resolución de
--     firmante; sin ella responde 42703 y se cae Consolidados entero.
--   • El default es false, así que correrlo antes no cambia el comportamiento de
--     la versión que está corriendo hoy: nadie se entera hasta que se marque el
--     checkbox.
--
-- Re-ejecutable: IF NOT EXISTS.
-- ============================================================================

SET client_encoding TO 'UTF8';

BEGIN;

ALTER TABLE ga_salidas_area_config
    ADD COLUMN IF NOT EXISTS firma_consolidado_por_proyecto boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN ga_salidas_area_config.firma_consolidado_por_proyecto IS
    'Solo aplica con filtra_por_proyecto = true. Si es true, el consolidado y la planilla grupal '
    'los firma el revisor POR PROYECTO (el mismo que aprueba las salidas) en vez del revisor del '
    'área. Si el documento mezcla obras se usa igual el del área: no hay un revisor de obra único.';

COMMIT;

-- ── Verificación (solo lectura) ─────────────────────────────────────────────
SELECT ai.area_item_name                         AS area,
       c.filtra_por_proyecto,
       c.firma_consolidado_por_proyecto
FROM   ga_salidas_area_config c
JOIN   area_scope s  ON s.area_scope_id = c.area_scope_id
JOIN   area_item  ai ON ai.area_item_id = s.area_item_id
WHERE  c.state
ORDER  BY ai.area_item_name;
