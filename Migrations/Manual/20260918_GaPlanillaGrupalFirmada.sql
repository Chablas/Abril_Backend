-- ============================================================================
-- Gestión Administrativa · Salidas — Copia firmada de la planilla grupal
-- Fecha: 2026-09-18
--
-- Al aprobar el reembolso (que ES firmar) la jefatura ahora firma también la
-- planilla grupal del consolidado, además de la planilla individual y del
-- Consolidado del S10. La copia firmada se guarda al lado de la original, igual
-- que pdf_firmado_* del consolidado.
--
-- Nullable: los consolidados aprobados antes de este cambio no tienen la grupal
-- firmada, y los que todavía no se aprueban tampoco.
--
-- Correr ANTES de desplegar el backend: EF lee ga_consolidado_s10 entera en todo
-- el módulo, y sin las columnas cae con 42703 en Gestión de Rendiciones,
-- Consolidados, Correcciones S10 y Reembolsos.
--
-- Idempotente: se puede correr varias veces. Aplicar en dev y prod.
-- ============================================================================

BEGIN;

ALTER TABLE ga_consolidado_s10
    ADD COLUMN IF NOT EXISTS planilla_grupal_firmado_url      text,
    ADD COLUMN IF NOT EXISTS planilla_grupal_firmado_item_id  text,
    ADD COLUMN IF NOT EXISTS planilla_grupal_firmado_filename text;

COMMENT ON COLUMN ga_consolidado_s10.planilla_grupal_firmado_url IS
    'Copia de la planilla grupal con la firma de la jefatura (se firma al aprobar el reembolso). Null hasta entonces.';

COMMIT;
