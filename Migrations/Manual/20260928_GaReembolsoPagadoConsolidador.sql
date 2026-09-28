-- ============================================================================
-- Gestión Administrativa — Reembolsos: correo «El consolidado fue pagado» al
-- consolidador (plantilla 22 del área usuaria)
--
-- Qué cambia y por qué
--
--   Al marcar como pagado un consolidado solo se les avisaba a los
--   colaboradores. Pero Tesorería le abona el consolidado al CONSOLIDADOR y es
--   él quien después le reembolsa a cada trabajador. Desde este cambio, al
--   pagar sale también REEMBOLSO_PAGADO_CONSOLIDADOR, uno por consolidado y
--   solo a quien lo adjuntó, con el monto que Tesorería le abonó, lo que le
--   toca reembolsar a cada trabajador (con sus rendiciones), el código del
--   consolidado y el de la planilla grupal.
--
--   Es un correo nuevo, con su propia fila en Reembolsos → Configuración →
--   Correos, justo antes de «Reembolso realizado» (el orden de la plata: primero
--   el consolidador, después cada colaborador). Nace prendido, sin copias y,
--   como «Reembolso observado por Tesorería», sin poder apagarse ni quitarle el
--   consolidador desde la pantalla.
--
-- Este script NO toca el esquema: solo el catálogo de correos.
--
-- ORDEN DE EJECUCIÓN
--   • Da igual antes o después de desplegar. Sin la fila el backend manda el
--     correo igual, solo al consolidador; lo que falta es verlo en
--     Configuración.
--
-- Re-ejecutable: si REEMBOLSO_PAGADO_CONSOLIDADOR ya existe no hace nada.
-- Aplicar en dev, demo y prod.
-- ============================================================================

-- El archivo está en UTF-8 y trae texto acentuado. En Windows psql arranca con
-- el client_encoding del locale (WIN1252) y esos bytes se rechazan, así que se
-- fija acá: el script queda correcto sin depender de cómo se invoque.
SET client_encoding TO 'UTF8';

BEGIN;

DO $$
DECLARE
    v_pantalla integer;
    v_grupo    integer;
    v_orden    integer;
BEGIN
    IF EXISTS (SELECT 1 FROM ga_correo_evento
               WHERE lower(codigo) = 'reembolso_pagado_consolidador' AND state)
    THEN
        RAISE NOTICE 'REEMBOLSO_PAGADO_CONSOLIDADOR ya existe: no se toca nada.';
        RETURN;
    END IF;

    SELECT id INTO v_pantalla FROM ga_correo_pantalla WHERE codigo = 'REEMBOLSOS';
    IF v_pantalla IS NULL THEN
        RAISE EXCEPTION 'Falta la pantalla REEMBOLSOS en ga_correo_pantalla.';
    END IF;

    SELECT id INTO v_grupo FROM ga_correo_grupo WHERE codigo = 'CORREOS';
    IF v_grupo IS NULL THEN
        RAISE EXCEPTION 'Falta el grupo CORREOS en ga_correo_grupo.';
    END IF;

    -- ── 1) Lugar: justo antes de «Reembolso realizado» ───────────────────────
    -- Si REEMBOLSO_PAGADO no estuviera en esta pantalla y sección, va al final.
    SELECT orden INTO v_orden
    FROM ga_correo_evento
    WHERE codigo = 'REEMBOLSO_PAGADO' AND state
      AND pantalla_id = v_pantalla AND grupo_id = v_grupo;

    IF v_orden IS NOT NULL THEN
        UPDATE ga_correo_evento
        SET orden      = orden + 1,
            updated_at = now()
        WHERE state
          AND pantalla_id = v_pantalla
          AND grupo_id    = v_grupo
          AND orden      >= v_orden;
    ELSE
        SELECT coalesce(max(orden), 0) + 1 INTO v_orden
        FROM ga_correo_evento
        WHERE state AND pantalla_id = v_pantalla AND grupo_id = v_grupo;
    END IF;

    -- ── 2) El correo ─────────────────────────────────────────────────────────
    -- Mismos interruptores que «Reembolso observado por Tesorería», el otro
    -- correo de esta pantalla al consolidador: no se apaga y su destinatario
    -- principal tampoco.
    INSERT INTO ga_correo_evento (
        codigo, nombre, descripcion, orden, active,
        destinatario_principal_nombre, destinatario_principal_activo,
        permite_desactivar_envio, permite_desactivar_principal,
        pantalla_id, grupo_id)
    VALUES (
        'REEMBOLSO_PAGADO_CONSOLIDADOR',
        'Consolidado pagado · al consolidador',
        'Avisa al consolidador que Tesorería le pagó el consolidado, con el monto total y lo que le toca reembolsar a cada trabajador. Sale una vez por consolidado.',
        v_orden,
        true,
        'El consolidador que adjuntó el consolidado',
        true,
        false,
        false,
        v_pantalla,
        v_grupo);
END $$;

COMMIT;

-- ── Verificación (solo lectura) ─────────────────────────────────────────────
SELECT p.codigo AS pantalla, e.orden, e.codigo, e.nombre, e.active,
       e.destinatario_principal_nombre, e.destinatario_principal_activo,
       e.permite_desactivar_envio, e.permite_desactivar_principal
FROM ga_correo_evento e
JOIN ga_correo_pantalla p ON p.id = e.pantalla_id
WHERE e.state
  AND p.codigo = 'REEMBOLSOS'
ORDER BY e.orden, e.id;
