-- ═══════════════════════════════════════════════════════════════════════════
-- Gestión GTH · Onboarding: aviso a GTH de que el colaborador firmó su carta
--                           oferta (CARTA_OFERTA_FIRMADA)
-- ═══════════════════════════════════════════════════════════════════════════
--
-- Un tipo de correo nuevo. No es un cambio de esquema: la configuración de
-- correos es data (`gth_correo_tipo` + `gth_correo_destinatario`) y la pantalla
-- /gestion-gth/onboarding/configuracion se arma sola con lo que haya acá.
--
-- Qué correo es: la VUELTA de CARTA_OFERTA. La ida se la manda GTH al
-- colaborador con el enlace para leer y firmar su carta; esta sale cuando él
-- pulsa «Firmar» en esa misma página pública y le avisa a GTH que ya hay un
-- documento firmado esperando su revisión. Es el único aviso que existe de eso:
-- nadie de la empresa dispara el acto, así que sin este correo la carta firmada
-- se quedaría esperando a que alguien pase por la bandeja de Onboarding. Su
-- botón abre el detalle de ese colaborador, que es donde se aprueba.
--
-- No lleva destinatario principal automático: a quién le llega sale entero de
-- Configuración, igual que FORMULARIO_COMPLETADO, que es el mismo caso del lado
-- de Reclutamiento (un aviso a GTH disparado por el postulante).
--
-- ── Destinatarios: se copian del gemelo, no se escriben acá ────────────────
-- Mismo criterio que 2026-08-28_gth_correos_aviso_area_y_reemplazo_secuencial:
-- en producción los destinatarios dinámicos están casi todos APAGADOS y los
-- correos salen a buzones concretos. Una lista fija acá haría una de dos cosas
-- malas: prender GTH_AREA y empezar a escribirle a un buzón de área que la
-- organización todavía tiene apagado, o dejar el correo sin destinatarios y que
-- no salga nunca (que en este correo es peor que en otros: es el único aviso de
-- la firma).
--
-- El gemelo es FORMULARIO_COMPLETADO: mismo público (GTH), mismo disparo (una
-- persona de fuera que termina algo en una página pública) y misma pregunta del
-- otro lado (entrar a revisarlo). Copiándolo, el correo nuevo aterriza donde ya
-- aterrizan los avisos a GTH, con los mismos interruptores, en cualquier base.
--
-- Después de correr esto, revisar en Configuración → Onboarding que sea lo que
-- se quiere. Concretamente: PRENDER «Área de Gestión del Talento Humano» el día
-- que el buzón del área deba recibirlo de verdad.
--
-- Idempotente: se puede correr varias veces sin duplicar ni pisar nada.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

SET client_encoding TO 'UTF8';

-- ───────────────────────────────────────────────────────────────────────────
-- 1) El tipo nuevo
--    orden 24: va detrás de CARTA_OFERTA (23), que es el otro correo de la
--    pantalla de Onboarding, y en el orden en que ocurren.
-- ───────────────────────────────────────────────────────────────────────────
INSERT INTO gth_correo_tipo (codigo, nombre, descripcion, orden,
                             principal_automatico, principal_automatico_active,
                             principal_automatico_nombre, active, state, created_date_time)
SELECT 'CARTA_OFERTA_FIRMADA',
       'Carta oferta firmada (a GTH)',
       'Avisa a GTH que el colaborador firmó su carta oferta desde el enlace y que el documento firmado ya está en su file digital, esperando la revisión que destraba el onboarding.',
       24, false, true, NULL, true, true, now()
WHERE NOT EXISTS (
    SELECT 1 FROM gth_correo_tipo t
    WHERE  t.codigo = 'CARTA_OFERTA_FIRMADA' AND t.state = true
);

-- Nombre y descripción también en el rerun (por si el texto se afinó después de
-- la primera corrida). El `active` no se toca: apagar el correo es una decisión
-- de Configuración y volver a correr el script no debe deshacerla.
UPDATE gth_correo_tipo
SET    nombre                      = 'Carta oferta firmada (a GTH)',
       descripcion                 = 'Avisa a GTH que el colaborador firmó su carta oferta desde el enlace y que el documento firmado ya está en su file digital, esperando la revisión que destraba el onboarding.',
       orden                       = 24,
       principal_automatico        = false,
       principal_automatico_nombre = NULL,
       updated_date_time           = now()
WHERE  codigo = 'CARTA_OFERTA_FIRMADA' AND state = true;

-- ───────────────────────────────────────────────────────────────────────────
-- 2) Destinatarios copiados de FORMULARIO_COMPLETADO
-- ───────────────────────────────────────────────────────────────────────────

