-- ============================================================================
-- Gestión Administrativa — Reembolsos: «Reembolso realizado» sale uno por
-- persona, no uno por rendición
--
-- Qué cambia y por qué
--
--   Al marcar como pagado un consolidado, a cada colaborador le llegaba un
--   correo por cada una de sus rendiciones: con tres rendiciones en el
--   consolidado, tres correos del mismo pago. Desde este cambio le llega UNO
--   por pago, con el monto sumado y sus rendiciones una por una (código,
--   planilla, periodo, salidas y monto de cada una).
--
--   El cambio es del backend. Acá solo se pone al día la descripción del correo
--   en Reembolsos → Configuración → Correos, que todavía decía «sale una vez por
--   planilla y trabajador». El código no cambia: es la clave de la
--   configuración de destinatarios que ya está cargada.
--
-- Este script NO toca el esquema: solo el texto del catálogo de correos.
--
-- ORDEN DE EJECUCIÓN
--   • Da igual antes o después de desplegar: es solo el texto que muestra la
--     Configuración.
--
-- Re-ejecutable: el UPDATE deja siempre el mismo valor.
-- Aplicar en dev, demo y prod.
-- ============================================================================

-- El archivo está en UTF-8 y trae texto acentuado. En Windows psql arranca con
-- el client_encoding del locale (WIN1252) y esos bytes se rechazan, así que se
-- fija acá: el script queda correcto sin depender de cómo se invoque.
SET client_encoding TO 'UTF8';

BEGIN;

UPDATE ga_correo_evento
SET descripcion = 'Avisa al colaborador que Tesorería ya pagó su reembolso. Sale uno por persona en cada pago, con el monto sumado y sus rendiciones una por una, no uno por rendición.',
    updated_at  = now()
WHERE codigo = 'REEMBOLSO_PAGADO' AND state;

COMMIT;

-- ── Verificación (solo lectura) ─────────────────────────────────────────────
SELECT codigo, nombre, descripcion
FROM ga_correo_evento
WHERE codigo = 'REEMBOLSO_PAGADO' AND state;
