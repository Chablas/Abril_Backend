-- ============================================================================
-- Gestión Administrativa — Revisores de Áreas unificado (los cinco actores).
-- PASO 2 de 2 · SOLO DESPUÉS DE DESPLEGAR EL BACKEND Y EL FRONTEND
-- Fecha: 2026-09-25
--
-- Bota lo que el código nuevo ya no lee:
--   • area_revisores, area_revisores_rendicion, area_consolidadores, workers_revisores
--     (lo vigente se copió en el PASO 1 a area_actor_asignacion / workers_actor_asignacion);
--   • ga_salidas_area_config.filtra_por_proyecto (la pantalla lo deduce y el algoritmo ya no
--     lo mira);
--   • las dos funcionalidades que reemplaza la pantalla nueva ('configuracion.revisores-areas'
--     y 'gestion-administrativa.config.consolidadores-areas'), con sus roles.
--
-- NO correrlo junto con el PASO 1: el backend desplegado antes de este cambio lee estas tablas,
-- y botarlas con él arriba tumba Solicitud de Salidas, Gestión de Salidas, Gestión de
-- Rendiciones, Consolidados y el login.
--
-- Guarda: si el PASO 1 no corrió (tablas nuevas vacías con filas vivas en las viejas), aborta sin
-- tocar nada.
-- ============================================================================

SET client_encoding TO 'UTF8';

BEGIN;

DO $$
DECLARE
    viejas integer := 0;
BEGIN
    IF to_regclass('public.area_actor_asignacion') IS NULL
       OR to_regclass('public.workers_actor_asignacion') IS NULL THEN
        RAISE EXCEPTION 'Faltan las tablas nuevas: corre primero 20260925_GaActoresUnificados.sql. Abortado.';
    END IF;

    IF to_regclass('public.workers_revisores') IS NOT NULL THEN
        SELECT count(*) INTO viejas FROM workers_revisores WHERE state;
        IF viejas > 0 AND NOT EXISTS (SELECT 1 FROM workers_actor_asignacion) THEN
            RAISE EXCEPTION 'workers_revisores tiene % filas vivas y workers_actor_asignacion esta vacia: '
                            'el PASO 1 no migro. Abortado.', viejas;
        END IF;
    END IF;
END $$;

DROP TABLE IF EXISTS area_revisores;
DROP TABLE IF EXISTS area_revisores_rendicion;
DROP TABLE IF EXISTS area_consolidadores;
DROP TABLE IF EXISTS workers_revisores;

ALTER TABLE ga_salidas_area_config DROP COLUMN IF EXISTS filtra_por_proyecto;

DELETE FROM role_feature
WHERE feature_id IN (SELECT feature_id FROM feature
                     WHERE feature_key IN ('configuracion.revisores-areas',
                                           'gestion-administrativa.config.consolidadores-areas'));

DELETE FROM feature
WHERE feature_key IN ('configuracion.revisores-areas',
                      'gestion-administrativa.config.consolidadores-areas');

COMMIT;

-- ── Verificación (solo lectura) ─────────────────────────────────────────────
SELECT to_regclass('public.area_revisores')           AS area_revisores,
       to_regclass('public.area_revisores_rendicion') AS area_revisores_rendicion,
       to_regclass('public.area_consolidadores')      AS area_consolidadores,
       to_regclass('public.workers_revisores')        AS workers_revisores;
