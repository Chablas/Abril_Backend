-- ATS grupal armado SIN CONEXIÓN (PWA): la cuadrilla lo arma y captura las firmas en el sótano y alguien lo sube
-- arriba. client_id (generado en el teléfono) hace idempotente el envío: reintentar no duplica el grupo.
ALTER TABLE ss_ats_grupo ADD COLUMN IF NOT EXISTS origen_offline boolean NOT NULL DEFAULT false;
ALTER TABLE ss_ats_grupo ADD COLUMN IF NOT EXISTS capturado_en timestamp without time zone NULL;
ALTER TABLE ss_ats_grupo ADD COLUMN IF NOT EXISTS client_id uuid NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_ss_ats_grupo_client_id ON ss_ats_grupo (client_id) WHERE client_id IS NOT NULL;

-- Cada ATS capturado sin conexión queda marcado: su hora real es hora_dispositivo; hora_servidor_firma es la de la sincronización.
ALTER TABLE ss_ats ADD COLUMN IF NOT EXISTS origen_offline boolean NOT NULL DEFAULT false;
