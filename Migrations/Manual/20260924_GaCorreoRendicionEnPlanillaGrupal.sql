-- ============================================================================
-- Gestión Administrativa — Correo «Rendición incluida en una planilla grupal»
-- Fecha: 2026-09-24
--
-- Al preparar la planilla grupal en Gestión de Rendiciones, a cada trabajador
-- de las planillas que cubre le llega «Rendición incluida en una planilla
-- grupal», con el botón para ver su rendición. Es el mismo molde que
-- REN_INCLUIDA_CONSOLIDADO («Tu rendición fue incluida en un consolidado»), un
-- paso antes: sale uno por rendición y trabajador, y como una planilla grupal no
-- se vuelve a preparar, no se repite. Va en la pantalla GESTION_RENDICIONES
-- porque ahí se ORIGINA, justo antes del aviso del consolidado.
--
-- Este script NO toca el esquema: solo agrega la fila al catálogo de correos.
--
-- ORDEN DE EJECUCIÓN
--   • Da igual antes o después de desplegar. Sin la fila el backend manda el
--     correo igual (un código que no está en ga_correo_evento se trata como
--     "activo y sin copias"); lo que falta es poder apagarlo o sumarle copias
--     desde Gestión de Rendiciones → Configuración → Correos.
--
-- Re-ejecutable: si la fila ya existe no hace nada. Aplicar en dev, demo y prod.
-- ============================================================================

-- El archivo está en UTF-8 y trae texto acentuado. En Windows psql arranca con
-- el client_encoding del locale (WIN1252): se fija acá para no depender de eso.
SET client_encoding TO 'UTF8';

BEGIN;

DO $$
DECLARE
    v_pantalla integer;
    v_grupo    integer;
    v_orden    integer;
BEGIN
    SELECT id INTO v_pantalla FROM ga_correo_pantalla WHERE codigo = 'GESTION_RENDICIONES' AND state;
    IF v_pantalla IS NULL THEN
        RAISE EXCEPTION 'Falta la pantalla GESTION_RENDICIONES en ga_correo_pantalla.';
    END IF;

    SELECT id INTO v_grupo FROM ga_correo_grupo WHERE codigo = 'CORREOS' AND state;
    IF v_grupo IS NULL THEN
        RAISE EXCEPTION 'Falta el grupo CORREOS en ga_correo_grupo.';
    END IF;

    IF EXISTS (SELECT 1 FROM ga_correo_evento
               WHERE lower(codigo) = 'ren_incluida_planilla_grupal' AND state) THEN
        RETURN;
    END IF;

    -- Justo antes del aviso del consolidado: en el flujo, la planilla grupal va
    -- primero. Se corre un lugar todo lo que viene después (el orden relativo de
    -- los demás no cambia).
    SELECT orden INTO v_orden FROM ga_correo_evento
    WHERE codigo = 'REN_INCLUIDA_CONSOLIDADO' AND state;

    IF v_orden IS NULL THEN
        SELECT COALESCE(MAX(orden), 0) + 1 INTO v_orden FROM ga_correo_evento WHERE state;
    ELSE
        UPDATE ga_correo_evento
        SET    orden = orden + 1, updated_at = now()
        WHERE  state AND orden >= v_orden;
    END IF;

    INSERT INTO ga_correo_evento (
        codigo, nombre, descripcion, orden,
        destinatario_principal_nombre, destinatario_principal_activo,
        permite_desactivar_envio, permite_desactivar_principal,
        pantalla_id, grupo_id)
    VALUES (
        'REN_INCLUIDA_PLANILLA_GRUPAL',
        'Rendición incluida en una planilla grupal · al solicitante',
        'Le avisa a cada trabajador que su rendición quedó incluida en la planilla grupal que preparó el consolidador, con el botón para verla. Es informativo: sale uno por rendición y trabajador.',
        v_orden,
        'El solicitante', true,
        true, true,
        v_pantalla, v_grupo);
END $$;

COMMIT;

-- ── Verificación (solo lectura) ─────────────────────────────────────────────
SELECT p.codigo AS pantalla, e.orden, e.codigo, e.nombre, e.active
FROM ga_correo_evento e
JOIN ga_correo_pantalla p ON p.id = e.pantalla_id
WHERE e.state AND p.codigo = 'GESTION_RENDICIONES'
ORDER BY e.orden, e.id;
