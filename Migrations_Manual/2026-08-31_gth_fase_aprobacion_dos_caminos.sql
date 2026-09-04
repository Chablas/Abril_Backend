-- ============================================================================
-- Gestión GTH · Reclutamiento — La fase de aprobación tiene dos caminos
--
-- `APROBACION_GG` es UNA fase del pipeline por la que pasan todas las vacantes
-- que necesitan firma, pero quién firma lo decide la vacante:
--   • NUEVO     → Gerencia General (una sola firma).
--   • REEMPLAZO → gerente del área y, con su visto bueno, GTH (dos firmas
--                 secuenciales).
--   • FFT       → nadie: el ingreso directo ni siquiera entra en esta fase.
--
-- El nombre del catálogo se quedó con el del primer diseño ("Aprobación Gerencia
-- General"), así que al solicitante de un reemplazo la línea de tiempo del
-- seguimiento le nombraba a un gerente que no ve ni firma su pedido. Pasa a
-- nombrar los dos caminos, que es lo que describe la fase completa.
--
-- La descripción venía de más atrás todavía ("solo aplica cuando es puesto nuevo
-- o perfil fuera del catálogo aprobado"), de cuando la aprobación era la
-- excepción y no el paso obligado de toda vacante que no sea un ingreso directo.
--
-- El BADGE de estado de cada vacante no sale de acá: el backend lo rotula por
-- vacante con quién tiene el turno — "Aprobación Gerencia General" / "Aprobación
-- Gerencia del Área" / "Aprobación GTH" (ver EtiquetaAprobacion en el backend).
-- Este nombre genérico es el de la fase, que se describe sin hablar de una
-- vacante concreta.
--
-- Solo renombra: no toca estados de requerimientos ni decisiones.
-- Idempotente: se puede correr múltiples veces sin duplicar ni romper nada.
-- ============================================================================

BEGIN;

UPDATE gth_estado_requerimiento
SET nombre            = 'Aprobación Gerencia General / Gerencia del Área',
    descripcion       = 'Las vacantes nuevas las aprueba Gerencia General; los reemplazos, el gerente del área y luego GTH.',
    updated_date_time = now()
WHERE codigo = 'APROBACION_GG'
  AND state  = true
  AND (nombre      <> 'Aprobación Gerencia General / Gerencia del Área'
    OR descripcion IS DISTINCT FROM 'Las vacantes nuevas las aprueba Gerencia General; los reemplazos, el gerente del área y luego GTH.');

COMMIT;
