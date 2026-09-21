-- ============================================================================
-- Gestión Administrativa · Solicitud de Salidas — el jefe del área se entera
-- cuando la salida la aprueba un residente.
--
-- Qué cambia y por qué
--
-- Las áreas marcadas como «filtrar por proyecto» en Solicitud de Salidas →
-- Configuración → Revisores le mandan la solicitud al residente de la obra, que
-- es quien la aprueba o la rechaza. El jefe de esa área se quedaba sin enterarse
-- de las salidas de su propia gente.
--
-- Desde ahora, cuando el revisor resuelto de un trabajador tiene categoría
-- RESIDENTE (workers.puesto_id → puesto.categoria_id), el jefe de su área:
--
--   1) recibe un correo INFORMATIVO propio (REVISOR_JEFE_AREA): el mismo detalle
--      de la solicitud, sin los botones de aprobar/rechazar (decidir sigue siendo
--      del residente) y sin los documentos adjuntos, que son solo para quien
--      decide;
--   2) entra además en la CONFIRMACIÓN al solicitante, como un destinatario más
--      —activable y desactivable como cualquier otro— gracias al tipo de
--      destinatario nuevo JEFE_AREA.
--
-- El jefe es el que la sección Revisores muestra en la fila del área SIN filtrar
-- por proyecto: el revisor asignado a nivel de área si lo hay y, si no, el Jefe
-- del área (o el Gerente de la gerencia de la que cuelga, subiendo por el árbol).
-- Es el mismo algoritmo del revisor, sin la obra.
--
-- ORDEN DE EJECUCIÓN
--   • ANTES del deploy, o en cualquier momento antes de que se registre una
--     solicitud con revisor residente. El bloque 2 toca el esquema (el CHECK de
--     ga_correo_regla) y sin él el backend NO puede guardar la fila del jefe del
--     área en la confirmación: el INSERT del bloque 3 —y cualquier intento desde
--     la pantalla— caería con 23514. El correo informativo del bloque 4, en
--     cambio, sale igual sin su fila (un código que no está en ga_correo_evento
--     se trata como "activo y sin copias"); lo que falta sin ella es poder
--     apagarlo desde Configuración.
--
-- Re-ejecutable: cada bloque está guardado por la existencia de lo que crea, así
-- que una segunda corrida no inserta, no renumera ni vuelve a tocar el CHECK.
-- ============================================================================

-- El archivo está en UTF-8 y trae texto acentuado. En Windows psql arranca con
-- el client_encoding del locale (WIN1252) y esos bytes se rechazan, así que se
-- fija acá: el script queda correcto sin depender de cómo se invoque.
SET client_encoding TO 'UTF8';

BEGIN;

-- ── 0) Prerrequisitos ───────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM ga_correo_pantalla WHERE codigo = 'SOLICITUD_SALIDAS' AND state)
    THEN
        RAISE EXCEPTION
            'Falta la pantalla SOLICITUD_SALIDAS en ga_correo_pantalla. Correr primero Migrations/Manual/20260908_GaCorreosPorPantallaYPlazoRendicion.sql.';
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

    IF NOT EXISTS (SELECT 1 FROM ga_correo_evento WHERE codigo = 'REVISOR' AND state)
    THEN
        RAISE EXCEPTION
            'Falta el correo REVISOR en ga_correo_evento: el aviso al jefe del área va justo detrás de él.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM ga_correo_evento WHERE codigo = 'CONFIRMACION' AND state)
    THEN
        RAISE EXCEPTION
            'Falta el correo CONFIRMACION en ga_correo_evento: es donde se carga al jefe del área como destinatario.';
    END IF;

    -- El id 5 se usa literal en el CHECK del bloque 2 (los CHECK no admiten subconsultas), así
    -- que tiene que estar libre o ya ser JEFE_AREA. En dev y prod los cuatro tipos existentes
    -- son 1..4 (verificado 2026-09-17), así que el 5 es el siguiente natural en las dos.
    IF EXISTS (SELECT 1 FROM ga_correo_tipo_destinatario WHERE id = 5 AND codigo <> 'JEFE_AREA')
    THEN
        RAISE EXCEPTION
            'El id 5 de ga_correo_tipo_destinatario ya lo ocupa otro tipo (%). Hay que elegir otro id y cambiarlo también en el CHECK del bloque 2.',
            (SELECT codigo FROM ga_correo_tipo_destinatario WHERE id = 5);
    END IF;
END $$;

-- ── 1) Tipo de destinatario JEFE_AREA ───────────────────────────────────────
-- Es el segundo tipo que no apunta a nadie fijo. A diferencia de ROL —que se
-- resuelve con una consulta (quién tiene ese rol hoy)— este depende del CONTEXTO
-- del envío: quién registró la solicitud y si su revisor es un residente. Por eso
-- la fila no lleva worker_id / area_scope_id / correo / role_id: los cuatro van
-- NULL y el destinatario lo pone el backend al enviar.
--
-- Id explícito para que dev y prod queden iguales y el CHECK de abajo pueda
-- nombrarlo (mismo criterio que CategoriaIds).
INSERT INTO ga_correo_tipo_destinatario (id, codigo, nombre, orden, active, state)
SELECT 5, 'JEFE_AREA', 'Jefe del área del solicitante', 5, true, true
WHERE  NOT EXISTS (SELECT 1 FROM ga_correo_tipo_destinatario WHERE id = 5);

-- Un INSERT con id explícito no adelanta la secuencia: sin esto, el siguiente
-- tipo que se cree sin id moriría con 23505.
SELECT setval(
    pg_get_serial_sequence('ga_correo_tipo_destinatario', 'id'),
    GREATEST((SELECT MAX(id) FROM ga_correo_tipo_destinatario), 1));