-- 2.a) Los correos escritos a mano del gemelo, tal cual y con su `active`. Solo
--      los que hoy están PRENDIDOS: los apagados del gemelo son restos de una
--      configuración anterior y copiarlos solo ensuciaría la sección nueva.
INSERT INTO gth_correo_destinatario (gth_correo_tipo_id, nombre, descripcion,
                                     email, es_copia, orden, active, state, created_date_time)
SELECT destino.gth_correo_tipo_id, d.nombre, d.descripcion,
       d.email, d.es_copia, d.orden, d.active, true, now()
FROM   gth_correo_tipo destino
JOIN   gth_correo_tipo gemelo ON gemelo.codigo = 'FORMULARIO_COMPLETADO' AND gemelo.state = true
JOIN   gth_correo_destinatario d
        ON d.gth_correo_tipo_id = gemelo.gth_correo_tipo_id AND d.state = true
WHERE  destino.codigo = 'CARTA_OFERTA_FIRMADA' AND destino.state = true
  AND  d.codigo IS NULL AND d.email IS NOT NULL AND d.active = true
  AND  NOT EXISTS (
        SELECT 1 FROM gth_correo_destinatario x
        WHERE  x.gth_correo_tipo_id = destino.gth_correo_tipo_id
          AND  x.state = true AND x.codigo IS NULL
          AND  lower(x.email) = lower(d.email));

-- 2.b) La fila dinámica del área de GTH, con el mismo `active` que tiene en el
--      gemelo: no se prende por nuestra cuenta un buzón que la organización
--      todavía tiene apagado. Va como principal (es_copia = false): este correo
--      le pide a GTH que entre a revisar, no lo pone en copia de nada.
INSERT INTO gth_correo_destinatario (gth_correo_tipo_id, codigo, nombre, es_copia,
                                     orden, active, state, created_date_time)
SELECT destino.gth_correo_tipo_id, 'GTH_AREA', 'Área de Gestión del Talento Humano', false, 1,
       coalesce((SELECT r.active
                   FROM gth_correo_destinatario r
                   JOIN gth_correo_tipo rt ON rt.gth_correo_tipo_id = r.gth_correo_tipo_id
                  WHERE rt.codigo = 'FORMULARIO_COMPLETADO' AND rt.state = true
                    AND upper(r.codigo) = 'GTH_AREA' AND r.state = true
                  LIMIT 1), true),
       true, now()
FROM   gth_correo_tipo destino
WHERE  destino.codigo = 'CARTA_OFERTA_FIRMADA' AND destino.state = true
  AND  NOT EXISTS (
        SELECT 1 FROM gth_correo_destinatario d
        WHERE  d.gth_correo_tipo_id = destino.gth_correo_tipo_id
          AND  upper(d.codigo) = 'GTH_AREA'
          AND  d.state = true);

-- ───────────────────────────────────────────────────────────────────────────
-- 3) Guarda: el correo tiene que quedar con al menos un destinatario PRINCIPAL
--    prendido, o no sale nunca y nadie se entera de ninguna firma. Si el gemelo
--    no aportó ninguno (una base donde FORMULARIO_COMPLETADO está sin
--    destinatarios activos), esto aborta sin dejar nada a medias.
-- ───────────────────────────────────────────────────────────────────────────
DO $$
DECLARE
    v_principales int;
BEGIN
    SELECT count(*) INTO v_principales
    FROM   gth_correo_destinatario d
    JOIN   gth_correo_tipo t ON t.gth_correo_tipo_id = d.gth_correo_tipo_id
    WHERE  t.codigo = 'CARTA_OFERTA_FIRMADA' AND t.state = true
      AND  d.state = true AND d.active = true AND d.es_copia = false;

    IF v_principales = 0 THEN
        RAISE EXCEPTION
            'CARTA_OFERTA_FIRMADA quedaria sin destinatarios principales activos: el aviso de la firma no le llegaria a nadie. Revisa los destinatarios de FORMULARIO_COMPLETADO, que es de donde se copian.';
    END IF;
END $$;

-- ───────────────────────────────────────────────────────────────────────────
-- 4) Cómo quedó (para leerlo en la salida del script)
-- ───────────────────────────────────────────────────────────────────────────
SELECT t.codigo AS correo, t.nombre, t.orden, t.active AS correo_activo,
       coalesce(d.codigo, d.email) AS destinatario,
       CASE WHEN d.es_copia THEN 'CC' ELSE 'Para' END AS lista,
       d.active AS destinatario_activo
FROM   gth_correo_tipo t
LEFT   JOIN gth_correo_destinatario d
        ON d.gth_correo_tipo_id = t.gth_correo_tipo_id AND d.state = true
WHERE  t.codigo IN ('CARTA_OFERTA', 'CARTA_OFERTA_FIRMADA') AND t.state = true
ORDER  BY t.orden, d.es_copia, d.orden;

COMMIT;
