-- ============================================================================
-- Gestión Administrativa — Rendir envía a primera revisión, y aprobarla le
-- avisa al consolidador.
--
-- Qué cambia y por qué
--
-- 1) «Rendir» en Solicitud de Salidas ya no descarga el PDF: genera la planilla
--    y la envía en el acto a la primera revisión de la jefatura, con los MISMOS
--    dos correos de «Enviar a revisión» de Mis Rendiciones (REN_PRIMERA_REVISION
--    al jefe y REN_ENVIADA al trabajador). Siguen administrándose en Mis
--    Rendiciones → Configuración → Correos, que es donde se reenvía una planilla
--    subsanada: acá solo se actualiza su descripción para que diga que también
--    los dispara Rendir. No cambia ningún destinatario.
--
-- 2) Correo nuevo REN_PRIMERA_APROBADA_CONSOLIDADOR: cuando la jefatura aprueba
--    la primera revisión en Gestión de Rendiciones, además del aviso al
--    trabajador sale uno a los consolidadores del área (Consolidados →
--    Configuración → Consolidadores) de que la rendición se suma a las
--    disponibles para el Consolidado del S10. Es informativo: el consolidador
--    junta varias y las consolida cuando le toca. Va en la pantalla
--    GESTION_RENDICIONES porque ahí se ORIGINA, que es la regla del catálogo.
--
-- Este script NO toca el esquema: solo pone el catálogo de correos al día.
--
-- ORDEN DE EJECUCIÓN
--   • Da igual antes o después de desplegar. Sin la fila, el backend manda el
--     correo nuevo igual a los consolidadores (el resolver trata un código que no
--     está en ga_correo_evento como "activo y sin copias"); lo que falta es poder
--     apagarlo o sumarle copias desde Configuración. Correrlo antes deja la
--     sección lista cuando se despliegue.
--
-- Re-ejecutable: el INSERT no duplica (WHERE NOT EXISTS) y los UPDATE dejan los
-- mismos valores si se corre dos veces.
-- ============================================================================

-- El archivo está en UTF-8 y trae texto acentuado. En Windows psql arranca con
-- el client_encoding del locale (WIN1252) y esos bytes se rechazan, así que se
-- fija acá: el script queda correcto sin depender de cómo se invoque.
SET client_encoding TO 'UTF8';

BEGIN;

-- ── 0) Prerrequisitos ───────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM ga_correo_pantalla WHERE codigo = 'GESTION_RENDICIONES' AND state)
    THEN
        RAISE EXCEPTION
            'Falta la pantalla GESTION_RENDICIONES en ga_correo_pantalla. Correr primero Migrations/Manual/20260908_GaCorreosPorPantallaYPlazoRendicion.sql.';
    END IF;

    -- Dos IF y no uno con OR: el segundo SELECT se planifica recién al llegar a él, así que
    -- sin la tabla aborta con el mensaje de acá y no con "no existe la relación".
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ga_correo_grupo')
    THEN
        RAISE EXCEPTION
            'Falta ga_correo_grupo. Correr primero Migrations_Manual/2026-09-10_ga_recordatorios_rendicion.sql.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM ga_correo_grupo WHERE codigo = 'CORREOS' AND state)
    THEN
        RAISE EXCEPTION
            'Falta el grupo CORREOS en ga_correo_grupo. Correr primero Migrations_Manual/2026-09-10_ga_recordatorios_rendicion.sql.';
    END IF;
END $$;

-- ── 1) Los dos correos del envío a primera revisión también salen al rendir ──
UPDATE ga_correo_evento
SET descripcion = 'Le avisa al jefe/revisor que el trabajador envió una rendición a primera revisión: al rendir en Solicitud de Salidas o al reenviarla desde Mis Rendiciones. Lleva los dos botones para entrar a aprobarla u observarla.',
    updated_at = now()
WHERE codigo = 'REN_PRIMERA_REVISION' AND state;

UPDATE ga_correo_evento
SET descripcion = 'Confirma al trabajador que su rendición quedó registrada y a quién se le envió para la primera revisión. Sale al rendir en Solicitud de Salidas y al reenviarla desde Mis Rendiciones.',
    updated_at = now()
WHERE codigo = 'REN_ENVIADA' AND state;

-- ── 2) El aviso de la aprobación a los consolidadores ──────────────────────
-- Va al final del orden: en Gestión de Rendiciones queda después de los dos
-- correos al solicitante (aprobada / observada).
INSERT INTO ga_correo_evento (
    codigo, nombre, descripcion, orden,
    destinatario_principal_nombre, destinatario_principal_activo,
    permite_desactivar_envio, permite_desactivar_principal,
    pantalla_id, grupo_id)
SELECT
    'REN_PRIMERA_APROBADA_CONSOLIDADOR',
    'Primera revisión aprobada · a los consolidadores',
    'Les avisa a los consolidadores del área que una rendición aprobada en primera revisión se suma a las disponibles para el Consolidado del S10. Es informativo; si se aprueban varias a la vez, sale un solo correo con todas.',
    (SELECT COALESCE(MAX(orden), 0) + 1 FROM ga_correo_evento WHERE state),
    'Los consolidadores del área del trabajador', true,
    true, true,
    p.id, g.id
FROM   ga_correo_pantalla p
CROSS  JOIN ga_correo_grupo g
WHERE  p.codigo = 'GESTION_RENDICIONES' AND p.state
  AND  g.codigo = 'CORREOS' AND g.state
  AND  NOT EXISTS (SELECT 1 FROM ga_correo_evento e
                   WHERE lower(e.codigo) = 'ren_primera_aprobada_consolidador' AND e.state);

COMMIT;

-- ── Verificación (solo lectura) ─────────────────────────────────────────────
SELECT p.codigo AS pantalla, e.orden, e.codigo, e.nombre, e.active, e.destinatario_principal_nombre
FROM ga_correo_evento e
JOIN ga_correo_pantalla p ON p.id = e.pantalla_id
WHERE e.state
  AND p.codigo IN ('RENDICIONES', 'GESTION_RENDICIONES')
ORDER BY p.codigo, e.orden, e.id;
