-- ============================================================================
-- 2026-09-02 · "Jefe del trabajador" y "Solicitante de la vacante" pasan a ser
--              dos destinatarios que NO se pisan
--
-- Pantalla: /ssoma/salud-ocupacional/emos/configuracion
--
-- ── El problema ─────────────────────────────────────────────────────────────
-- Los dos destinatarios de jefatura se solapaban y ninguno se podía configurar
-- solo. Hasta hoy:
--
--   JEFE             → el jefe SIEMPRE, tanto de un trabajador de casa como de
--                      una ficha de pre-ingreso; en el pre-ingreso caía al
--                      revisor del área de destino, o sea al FUTURO jefe.
--   JEFE_SOLICITANTE → el solicitante de la vacante si la ficha es de
--                      pre-ingreso y, si no, otra vez el jefe.
--
-- Por eso, con "Jefe del trabajador" prendido en la Programación aceptada por
-- la clínica, al jefe que pidió una vacante le llegaba la cita del EMO de
-- Ingreso de un postulante que todavía no es su trabajador (ni de Abril). Eso
-- no le interesa: lo que le importa de esa persona lo cuenta Reclutamiento, no
-- la agenda de la clínica.
--
-- ── Lo que queda ────────────────────────────────────────────────────────────
--   JEFE        → el jefe ACTUAL de alguien que ya trabaja acá. En una ficha de
--                 pre-ingreso no aporta a nadie.
--   SOLICITANTE → quien pidió la vacante en Solicitud de Personal (un jefe o
--                 gerente que sí trabaja acá). Solo en las fichas de
--                 pre-ingreso; nunca cae al jefe del trabajador.
--
-- Cada ficha aporta exactamente uno de los dos, según su workers_estado_id
-- (WorkersEstadoIds.PreIngreso = 4 Finalista aprobado, 5 No ingresó). El corte
-- vive en el código (EmoDestinatariosResolver), no en esta tabla: no es algo
-- que se configure. Lo que sí se configura desde la pantalla es a cuál de los
-- dos le habla cada correo — que es justamente lo que antes no se podía.
--
-- Este script solo renombra. NINGUNA celda de la matriz cambia de estado: la
-- configuración que hay hoy en producción ya es la que se quiere
--   • Jefe del trabajador: prendido en Oficina Central y Staff en la
--     Programación aceptada y en la Rechazada por la clínica.
--   • Solicitante de la vacante: apagado en los 5 correos y en los 3 perfiles.
-- Al pasar a ser dos destinatarios que no se pisan, esas mismas celdas ahora
-- significan lo que dicen.
--
-- ── Orden respecto del deploy ───────────────────────────────────────────────
-- Da igual, y conviene correrlo junto con el deploy. El único efecto de la
-- ventana entre uno y otro es que el destinatario renombrado no se encuentre
-- por código (el backend viejo busca JEFE_SOLICITANTE y el nuevo SOLICITANTE),
-- y como en producción está apagado en los 5 correos no le llega a nadie ni
-- antes ni después. La fila de JEFE no cambia de código, así que ese
-- destinatario sigue resolviéndose durante toda la ventana.
--
-- Idempotente: se puede correr más de una vez sin duplicar ni deshacer nada.
-- ============================================================================

BEGIN;

-- ── 1) JEFE_SOLICITANTE pasa a ser SOLICITANTE ──────────────────────────────
-- Se renombra la fila en vez de darla de baja y crear otra: es el mismo
-- destinatario con el alcance recortado, y conservarla mantiene sus 15 celdas
-- (5 correos × 3 perfiles) con lo que el usuario haya configurado en cada una.
UPDATE ss_emo_correo_destinatario
SET codigo      = 'SOLICITANTE',
    nombre      = 'Solicitante de la vacante',
    descripcion = 'Se resuelve al enviar: quien pidió la vacante en Solicitud de Personal. '
               || 'Solo aplica mientras la ficha sea de pre-ingreso — a un trabajador que ya '
               || 'está en Abril le escribe el Jefe del trabajador, no este destinatario.'
WHERE state
  AND upper(codigo) = 'JEFE_SOLICITANTE';

-- ── 2) JEFE deja dicho que es solo de quien ya trabaja acá ──────────────────
UPDATE ss_emo_correo_destinatario
SET descripcion = 'Se resuelve al enviar: el jefe actual de un trabajador que ya está en Abril '
               || '(su jefe personalizado o, si no tiene, el revisor de su área). A una ficha de '
               || 'pre-ingreso no le llega por esta vía: a esa le escribe el Solicitante de la vacante.'
WHERE state
  AND upper(codigo) = 'JEFE';

COMMIT;

-- ============================================================================
-- Verificación: las dos filas renombradas y su matriz, que NO debe haber
-- cambiado (Jefe del trabajador prendido en OC y Staff de ACEPTADA y RECHAZADA;
-- Solicitante de la vacante apagado en todo).
--
-- SELECT e.codigo AS correo, d.codigo AS destinatario,
--        max(CASE WHEN p.codigo = 'OFICINA_CENTRAL' THEN r.active::int END) AS of_central,
--        max(CASE WHEN p.codigo = 'STAFF'           THEN r.active::int END) AS staff,
--        max(CASE WHEN p.codigo = 'OBRA'            THEN r.active::int END) AS obra
-- FROM ss_emo_correo_regla r
-- JOIN ss_emo_correo_evento e       ON e.id = r.evento_id
-- JOIN ss_emo_correo_perfil p       ON p.id = r.perfil_id
-- JOIN ss_emo_correo_destinatario d ON d.id = r.destinatario_id
-- WHERE r.state AND upper(d.codigo) IN ('JEFE', 'SOLICITANTE')
-- GROUP BY e.orden, e.codigo, d.orden, d.id, d.codigo
-- ORDER BY e.orden, d.orden, d.id;
-- ============================================================================