-- ── 2) El CHECK deja pasar la fila sin objetivo de JEFE_AREA ───────────────
-- Antes exigía EXACTAMENTE uno de los cuatro campos. Ahora exige exactamente uno
-- para todos los tipos y exactamente CERO para JEFE_AREA: la garantía no se
-- relaja, se vuelve condicional al tipo.
DO $$
BEGIN
    IF pg_get_constraintdef(
           (SELECT oid FROM pg_constraint
            WHERE conrelid = 'ga_correo_regla'::regclass
              AND conname  = 'chk_ga_correo_regla_target')
       ) LIKE '%tipo_id%'
    THEN
        RAISE NOTICE 'chk_ga_correo_regla_target ya contempla JEFE_AREA: no se toca.';
        RETURN;
    END IF;

    ALTER TABLE ga_correo_regla DROP CONSTRAINT chk_ga_correo_regla_target;

    ALTER TABLE ga_correo_regla ADD CONSTRAINT chk_ga_correo_regla_target CHECK (
        (CASE WHEN worker_id     IS NOT NULL THEN 1 ELSE 0 END
       + CASE WHEN area_scope_id IS NOT NULL THEN 1 ELSE 0 END
       + CASE WHEN correo        IS NOT NULL THEN 1 ELSE 0 END
       + CASE WHEN role_id       IS NOT NULL THEN 1 ELSE 0 END)
        = CASE WHEN tipo_id = 5 THEN 0 ELSE 1 END   -- 5 = JEFE_AREA
    );
END $$;

-- ── 3) El jefe del área, como destinatario de la confirmación ──────────────
-- Queda ACTIVO de entrada: es lo que se pidió. Se apaga desde Solicitud de
-- Salidas → Configuración → Correos → Confirmación como cualquier otra fila, y
-- mientras el revisor del solicitante no sea un residente no le llega a nadie.
INSERT INTO ga_correo_regla (evento_id, tipo_id, orden, active, state)
SELECT e.id, 5,
       COALESCE((SELECT MAX(r.orden) FROM ga_correo_regla r WHERE r.evento_id = e.id AND r.state), 0) + 1,
       true, true
FROM   ga_correo_evento e
WHERE  e.codigo = 'CONFIRMACION' AND e.state
  AND  NOT EXISTS (SELECT 1 FROM ga_correo_regla r
                   WHERE r.evento_id = e.id AND r.tipo_id = 5 AND r.state);

-- ── 4) El aviso informativo, justo detrás del correo al revisor ────────────
-- La pantalla de Configuración ordena los correos solo por `orden`, así que para
-- que el hermano quede pegado al del revisor hay que abrirle el hueco: se corren
-- un lugar los que van después. El orden es global (las pantallas comparten la
-- numeración y cada una muestra los suyos), así que correrlos a todos mantiene
-- el orden relativo de todas las pantallas.
DO $$
DECLARE
    v_orden_revisor int;
BEGIN
    IF EXISTS (SELECT 1 FROM ga_correo_evento WHERE lower(codigo) = 'revisor_jefe_area' AND state)
    THEN
        RAISE NOTICE 'REVISOR_JEFE_AREA ya existe: no se inserta ni se renumera.';
        RETURN;
    END IF;

    SELECT orden INTO v_orden_revisor
    FROM   ga_correo_evento
    WHERE  codigo = 'REVISOR' AND state;

    UPDATE ga_correo_evento
    SET    orden = orden + 1,
           updated_at = now()
    WHERE  state AND orden > v_orden_revisor;

    INSERT INTO ga_correo_evento (
        codigo, nombre, descripcion, orden,
        destinatario_principal_nombre, destinatario_principal_activo,
        permite_desactivar_envio, permite_desactivar_principal,
        pantalla_id, grupo_id)
    SELECT
        'REVISOR_JEFE_AREA',
        'Al crear · Aviso al jefe del área',
        'Le avisa al jefe del área que una salida de su gente la va a aprobar el residente de la obra. Solo sale cuando el revisor del trabajador es de categoría RESIDENTE. Es informativo: lleva el detalle de la solicitud pero no los botones de aprobar/rechazar ni los documentos adjuntos, porque la decisión es del residente.',
        v_orden_revisor + 1,
        'El jefe del área del solicitante', true,
        true, true,
        p.id, g.id
    FROM   ga_correo_pantalla p
    CROSS  JOIN ga_correo_grupo g
    WHERE  p.codigo = 'SOLICITUD_SALIDAS' AND p.state
      AND  g.codigo = 'CORREOS' AND g.state;
END $$;

COMMIT;

-- ── Verificación (solo lectura) ─────────────────────────────────────────────
SELECT p.codigo AS pantalla, e.orden, e.codigo, e.nombre, e.active, e.destinatario_principal_nombre
FROM ga_correo_evento e
JOIN ga_correo_pantalla p ON p.id = e.pantalla_id
JOIN ga_correo_grupo g ON g.id = e.grupo_id
WHERE e.state AND g.codigo = 'CORREOS'
ORDER BY e.orden, e.id;

SELECT e.codigo AS correo, t.codigo AS tipo, r.active,
       COALESCE(r.correo, '(lo resuelve el envío)') AS destinatario
FROM ga_correo_regla r
JOIN ga_correo_evento e ON e.id = r.evento_id
JOIN ga_correo_tipo_destinatario t ON t.id = r.tipo_id
WHERE r.state AND e.codigo = 'CONFIRMACION'
ORDER BY r.orden, r.id;
