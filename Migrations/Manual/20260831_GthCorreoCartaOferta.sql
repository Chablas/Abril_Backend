-- ============================================================================
-- Gestión GTH · Onboarding — Correo de la carta oferta configurable
-- Fecha: 2026-08-31
--
-- El correo que recibe el colaborador con el enlace para leer su carta oferta,
-- registrar su firma y firmarla en línea (sale al abrir el onboarding y cada vez
-- que GTH reenvía el enlace desde el detalle) era el único correo del módulo que
-- iba cableado en el código: destinatario fijo y sin pantalla donde tocarlo.
-- Ahora entra al mismo esquema que los otros veintidós y se administra desde
-- /gestion-gth/onboarding/configuracion.
--
--   • gth_correo_tipo         → CARTA_OFERTA (nuevo, con principal automático)
--   • gth_correo_destinatario → GTH_AREA en copia, APAGADO
--
-- El destinatario principal lo sigue poniendo el sistema: es el correo personal
-- del colaborador, que sale de su ficha (o el que GTH escriba a mano al abrir el
-- onboarding). Por eso va como principal_automatico = true y no como una fila de
-- destinatarios: no hay una dirección que configurar, cambia en cada envío. Lo
-- que la pantalla agrega es poder sumarle principales y copias, y poder apagarlo
-- —a él con su propio interruptor, o al correo entero con el maestro—.
--
-- La fila GTH_AREA nace APAGADA a propósito. Es el mismo criterio que ya tiene
-- ENTREVISTA (el otro correo que sale hacia afuera): la copia a GTH queda
-- disponible como un interruptor en la pantalla, pero prenderla es una decisión
-- de GTH sobre cuándo quiere empezar a recibirla, no algo que decida este script.
--
-- ⚠️ Sin correr el script el correo sigue saliendo igual: cuando el tipo no
--    existe, el backend cae en su default (principal automático activo y sin
--    destinatarios configurados), que es exactamente el comportamiento de hoy.
--    Lo único que falta hasta correrlo es la pantalla, que se vería vacía.
--
-- Idempotente: se puede correr varias veces sin duplicar ni pisar nada.
-- Aplicar en dev y prod.
-- ============================================================================

BEGIN;

-- ============================================================================
-- 1) Tipo de correo CARTA_OFERTA
--
--    orden = 23: va al final de la lista, detrás de AGRADECIMIENTO (22), que es
--    el último de Reclutamiento. El onboarding es la fase que sigue al cierre
--    del requerimiento, así que el orden del catálogo sigue siendo el del flujo.
--    Los órdenes existentes no se tocan.
--
--    active = true (default): el correo nace prendido, que es como está hoy.
-- ============================================================================
INSERT INTO gth_correo_tipo (
    codigo, nombre, descripcion, orden,
    principal_automatico, principal_automatico_nombre
)
SELECT 'CARTA_OFERTA',
       'Carta oferta al colaborador',
       'Le manda al colaborador el enlace para leer su carta oferta, registrar su firma y firmarla en línea. Sale al abrir el onboarding y cada vez que se reenvía el enlace desde el detalle.',
       23,
       true,
       'Colaborador'
WHERE NOT EXISTS (
    SELECT 1 FROM gth_correo_tipo WHERE codigo = 'CARTA_OFERTA' AND state
);

-- ============================================================================
-- 2) Destinatario dinámico GTH_AREA, en copia y APAGADO
--
--    Va sin email: lo resuelve el backend leyendo area_scope.email del nodo de
--    Gestión del Talento Humano, igual que en el resto de los correos. Al estar
--    apagado no cambia nada del envío de hoy; queda como un interruptor en la
--    pantalla para el día que GTH quiera quedarse con copia de cada carta.
-- ============================================================================
INSERT INTO gth_correo_destinatario
    (gth_correo_tipo_id, codigo, email, nombre, es_copia, orden, active)
SELECT t.gth_correo_tipo_id,
       'GTH_AREA',
       NULL,
       'Área de Gestión del Talento Humano',
       true,
       1,
       false
FROM gth_correo_tipo t
WHERE t.codigo = 'CARTA_OFERTA' AND t.state
  AND NOT EXISTS (
      SELECT 1 FROM gth_correo_destinatario d
      WHERE d.gth_correo_tipo_id = t.gth_correo_tipo_id
        AND upper(d.codigo) = 'GTH_AREA' AND d.state
  );

COMMIT;

-- ============================================================================
-- Verificación
-- ============================================================================
-- El correo nuevo y sus destinatarios (debe salir CARTA_OFERTA activo, con
-- principal automático "Colaborador" y una sola fila GTH_AREA en copia y en f):
-- SELECT t.codigo AS correo, t.nombre, t.active AS correo_activo, t.orden,
--        t.principal_automatico, t.principal_automatico_active,
--        t.principal_automatico_nombre,
--        d.codigo AS destinatario, d.es_copia, d.active
--   FROM gth_correo_tipo t
--   LEFT JOIN gth_correo_destinatario d
--          ON d.gth_correo_tipo_id = t.gth_correo_tipo_id AND d.state
--  WHERE t.state AND t.codigo = 'CARTA_OFERTA';
--
-- El catálogo completo en el orden en que se muestran las pestañas (CARTA_OFERTA
-- debe quedar último, en 23, y ningún otro orden debe haber cambiado):
-- SELECT codigo, nombre, orden, active FROM gth_correo_tipo
--  WHERE state ORDER BY orden, gth_correo_tipo_id;
