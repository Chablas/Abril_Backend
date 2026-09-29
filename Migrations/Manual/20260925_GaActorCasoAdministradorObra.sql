-- ============================================================================
-- Gestión Administrativa — Revisores de Áreas: tipo de trabajador
-- «Administrador de obra».
-- ANTES DE DESPLEGAR EL BACKEND
-- Fecha: 2026-09-25
--
-- Un sexto caso en ga_actor_caso (id 6, ActorCasoIds.AdministradorObra): quien
-- administra alguna obra activa (project.workers_coord_admin_id) y trabaja en obra.
-- Como el staff, salvo que su 1.ª revisión la aprueba el residente, de sus salidas
-- no se avisa a ningún jefe (salvo que se personalice) y su consolidado lo firma él
-- mismo junto con el residente.
--
-- Hace falta donde 20260925_GaActoresUnificados.sql ya se corrió antes de este
-- cambio (dev y demo). En prod ese script ya trae la fila; correr este igual no
-- hace nada. Re-ejecutable.
-- ============================================================================

SET client_encoding TO 'UTF8';

BEGIN;

INSERT INTO ga_actor_caso (ga_actor_caso_id, codigo, nombre, display_order)
VALUES (6, 'ADMINISTRADOR_OBRA', 'Administrador de obra', 3)
ON CONFLICT (ga_actor_caso_id) DO NOTHING;

-- Se muestra detrás de Staff.
UPDATE ga_actor_caso c SET display_order = v.orden
FROM (VALUES (1, 1), (2, 2), (6, 3), (3, 4), (4, 5), (5, 6)) AS v(id, orden)
WHERE c.ga_actor_caso_id = v.id AND c.display_order <> v.orden;

COMMIT;

-- ── Verificación (solo lectura) ─────────────────────────────────────────────
SELECT ga_actor_caso_id, codigo, nombre, display_order
FROM ga_actor_caso
WHERE state
ORDER BY display_order;
