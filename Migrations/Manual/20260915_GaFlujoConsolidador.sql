-- ============================================================================
-- Gestión Administrativa — Después de la primera revisión, el trámite del S10
-- es del CONSOLIDADOR y no del trabajador.
--
-- Qué cambia y por qué
--
-- El trabajador ahora solo rinde, envía la planilla a primera revisión y la
-- subsana si vuelve observada. Todo lo que viene después lo hace el consolidador
-- de su área (Consolidados → Configuración → Consolidadores):
--   • adjunta el Consolidado del S10 desde Gestión de Rendiciones (puede juntar
--     rendiciones de razones sociales distintas);
--   • le avisa a la jefatura desde Consolidados («Avisar a la jefatura»);
--   • le pide la corrección al Coordinador ERP desde Consolidados.
-- La jefatura sigue siendo la única que aprueba u observa el reembolso, y los
-- correos que antes le llegaban al trabajador por el reembolso (aprobado,
-- observado por la jefatura, observado por Tesorería, corrección atendida) ahora
-- le llegan al consolidador.
--
-- Este script NO toca el esquema: solo pone el catálogo de correos al día.
--
-- 1) S10_REVISOR y CORRECCION_S10_SOLICITADA pasan de la pantalla RENDICIONES a
--    CONSOLIDADOS: el catálogo cuelga cada correo de la pantalla donde SE ORIGINA,
--    y los dos botones que los disparan se mudaron a Consolidados. Sus reglas de
--    destinatarios (ga_correo_regla) se mudan solas: cuelgan del evento.
--
-- 2) Nombre, descripción y destinatario principal de los correos que cambiaron
--    de destinatario (trabajador → consolidador), para que la configuración diga
--    a quién le llegan de verdad.
--
-- ORDEN DE EJECUCIÓN
--   • Da igual antes o después de desplegar: el backend busca los correos por
--     código, no por pantalla. Correrlo antes deja la configuración de Consolidados
--     lista cuando aparezcan los botones nuevos.
--
-- Re-ejecutable: cada UPDATE deja los mismos valores si se corre dos veces.
-- ============================================================================

-- El archivo está en UTF-8 y trae texto acentuado. En Windows psql arranca con
-- el client_encoding del locale (WIN1252) y esos bytes se rechazan, así que se
-- fija acá: el script queda correcto sin depender de cómo se invoque.
SET client_encoding TO 'UTF8';

BEGIN;

-- ── 0) Prerrequisitos ───────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM ga_correo_pantalla WHERE codigo = 'CONSOLIDADOS' AND state)
    THEN
        RAISE EXCEPTION
            'Falta la pantalla CONSOLIDADOS en ga_correo_pantalla. Correr primero los scripts de Consolidados.';
    END IF;
END $$;

-- ── 1) Los correos que dispara el consolidador se administran en Consolidados ─
UPDATE ga_correo_evento
SET pantalla_id = (SELECT id FROM ga_correo_pantalla WHERE codigo = 'CONSOLIDADOS' AND state),
    nombre = 'Consolidado por revisar · a la jefatura',
    descripcion = 'Lo dispara el consolidador desde Consolidados («Avisar a la jefatura») cuando un Consolidado del S10 tiene reembolsos esperando la aprobación de la jefatura. Se puede repetir.',
    destinatario_principal_nombre = 'La jefatura de los trabajadores del consolidado',
    updated_at = now()
WHERE codigo = 'S10_REVISOR' AND state;

UPDATE ga_correo_evento
SET pantalla_id = (SELECT id FROM ga_correo_pantalla WHERE codigo = 'CONSOLIDADOS' AND state),
    nombre = 'Corrección del S10 solicitada · al Coordinador ERP',
    descripcion = 'Lo dispara el consolidador desde Consolidados cuando el reembolso de un Consolidado del S10 quedó observado. Le avisa al Coordinador ERP que hay una corrección esperándolo, con el número de reembolso, la observación y el motivo del consolidador.',
    destinatario_principal_nombre = 'Coordinador ERP',
    updated_at = now()
WHERE codigo = 'CORRECCION_S10_SOLICITADA' AND state;

-- ── 2) Los correos del reembolso le llegan al consolidador ──────────────────
UPDATE ga_correo_evento
SET nombre = 'Reembolso aprobado · al consolidador',
    descripcion = 'Se envía al consolidador que adjuntó el Consolidado del S10 cuando la jefatura aprueba (y firma) su reembolso. Lleva el botón para ver el consolidado en la intranet.',
    destinatario_principal_nombre = 'El consolidador que adjuntó el consolidado',
    updated_at = now()
WHERE codigo = 'REEMBOLSO_APROBADO' AND state;

UPDATE ga_correo_evento
SET nombre = 'Reembolso observado · al consolidador',
    descripcion = 'Se envía al consolidador que adjuntó el Consolidado del S10 cuando la jefatura observa su reembolso. Lleva la observación y el botón para subsanarla: recargar el consolidado o pedirle la corrección al Coordinador ERP.',
    destinatario_principal_nombre = 'El consolidador que adjuntó el consolidado',
    updated_at = now()
WHERE codigo = 'REEMBOLSO_RECHAZADO' AND state;

UPDATE ga_correo_evento
SET nombre = 'Reembolso observado por Tesorería · al consolidador',
    descripcion = 'Avisa al consolidador que Tesorería devolvió el reembolso de su Consolidado del S10 antes de pagarlo, con el motivo y los dos caminos para subsanar: recargar el consolidado o pedirle la corrección al Coordinador ERP. Sale una vez por consolidado.',
    destinatario_principal_nombre = 'El consolidador que adjuntó el consolidado',
    updated_at = now()
WHERE codigo = 'REEMBOLSO_OBSERVADO_TESORERIA' AND state;

UPDATE ga_correo_evento
SET nombre = 'Corrección del S10 atendida · al consolidador',
    descripcion = 'Le avisa al consolidador que pidió la corrección que el Coordinador ERP ya la hizo en el S10 y que puede recargar el Consolidado del S10. Sale una vez por consolidado.',
    destinatario_principal_nombre = 'El consolidador que pidió la corrección',
    updated_at = now()
WHERE codigo = 'CORRECCION_S10_ATENDIDA' AND state;

-- La primera revisión aprobada sigue llegándole al trabajador, pero ya no le
-- pide nada: lo que sigue es del consolidador.
UPDATE ga_correo_evento
SET descripcion = 'Le avisa al trabajador que su jefe aprobó la primera revisión. Es informativo: lo que sigue (el Consolidado del S10) es del consolidador de su área.',
    updated_at = now()
WHERE codigo = 'REN_PRIMERA_APROBADA' AND state;

COMMIT;

-- ── Verificación (solo lectura) ─────────────────────────────────────────────
SELECT e.codigo, p.codigo AS pantalla, e.nombre, e.destinatario_principal_nombre
FROM ga_correo_evento e
JOIN ga_correo_pantalla p ON p.id = e.pantalla_id
WHERE e.state
  AND e.codigo IN ('S10_REVISOR', 'CORRECCION_S10_SOLICITADA', 'REEMBOLSO_APROBADO',
                   'REEMBOLSO_RECHAZADO', 'REEMBOLSO_OBSERVADO_TESORERIA',
                   'CORRECCION_S10_ATENDIDA', 'REN_PRIMERA_APROBADA')
ORDER BY p.codigo, e.orden, e.id;
