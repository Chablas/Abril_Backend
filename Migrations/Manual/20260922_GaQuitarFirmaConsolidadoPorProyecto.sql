-- ============================================================================
-- Gestión Administrativa — Se quita «Firma por obra».
--
-- Qué cambia y por qué
--
-- La columna era un interruptor por área: con ella en true el consolidado y la
-- planilla grupal los firmaba el revisor de la OBRA en vez del revisor del
-- ÁREA. Nació el 2026-09-21, el mismo día que entró el administrador de obra al
-- ciclo de la rendición, y quedó contradiciendo a lo que se pidió:
--
--   • con el checkbox APAGADO —como estaba en Costos y Presupuestos— el
--     administrador de obra no intervenía en NADA: ni la 1.ª revisión ni la
--     firma miraban la obra, y las dos caían en el Jefe del área;
--   • pero el consolidador sí seguía a la obra, así que el documento lo subía
--     el administrador y lo firmaba el Jefe;
--   • y la sección «Revisores de Áreas» de Rendiciones, que no muestra este
--     checkbox, anunciaba por proyecto al administrador y al residente.
--
-- Desde ahora manda «Filtrar por proyecto» y nada más: en un área filtrada, si
-- la planilla es de UNA sola obra, la revisa y la firma su gente —primero el
-- administrador de obra y después el residente—. Con obras mezcladas no hay una
-- sola a quién dársela y sigue respondiendo el área, que es la regla de siempre
-- y no depende de esta columna.
--
-- ORDEN DE EJECUCIÓN
--   • DESPUÉS de desplegar. El backend que está corriendo hoy todavía mapea la
--     columna: dropearla antes lo deja respondiendo 42703 en Consolidados.
--   • No corre prisa: la columna es NOT NULL DEFAULT false, así que el backend
--     nuevo —que ya no la conoce— inserta sin problema mientras siga existiendo.
--
-- Si en este entorno nunca se corrió 20260921_GaFirmaConsolidadoPorProyecto.sql
-- la columna no existe y este script no hace nada: es el mismo estado final.
--
-- Re-ejecutable: IF EXISTS.
-- ============================================================================

SET client_encoding TO 'UTF8';

BEGIN;

ALTER TABLE ga_salidas_area_config
    DROP COLUMN IF EXISTS firma_consolidado_por_proyecto;

COMMIT;

-- ── Verificación (solo lectura) ─────────────────────────────────────────────
-- Las áreas que se subdividen por proyecto: en ellas la planilla y el
-- consolidado son de la obra, sin ningún otro interruptor de por medio.
SELECT ai.area_item_name AS area,
       c.filtra_por_proyecto
FROM   ga_salidas_area_config c
JOIN   area_scope s  ON s.area_scope_id = c.area_scope_id
JOIN   area_item  ai ON ai.area_item_id = s.area_item_id
WHERE  c.state
ORDER  BY ai.area_item_name;
