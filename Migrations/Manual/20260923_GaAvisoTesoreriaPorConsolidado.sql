-- ============================================================================
-- Gestión Administrativa · Salidas — El aviso a Tesorería pasa a ser por consolidado
-- Fecha: 2026-09-23
--
-- Al firmarse un consolidado de N planillas, Tesorería recibía N correos
-- «Reembolso por pagar - rendición REN-…», uno por planilla. Ahora recibe UNO
-- por consolidado («Consolidado pendiente de revisión - CONS-…»), con el resumen
-- del documento entero. El cambio es de código: acá solo se corrige la
-- descripción que muestra Consolidados → Configuración → Correos, que seguía
-- hablando de «una planilla».
--
-- ORDEN DE EJECUCIÓN: indistinto (es texto). Re-corrible. Aplicar en dev, demo y prod.
-- ============================================================================

UPDATE ga_correo_evento
   SET descripcion = 'Avisa a Tesorería que un consolidado quedó firmado por la jefatura y su '
                     'reembolso entró a la bandeja de pago. El destinatario principal son los que '
                     'tienen el rol TESORERO; los demás se agregan acá como destinatarios (por rol, '
                     'área, trabajador o correo).',
       updated_at  = now()
 WHERE codigo = 'TESORERIA_REEMBOLSO' AND state;
