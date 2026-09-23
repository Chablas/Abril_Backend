-- ============================================================================
-- Gestión Administrativa — Consolidados: correo «La observación fue subsanada y
-- el consolidado volvió a Tesorería» (plantilla 20 del área usuaria)
--
-- Qué cambia y por qué
--
--   Cuando Tesorería observa un consolidado, vuelve al consolidador; él lo
--   recarga y la jefatura lo firma de nuevo. Hasta ahora esa firma le mandaba a
--   Tesorería el mismo aviso que un consolidado nuevo («Consolidado pendiente de
--   revisión»). Desde este cambio, si el consolidado volvía de una observación de
--   Tesorería, sale en su lugar TESORERIA_SUBSANADA, con lo que ella había
--   observado.
--
--   Es un correo aparte, con su propia fila en Consolidados → Configuración →
--   Correos. Nace como gemelo de TESORERIA_REEMBOLSO: mismo destinatario
--   principal (el rol TESORERO), mismos interruptores y las mismas copias que hoy
--   estén prendidas; queda justo debajo de él en la pantalla.
--
-- Este script NO toca el esquema: solo el catálogo de correos.
--
-- ORDEN DE EJECUCIÓN
--   • Da igual antes o después de desplegar. Sin la fila el backend manda el
--     correo igual, solo al rol TESORERO; lo que falta es verlo en Configuración
--     y las copias de su gemelo.
--
-- Re-ejecutable: si TESORERIA_SUBSANADA ya existe no hace nada.
-- ============================================================================

-- El archivo está en UTF-8 y trae texto acentuado. En Windows psql arranca con
-- el client_encoding del locale (WIN1252) y esos bytes se rechazan, así que se
-- fija acá: el script queda correcto sin depender de cómo se invoque.
SET client_encoding TO 'UTF8';

BEGIN;

DO $$
DECLARE
    gemelo   ga_correo_evento%ROWTYPE;
    nuevo_id integer;
BEGIN
    IF EXISTS (SELECT 1 FROM ga_correo_evento
               WHERE lower(codigo) = 'tesoreria_subsanada' AND state)
    THEN
        RAISE NOTICE 'TESORERIA_SUBSANADA ya existe: no se toca nada.';
        RETURN;
    END IF;

    SELECT * INTO gemelo
    FROM ga_correo_evento
    WHERE codigo = 'TESORERIA_REEMBOLSO' AND state;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Falta el correo TESORERIA_REEMBOLSO en ga_correo_evento.';
    END IF;

    -- ── 1) Lugar justo debajo de su gemelo, en la misma pantalla y sección ───
    UPDATE ga_correo_evento
    SET orden      = orden + 1,
        updated_at = now()
    WHERE state
      AND pantalla_id = gemelo.pantalla_id
      AND grupo_id    = gemelo.grupo_id
      AND orden       > gemelo.orden;

    -- ── 2) El correo, con los interruptores de su gemelo ─────────────────────
    INSERT INTO ga_correo_evento (
        codigo, nombre, descripcion, orden, active,
        destinatario_principal_nombre, destinatario_principal_activo,
        permite_desactivar_envio, permite_desactivar_principal,
        pantalla_id, grupo_id)
    VALUES (
        'TESORERIA_SUBSANADA',
        'Observación subsanada · a Tesorería',
        'Avisa a Tesorería que un consolidado que ella había observado volvió firmado por la jefatura, con la observación subsanada. Sale en lugar de «Consolidado firmado · a Tesorería». El destinatario principal son los que tienen el rol TESORERO; los demás se agregan acá como destinatarios (por rol, área, trabajador o correo).',
        gemelo.orden + 1,
        gemelo.active,
        gemelo.destinatario_principal_nombre,
        gemelo.destinatario_principal_activo,
        gemelo.permite_desactivar_envio,
        gemelo.permite_desactivar_principal,
        gemelo.pantalla_id,
        gemelo.grupo_id)
    RETURNING id INTO nuevo_id;

    -- ── 3) Las copias que hoy tiene prendidas su gemelo ──────────────────────
    INSERT INTO ga_correo_regla (
        evento_id, tipo_id, worker_id, area_scope_id, correo, role_id,
        incluir_descendientes, orden, active, state)
    SELECT
        nuevo_id, r.tipo_id, r.worker_id, r.area_scope_id, r.correo, r.role_id,
        r.incluir_descendientes, r.orden, true, true
    FROM ga_correo_regla r
    WHERE r.evento_id = gemelo.id
      AND r.state
      AND r.active;
END $$;

COMMIT;

-- ── Verificación (solo lectura) ─────────────────────────────────────────────
SELECT p.codigo AS pantalla, e.orden, e.codigo, e.nombre, e.active
FROM ga_correo_evento e
JOIN ga_correo_pantalla p ON p.id = e.pantalla_id
WHERE e.state
  AND p.codigo = 'CONSOLIDADOS'
ORDER BY e.orden, e.id;

SELECT e.codigo, r.tipo_id, r.worker_id, r.area_scope_id, r.correo, r.role_id, r.active
FROM ga_correo_regla r
JOIN ga_correo_evento e ON e.id = r.evento_id
WHERE r.state
  AND e.codigo IN ('TESORERIA_REEMBOLSO', 'TESORERIA_SUBSANADA')
ORDER BY e.codigo, r.orden, r.id;
