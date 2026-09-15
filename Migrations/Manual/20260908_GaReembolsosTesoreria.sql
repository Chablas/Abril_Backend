-- ============================================================================
-- Gestión Administrativa — Reembolsos (Tesorería): revisión documental previa
-- al pago, trazabilidad de quién la confirmó y los dos correos del cierre.
--
-- Qué cambia y por qué
--
-- 1) ga_estado_reembolso: estado nuevo "Proceder con el reembolso" (id 6).
--    Hasta ahora Tesorería pagaba directo lo que la jefatura firmaba. El
--    requerimiento funcional (RG-26 / RF-TES-06 / RF-TES-07) exige un paso
--    previo: Tesorería revisa la planilla, el Consolidado del S10, la firma de
--    la jefatura y los tramos con sus vouchers, y recién al CONFIRMAR esa
--    revisión el reembolso queda habilitado para pago. El flujo pasa a ser
--        Firmado (4) → Proceder con el reembolso (6) → Pagado (5)
--    y el pago desde Firmado deja de estar permitido.
--
--    El id 6 va al final de la tabla (no se renumera nada) y el ORDEN sí
--    refleja el flujo: por eso Pagado pasa de orden 5 a 6.
--
-- 2) ga_solicitud_salida.revision_tesoreria_por_id / _at: quién confirmó esa
--    revisión y cuándo (RF-TRZ-11). Van en la salida, como firmado_por/pagado_por,
--    porque el estado del reembolso también vive ahí.
--
-- 3) ga_correo_evento: los dos correos que faltaban en el ciclo.
--      • TESORERIA_REEMBOLSO (RF-TES-01) — a Tesorería, cuando la jefatura
--        firma. Se ORIGINA en Gestión de Rendiciones, que es donde se firma.
--        Su destinatario principal no es un área ni una lista escrita a mano:
--        lo resuelve el backend por PUESTO (categoría Tesorero) + rol TESORERO,
--        que son las dos condiciones que abren la bandeja de Reembolsos.
--      • REEMBOLSO_PAGADO (RG-28 / RF-TES-11) — al colaborador, al registrarse
--        el pago. Es el primer correo que se origina en Reembolsos: hasta ahora
--        esa pantalla estaba en el catálogo sin ningún correo propio.
--
-- ORDEN DE EJECUCIÓN
--   • Correr ANTES de desplegar el backend: el código nuevo lee
--     revision_tesoreria_at en cada listado de Reembolsos, así que desplegar
--     primero deja esa pantalla en 500. Al revés no hay problema: el backend
--     viejo no escribe ninguna de las columnas nuevas y nunca deja una salida
--     en el estado 6.
--   • Correr DESPUÉS de 20260908_GaCorreosPorPantallaYPlazoRendicion.sql, que
--     crea ga_correo_pantalla y el pantalla_id NOT NULL. La guarda de abajo
--     aborta con el nombre del script si falta.
--
-- QUÉ PASA CON LO QUE YA ESTABA EN CURSO
--   Las salidas que hoy están en Firmado (4) NO se tocan: quedan esperando la
--   confirmación de Tesorería, que es exactamente el paso nuevo. Las Pagadas
--   tampoco. No hay filas que queden sin camino.
--
-- Re-ejecutable: los IF NOT EXISTS / ON CONFLICT / WHERE NOT EXISTS dejan cada
-- sentencia sin efecto si ya se corrió. Correr TODO junto (no hay pasos "solo
-- después del deploy").
-- ============================================================================

BEGIN;

-- ── 0) Prerrequisito: el catálogo de pantallas ──────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables
                   WHERE table_name = 'ga_correo_pantalla')
    THEN
        RAISE EXCEPTION
            'Falta ga_correo_pantalla. Correr primero Migrations/Manual/20260908_GaCorreosPorPantallaYPlazoRendicion.sql.';
    END IF;
END $$;

-- ── 1) Estado nuevo del reembolso ───────────────────────────────────────────
INSERT INTO ga_estado_reembolso (id, descripcion, orden, activo)
VALUES (6, 'Proceder con el reembolso', 5, true)
ON CONFLICT (id) DO NOTHING;

