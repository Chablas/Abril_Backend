-- ─────────────────────────────────────────────────────────────────────────────
-- Logo del proyecto para el PDF "Control de Licencias" (organizador visual):
-- además del logo ABRIL, cada proyecto puede subir su propio logo, que se
-- imprime a la derecha del encabezado. Si un proyecto no tiene logo cargado,
-- se avisa (campanita + correo) al Coordinador SSOMA y al Administrador del
-- proyecto, vía el mismo cron de recordatorios de Control de Licencias.
-- ─────────────────────────────────────────────────────────────────────────────

ALTER TABLE project ADD COLUMN IF NOT EXISTS logo_url TEXT;

-- Tipo de notificación in-app de la campanita para el aviso de logo pendiente.
INSERT INTO notificacion_tipo (codigo, nombre, orden, created_date_time, active, state)
VALUES ('VECINOS_LOGO_PENDIENTE', 'Falta subir el logo del proyecto', 110, now(), true, true)
ON CONFLICT (codigo) WHERE (state = true)
DO UPDATE SET nombre = EXCLUDED.nombre, active = true, updated_date_time = now();
