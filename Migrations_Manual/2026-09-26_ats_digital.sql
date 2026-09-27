-- ============================================================================
-- ATS Digital (Análisis de Trabajo Seguro) — SsAtsFeature
--
-- Reemplaza el llenado en Excel (SSO-FO-018.m "ATS SUPERVISIÓN") por un
-- formulario digital que cada miembro del staff llena y firma desde su propio
-- celular/PC, con evidencia de trazabilidad pensada para sostener una
-- inspección de SUNAFIL o un peritaje en el peor de los casos:
--   - hora de FIRMA la pone el servidor, nunca el dispositivo
--   - geolocalización (lat/lng/precisión) al momento de firmar
--   - selfie tomada con cámara en vivo (nunca subida de galería) + su hash,
--     para detectar una foto reciclada (mismo patrón que ss_ats usa el Tareo
--     de Control de Acceso con foto_hash)
--   - firma + hash del contenido firmado, verificable desde el QR del PDF
--   - una vez Estado = 'Firmado' el registro es INMUTABLE: una corrección crea
--     un ATS nuevo con ats_anterior_id, nunca un UPDATE sobre uno firmado
--   - ss_ats_audit_log es INSERT-ONLY y encadena hash_anterior -> hash: si se
--     edita la tabla por fuera de la aplicación, la cadena deja de calzar
--
-- Idempotente: usa IF NOT EXISTS / ON CONFLICT DO NOTHING, se puede re-correr.
-- ============================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS ss_ats_peligro (
    id                       serial PRIMARY KEY,
    nombre                   text NOT NULL,
    medida_control_sugerida  text,
    orden                    smallint NOT NULL DEFAULT 0,
    activo                   boolean NOT NULL DEFAULT true,
    created_at               timestamp NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS ss_ats (
    id                     serial PRIMARY KEY,
    worker_id              int NOT NULL REFERENCES workers(id),
    proyecto_id            int NOT NULL REFERENCES project(project_id),
    actividad              text NOT NULL,
    lugar                  text,

    fecha                  date NOT NULL,
    hora_servidor_firma    timestamp,
    hora_dispositivo       timestamp,

    lat                    numeric(9,6),
    lng                    numeric(9,6),
    precision_metros       numeric(8,2),

    selfie_url             text,
    selfie_hash            text,

    firma_url              text,
    firma_hash             text,

    ip_origen              text,
    user_agent             text,

    estado                 varchar(20) NOT NULL DEFAULT 'Borrador'
                               CHECK (estado IN ('Borrador', 'Firmado')),
    ats_anterior_id        int REFERENCES ss_ats(id),

    pdf_url                text,
    pdf_hash               text,

    created_at             timestamp NOT NULL DEFAULT now(),
    updated_at             timestamp NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_ss_ats_worker_fecha ON ss_ats (worker_id, fecha);
CREATE INDEX IF NOT EXISTS ix_ss_ats_proyecto ON ss_ats (proyecto_id, fecha);
-- Selfie duplicada (reciclada / de galería): mismo principio que ac_tareo_registro.foto_hash.
CREATE INDEX IF NOT EXISTS ix_ss_ats_selfie_hash ON ss_ats (selfie_hash);

CREATE TABLE IF NOT EXISTS ss_ats_detalle (
    id             serial PRIMARY KEY,
    ats_id         int NOT NULL REFERENCES ss_ats(id),
    peligro_id     int NOT NULL REFERENCES ss_ats_peligro(id),
    medida_control text NOT NULL,
    orden          smallint NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS ix_ss_ats_detalle_ats ON ss_ats_detalle (ats_id);

CREATE TABLE IF NOT EXISTS ss_ats_audit_log (
    id             bigserial PRIMARY KEY,
    ats_id         int NOT NULL REFERENCES ss_ats(id),
    evento         varchar(30) NOT NULL,
    user_id        int REFERENCES app_user(user_id),
    ip_origen      text,
    detalle        text,
    created_at     timestamp NOT NULL DEFAULT now(),
    hash_anterior  text,
    hash           text NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_ss_ats_audit_log_ats ON ss_ats_audit_log (ats_id, created_at);

CREATE TABLE IF NOT EXISTS ss_ats_consentimiento (
    id             serial PRIMARY KEY,
    worker_id      int NOT NULL REFERENCES workers(id),
    aceptado_en    timestamp NOT NULL DEFAULT now(),
    ip_origen      text,
    version_texto  varchar(10) NOT NULL DEFAULT 'v1'
);

CREATE INDEX IF NOT EXISTS ix_ss_ats_consentimiento_worker ON ss_ats_consentimiento (worker_id);

-- ── Catálogo de peligros (SSO-FO-018.m "ATS SUPERVISIÓN") ───────────────────
INSERT INTO ss_ats_peligro (nombre, orden)
SELECT * FROM (VALUES
    ('Tropiezos y caídas', 1),
    ('Brillo de pantalla', 2),
    ('Habilitado y vaciado de zapatas', 3),
    ('Instalación de escuadras para plataformas voladizas', 4),
    ('Encofrado y desencofrado de losas o pisos', 5),
    ('Encofrado y desencofrado de vigas', 6),
    ('Encofrado y desencofrado de escaleras', 7),
    ('Encofrado y desencofrado de ductos', 8),
    ('Desencofrado verticales y curado', 9),
    ('Desencofrado horizontales', 10),
    ('Ruido', 11)
) AS v(nombre, orden)
WHERE NOT EXISTS (SELECT 1 FROM ss_ats_peligro p WHERE p.nombre = v.nombre);

COMMIT;

-- ============================================================================
-- Verificación (correr después; no modifica nada)
-- ============================================================================
-- SELECT table_name FROM information_schema.tables
-- WHERE table_name IN ('ss_ats','ss_ats_peligro','ss_ats_detalle','ss_ats_audit_log','ss_ats_consentimiento');
-- Esperado: las 5 filas.
--
-- SELECT count(*) FROM ss_ats_peligro;
-- Esperado: 11.