-- El orden refleja el flujo, y Pagado pasó a ser el último.
UPDATE ga_estado_reembolso SET orden = 6 WHERE id = 5 AND orden <> 6;

-- ── 2) Trazabilidad de la revisión de Tesorería ─────────────────────────────
ALTER TABLE ga_solicitud_salida
    ADD COLUMN IF NOT EXISTS revision_tesoreria_por_id integer,
    ADD COLUMN IF NOT EXISTS revision_tesoreria_at     timestamptz;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE  conname = 'fk_ga_solicitud_salida_revision_tesoreria_por'
    ) THEN
        ALTER TABLE ga_solicitud_salida
            ADD CONSTRAINT fk_ga_solicitud_salida_revision_tesoreria_por
            FOREIGN KEY (revision_tesoreria_por_id) REFERENCES app_user (user_id);
    END IF;
END $$;

COMMENT ON COLUMN ga_solicitud_salida.revision_tesoreria_por_id IS
    'Tesorero que confirmo la revision documental (RG-26) y dejo el reembolso listo para pagar.';
COMMENT ON COLUMN ga_solicitud_salida.revision_tesoreria_at IS
    'Momento de esa confirmacion. Es el paso que habilita el pago: sin el, la salida sigue Firmada.';

-- ── 3) Los dos correos del cierre ───────────────────────────────────────────
INSERT INTO ga_correo_evento (
    codigo, nombre, descripcion, orden,
    destinatario_principal_nombre, destinatario_principal_activo,
    permite_desactivar_envio, permite_desactivar_principal, pantalla_id)
SELECT
    'TESORERIA_REEMBOLSO',
    'Reembolso firmado · a Tesorería',
    'Avisa a Tesoreria que una planilla quedo firmada por la jefatura y su reembolso entro a la bandeja de pago. El destinatario principal se resuelve por puesto (categoria Tesorero) y rol TESORERO.',
    12,
    'Tesorería (puesto de categoría Tesorero)', true, true, true,
    p.id
FROM   ga_correo_pantalla p
WHERE  p.codigo = 'GESTION_RENDICIONES' AND p.state
  AND  NOT EXISTS (SELECT 1 FROM ga_correo_evento e
                   WHERE lower(e.codigo) = 'tesoreria_reembolso' AND e.state);

INSERT INTO ga_correo_evento (
    codigo, nombre, descripcion, orden,
    destinatario_principal_nombre, destinatario_principal_activo,
    permite_desactivar_envio, permite_desactivar_principal, pantalla_id)
SELECT
    'REEMBOLSO_PAGADO',
    'Reembolso realizado · al solicitante',
    'Avisa al colaborador que Tesoreria ya pago su reembolso. Sale una vez por planilla y trabajador, no por salida.',
    13,
    'El solicitante', true, true, true,
    p.id
FROM   ga_correo_pantalla p
WHERE  p.codigo = 'REEMBOLSOS' AND p.state
  AND  NOT EXISTS (SELECT 1 FROM ga_correo_evento e
                   WHERE lower(e.codigo) = 'reembolso_pagado' AND e.state);

COMMIT;

-- ============================================================================
-- Verificación
-- ============================================================================
-- SELECT * FROM ga_estado_reembolso ORDER BY orden;
--
-- SELECT column_name, data_type, is_nullable
-- FROM   information_schema.columns
-- WHERE  table_name = 'ga_solicitud_salida'
--   AND  column_name LIKE 'revision_tesoreria%';
--
-- SELECT p.codigo AS pantalla, e.orden, e.codigo, e.nombre
-- FROM   ga_correo_evento e
-- JOIN   ga_correo_pantalla p ON p.id = e.pantalla_id
-- WHERE  e.state
-- ORDER  BY p.orden, e.orden;
--
-- Cuántas salidas quedan esperando la confirmación de Tesorería (estado 4):
-- SELECT count(*) FROM ga_solicitud_salida WHERE estado_reembolso_id = 4;
