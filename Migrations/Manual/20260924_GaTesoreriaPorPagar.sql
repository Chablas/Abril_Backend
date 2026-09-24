-- ============================================================================
-- Gestión Administrativa — Reembolsos: correo «El consolidado está listo para
-- programación de pago» (plantilla 21 del área usuaria)
--
-- Qué cambia y por qué
--
--   Confirmar la revisión de un consolidado en Reembolsos no le avisaba a nadie.
--   Desde este cambio, al confirmarla sale TESORERIA_POR_PAGAR a Tesorería —los
--   que tienen el rol TESORERO, por rol y no por la categoría del puesto—, uno
--   por consolidado, con el resumen de lo que quedó listo para pagar.
--
--   Es un correo nuevo, con su propia fila en Reembolsos → Configuración →
--   Correos, justo antes de «Reembolso realizado» (el orden del flujo: primero se
--   confirma, después se paga). Nace prendido y sin copias: los demás
--   destinatarios se agregan desde esa pantalla.
--
-- Este script NO toca el esquema: solo el catálogo de correos.
--
-- ORDEN DE EJECUCIÓN
--   • Da igual antes o después de desplegar. Sin la fila el backend manda el
--     correo igual, solo al rol TESORERO; lo que falta es verlo (y poder
--     apagarlo) en Configuración.
--
-- Re-ejecutable: si TESORERIA_POR_PAGAR ya existe no hace nada.
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
               WHERE lower(codigo) = 'tesoreria_por_pagar' AND state)
    THEN
        RAISE NOTICE 'TESORERIA_POR_PAGAR ya existe: no se toca nada.';
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
    -- Mismos interruptores que «Consolidado firmado · a Tesorería»: se puede
    -- apagar entero y se puede apagar el destinatario principal.
    INSERT INTO ga_correo_evento (
        codigo, nombre, descripcion, orden, active,
        destinatario_principal_nombre, destinatario_principal_activo,
        permite_desactivar_envio, permite_desactivar_principal,
        pantalla_id, grupo_id)
    VALUES (
        'TESORERIA_POR_PAGAR',
        'Consolidado listo para pago · a Tesorería',
        'Avisa a Tesorería que se confirmó la revisión de un consolidado y quedó listo para programar el pago. Sale una vez por consolidado. El destinatario principal son los que tienen el rol TESORERO; los demás se agregan acá como destinatarios (por rol, área, trabajador o correo).',
        v_orden,
        true,
        'Tesorería (rol TESORERO)',
        true,
        true,
        true,
        v_pantalla,
        v_grupo);
END $$;

COMMIT;

-- ── Verificación (solo lectura) ─────────────────────────────────────────────
SELECT p.codigo AS pantalla, e.orden, e.codigo, e.nombre, e.active,
       e.destinatario_principal_nombre, e.destinatario_principal_activo
FROM ga_correo_evento e
JOIN ga_correo_pantalla p ON p.id = e.pantalla_id
WHERE e.state
  AND p.codigo = 'REEMBOLSOS'
ORDER BY e.orden, e.id;
