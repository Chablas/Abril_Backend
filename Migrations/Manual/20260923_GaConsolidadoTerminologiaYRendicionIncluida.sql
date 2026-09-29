-- ============================================================================
-- Gestión Administrativa — Consolidados habla de «consolidado» y no de
-- «reembolso», y adjuntar el consolidado les avisa a los trabajadores.
--
-- Qué cambia y por qué
--
-- 1) En Consolidados (la pantalla y su Configuración → Correos) lo que la
--    jefatura aprueba u observa es el CONSOLIDADO del S10: «reembolso» queda
--    para Tesorería, que es quien lo paga desde Reembolsos. Acá solo se ponen al
--    día el nombre y la descripción de los cinco correos de la pantalla
--    CONSOLIDADOS. Los códigos no cambian: son la clave de la configuración de
--    destinatarios que ya está cargada. El «N.º de reembolso» se queda: es el
--    número que le pone el S10 al documento.
--
-- 2) Correo nuevo REN_INCLUIDA_CONSOLIDADO: al adjuntar el Consolidado del S10
--    en Gestión de Rendiciones, a cada trabajador de las planillas que cubre le
--    llega «Tu rendición fue incluida en un consolidado», con el botón para ver
--    su rendición (plantilla 11 del área usuaria). Sale uno por rendición y
--    trabajador; reemplazar el consolidado desde Consolidados no lo repite. Va
--    en la pantalla GESTION_RENDICIONES porque ahí se ORIGINA, que es la regla
--    del catálogo.
--
-- Este script NO toca el esquema: solo pone el catálogo de correos al día.
--
-- ORDEN DE EJECUCIÓN
--   • Da igual antes o después de desplegar. Los UPDATE son solo el texto que
--     muestra la Configuración. Sin la fila nueva el backend manda el correo
--     igual (el resolver trata un código que no está en ga_correo_evento como
--     "activo y sin copias"); lo que falta es poder apagarlo o sumarle copias.
--
-- Re-ejecutable: los UPDATE dejan los mismos valores y el INSERT no duplica
-- (WHERE NOT EXISTS).
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
        RAISE EXCEPTION 'Falta la pantalla GESTION_RENDICIONES en ga_correo_pantalla.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM ga_correo_grupo WHERE codigo = 'CORREOS' AND state)
    THEN
        RAISE EXCEPTION 'Falta el grupo CORREOS en ga_correo_grupo.';
    END IF;
END $$;

-- ── 1) Los correos de Consolidados dicen «consolidado» ──────────────────────
UPDATE ga_correo_evento
SET descripcion = 'Lo dispara el consolidador desde Consolidados («Avisar a la jefatura») cuando un Consolidado del S10 espera la aprobación de la jefatura. Se puede repetir.',
    updated_at  = now()
WHERE codigo = 'S10_REVISOR' AND state;

UPDATE ga_correo_evento
SET nombre      = 'Consolidado aprobado · al consolidador',
    descripcion = 'Se envía al consolidador que adjuntó el Consolidado del S10 cuando la jefatura lo aprueba (y firma). Lleva el botón para ver el consolidado en la intranet.',
    updated_at  = now()
WHERE codigo = 'REEMBOLSO_APROBADO' AND state;

UPDATE ga_correo_evento
SET nombre      = 'Consolidado observado · al consolidador',
    descripcion = 'Se envía al consolidador que adjuntó el Consolidado del S10 cuando la jefatura lo observa. Lleva la observación y el botón para subsanarla: recargar el consolidado o pedirle la corrección al Coordinador ERP.',
    updated_at  = now()
WHERE codigo = 'REEMBOLSO_RECHAZADO' AND state;

UPDATE ga_correo_evento
SET nombre      = 'Consolidado firmado · a Tesorería',
    descripcion = 'Avisa a Tesorería que un consolidado quedó firmado por la jefatura y entró a su bandeja de Reembolsos. El destinatario principal son los que tienen el rol TESORERO; los demás se agregan acá como destinatarios (por rol, área, trabajador o correo).',
    updated_at  = now()
WHERE codigo = 'TESORERIA_REEMBOLSO' AND state;

UPDATE ga_correo_evento
SET descripcion = 'Lo dispara el consolidador desde Consolidados cuando un Consolidado del S10 quedó observado. Le avisa al Coordinador ERP que hay una corrección esperándolo, con el número de reembolso, la observación y el motivo del consolidador.',
    updated_at  = now()
WHERE codigo = 'CORRECCION_S10_SOLICITADA' AND state;

-- ── 2) El aviso a los trabajadores al adjuntar el consolidado ──────────────
-- Va al final del orden: en Gestión de Rendiciones queda después del aviso a
-- los consolidadores.
INSERT INTO ga_correo_evento (
    codigo, nombre, descripcion, orden,
    destinatario_principal_nombre, destinatario_principal_activo,
    permite_desactivar_envio, permite_desactivar_principal,
    pantalla_id, grupo_id)
SELECT
    'REN_INCLUIDA_CONSOLIDADO',
    'Rendición incluida en un consolidado · al solicitante',
    'Le avisa a cada trabajador que su rendición quedó incluida en el Consolidado del S10 que adjuntó el consolidador, con el botón para verla. Es informativo: sale uno por rendición y trabajador, y reemplazar el consolidado no lo repite.',
    (SELECT COALESCE(MAX(orden), 0) + 1 FROM ga_correo_evento WHERE state),
    'El solicitante', true,
    true, true,
    p.id, g.id
FROM   ga_correo_pantalla p
CROSS  JOIN ga_correo_grupo g
WHERE  p.codigo = 'GESTION_RENDICIONES' AND p.state
  AND  g.codigo = 'CORREOS' AND g.state
  AND  NOT EXISTS (SELECT 1 FROM ga_correo_evento e
                   WHERE lower(e.codigo) = 'ren_incluida_consolidado' AND e.state);

COMMIT;

-- ── Verificación (solo lectura) ─────────────────────────────────────────────
SELECT p.codigo AS pantalla, e.orden, e.codigo, e.nombre, e.active
FROM ga_correo_evento e
JOIN ga_correo_pantalla p ON p.id = e.pantalla_id
WHERE e.state
  AND p.codigo IN ('CONSOLIDADOS', 'GESTION_RENDICIONES')
ORDER BY p.codigo, e.orden, e.id;
