-- Onboarding: se va la fase «File digital» y el aviso al responsable de obra se muda a «Correo de
-- bienvenida», donde GTH lo manda con un clic.
--
-- Por qué:
--   • GUARDAR_CARTA_SHAREPOINT y AVISO_TI ya ocurren antes, en Reclutamiento: repetirlas acá era
--     mostrar como pendiente algo que ya pasó.
--   • AVISO_OBRA dejó de ser automática: la manda GTH: hasta que el correo no sale, la fecha de
--     ingreso todavía se puede mover, y avisar antes obliga a corregir después.
--   • Con esas tres fuera, FILE_DIGITAL se queda sin actividades y sale del embudo.
--
-- ORDEN DE DESPLIEGUE: este script va ANTES de subir el código. El backend nuevo lee las tres
-- columnas de aviso_obra_* en toda consulta a gth_onboarding, así que sin ellas la pantalla
-- responde 500. Al revés no pasa nada: el código viejo ignora lo que este script cambia.
--
-- Re-corrible: cada bloque revisa el estado antes de escribir.

BEGIN;

-- ── 1. Columnas del aviso al responsable de obra ────────────────────────────
-- Es lo único que marca esa actividad como cumplida. El correo se guarda porque el coordinador
-- administrativo del proyecto puede cambiar después: sin esto la pantalla mostraría el buzón de
-- quien está hoy como si fuera el que recibió el aviso.
ALTER TABLE gth_onboarding
    ADD COLUMN IF NOT EXISTS aviso_obra_enviado_date_time timestamptz,
    ADD COLUMN IF NOT EXISTS aviso_obra_email             text,
    ADD COLUMN IF NOT EXISTS aviso_obra_user_id           integer;

-- ── 2. Los onboardings parados en FILE_DIGITAL pasan a CORREO_BIENVENIDA ────
-- Va primero: la fase no se puede dar de baja con gente adentro.
UPDATE gth_onboarding o
SET    gth_onboarding_fase_id = (SELECT gth_onboarding_fase_id FROM gth_onboarding_fase
                                 WHERE codigo = 'CORREO_BIENVENIDA' AND state),
       updated_date_time      = now()
WHERE  o.state
  AND  o.gth_onboarding_fase_id = (SELECT gth_onboarding_fase_id FROM gth_onboarding_fase
                                   WHERE codigo = 'FILE_DIGITAL' AND state);

-- ── 3. AVISO_OBRA se muda, se renombra y deja de ser automática ─────────────
UPDATE gth_onboarding_actividad
SET    gth_onboarding_fase_id = (SELECT gth_onboarding_fase_id FROM gth_onboarding_fase
                                 WHERE codigo = 'CORREO_BIENVENIDA' AND state),
       nombre      = 'Enviar aviso al responsable de obra para prever espacio y condiciones de ingreso',
       descripcion = 'Va al coordinador administrativo del proyecto destino. Los ingresos a Oficina Central no lo llevan.',
       orden       = 3,
       automatica  = false,
       updated_date_time = now()
WHERE  codigo = 'AVISO_OBRA' AND state;

-- ── 4. Bajan las dos actividades que ya ocurren en Reclutamiento ────────────
UPDATE gth_onboarding_actividad
SET    active = false, state = false, updated_date_time = now()
WHERE  codigo IN ('GUARDAR_CARTA_SHAREPOINT', 'AVISO_TI') AND state;

-- ── 5. Baja la fase FILE_DIGITAL ────────────────────────────────────────────
UPDATE gth_onboarding_fase
SET    active = false, state = false, updated_date_time = now()
WHERE  codigo = 'FILE_DIGITAL' AND state;

-- ── 6. Renumeración del embudo ──────────────────────────────────────────────
-- El `orden` es el número que la pantalla dibuja en cada círculo: sin esto el proceso arrancaría
-- en un «2».
UPDATE gth_onboarding_fase SET orden = 1, updated_date_time = now() WHERE codigo = 'CORREO_BIENVENIDA' AND state AND orden <> 1;
UPDATE gth_onboarding_fase SET orden = 2, updated_date_time = now() WHERE codigo = 'FORMULARIO_WEB'    AND state AND orden <> 2;
UPDATE gth_onboarding_fase SET orden = 3, updated_date_time = now() WHERE codigo = 'PREINICIO'         AND state AND orden <> 3;
UPDATE gth_onboarding_fase SET orden = 4, updated_date_time = now() WHERE codigo = 'CIERRE_ONBOARDING' AND state AND orden <> 4;
UPDATE gth_onboarding_fase SET orden = 5, updated_date_time = now() WHERE codigo = 'BASE_MAESTRA'      AND state AND orden <> 5;

-- ── 7. El correo configurable ───────────────────────────────────────────────
-- Es el primer correo de Onboarding y se administra desde su propia pantalla de Configuración
-- (CorreoConfigService.CorreosPorPantalla → "onboarding"). El destinatario principal lo pone el
-- sistema —el coordinador administrativo del proyecto destino, que cambia con cada colaborador—,
-- así que va como principal automático y no como fila de gth_correo_destinatario.
INSERT INTO gth_correo_tipo (codigo, nombre, descripcion, orden,
                             principal_automatico, principal_automatico_active, principal_automatico_nombre)
SELECT 'AVISO_OBRA',
       'Aviso al responsable de obra',
       'Le pide al coordinador administrativo del proyecto que prevea espacio y condiciones para el nuevo ingreso.',
       26, true, true, 'Coordinador administrativo del proyecto'
WHERE  NOT EXISTS (SELECT 1 FROM gth_correo_tipo WHERE codigo = 'AVISO_OBRA' AND state);

COMMIT;

-- Verificación
-- SELECT codigo, nombre, orden, active, state FROM gth_onboarding_fase ORDER BY state DESC, orden;
-- SELECT a.codigo, f.codigo AS fase, a.orden, a.automatica, a.state
--   FROM gth_onboarding_actividad a
--   JOIN gth_onboarding_fase f ON f.gth_onboarding_fase_id = a.gth_onboarding_fase_id
--  ORDER BY a.state DESC, f.orden, a.orden;
-- SELECT codigo, nombre, orden, principal_automatico FROM gth_correo_tipo WHERE codigo = 'AVISO_OBRA';
