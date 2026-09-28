-- ============================================================================
-- PETAR — Permiso Escrito de Trabajo de Alto Riesgo, enlazado obligatoriamente
-- al ATS que lo origina (nunca un documento suelto). Exige 3 firmas: ejecutante
-- (selfie+geo, igual que el ATS), Supervisor/Responsable del trabajo, y SSOMA
-- — esta última obligatoria siempre, sin excepción.
--
-- Idempotente (CREATE ... IF NOT EXISTS + seed con WHERE NOT EXISTS).
-- ============================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS ss_petar_tipo (
    id      serial PRIMARY KEY,
    nombre  text NOT NULL,
    orden   smallint NOT NULL DEFAULT 0,
    activo  boolean NOT NULL DEFAULT true
);

CREATE TABLE IF NOT EXISTS ss_petar_item (
    id      serial PRIMARY KEY,
    tipo_id int NOT NULL REFERENCES ss_petar_tipo(id),
    texto   text NOT NULL,
    orden   smallint NOT NULL DEFAULT 0,
    activo  boolean NOT NULL DEFAULT true
);

CREATE TABLE IF NOT EXISTS ss_petar (
    id                       serial PRIMARY KEY,
    ats_id                   int NOT NULL REFERENCES ss_ats(id),
    tipo_id                  int NOT NULL REFERENCES ss_petar_tipo(id),
    worker_id                int NOT NULL REFERENCES workers(id),
    proyecto_id              int NOT NULL REFERENCES project(project_id),

    descripcion_trabajo      text NOT NULL,
    lugar                    text,
    fecha                    date NOT NULL,
    hora_inicio              time,
    hora_fin                 time,

    hora_servidor_firma      timestamp,
    hora_dispositivo         timestamp,
    lat                      numeric(9,6),
    lng                      numeric(9,6),
    precision_metros         numeric(8,2),
    selfie_url               text,
    selfie_hash              text,
    firma_url                text,
    firma_hash               text,
    ip_origen                text,
    user_agent               text,

    supervisor_worker_id     int REFERENCES workers(id),
    supervisor_nombre        text,
    supervisor_cargo         text,
    supervisor_firma_url     text,
    supervisor_firma_hash    text,
    supervisor_hora_servidor timestamp,

    ssoma_worker_id          int REFERENCES workers(id),
    ssoma_nombre             text,
    ssoma_cargo              text,
    ssoma_firma_url          text,
    ssoma_firma_hash         text,
    ssoma_hora_servidor      timestamp,

    estado                   varchar(20) NOT NULL DEFAULT 'Borrador'
                                 CHECK (estado IN ('Borrador', 'Firmado', 'Cerrado')),

    cierre_hora_servidor     timestamp,
    cierre_observaciones     text,
    cierre_firma_url         text,
    cierre_firma_hash        text,

    pdf_url                  text,
    pdf_hash                 text,

    created_at               timestamp NOT NULL DEFAULT now(),
    updated_at               timestamp NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_ss_petar_ats ON ss_petar (ats_id);
CREATE INDEX IF NOT EXISTS ix_ss_petar_worker_fecha ON ss_petar (worker_id, fecha);
CREATE INDEX IF NOT EXISTS ix_ss_petar_proyecto ON ss_petar (proyecto_id, fecha);
CREATE INDEX IF NOT EXISTS ix_ss_petar_selfie_hash ON ss_petar (selfie_hash);

CREATE TABLE IF NOT EXISTS ss_petar_item_respuesta (
    id         serial PRIMARY KEY,
    petar_id   int NOT NULL REFERENCES ss_petar(id),
    item_id    int NOT NULL REFERENCES ss_petar_item(id),
    texto      text NOT NULL,
    respuesta  varchar(2) NOT NULL CHECK (respuesta IN ('SI', 'NO', 'NA')),
    orden      smallint NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_ss_petar_item_respuesta_petar ON ss_petar_item_respuesta (petar_id);

CREATE TABLE IF NOT EXISTS ss_petar_audit_log (
    id             bigserial PRIMARY KEY,
    petar_id       int NOT NULL REFERENCES ss_petar(id),
    evento         varchar(30) NOT NULL,
    user_id        int REFERENCES app_user(user_id),
    ip_origen      text,
    detalle        text,
    created_at     timestamp NOT NULL DEFAULT now(),
    hash_anterior  text,
    hash           text NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_ss_petar_audit_log_petar ON ss_petar_audit_log (petar_id, created_at);

COMMIT;

-- ============================================================================
-- Seed de tipos y checklist — idempotente
-- NOTA: este contenido es genérico (redactado por IA, sin Excel de respaldo).
-- Corre DESPUÉS este archivo Y LUEGO 2026-10-02_petar_checklists_reales.sql,
-- que reemplaza estos 6 tipos con el checklist real de los formatos SSO-FO-039/
-- 040/041/042/043/125 y agrega el tipo 7 (SSO-FO-153, izaje no convencional).
-- ============================================================================
BEGIN;

INSERT INTO ss_petar_tipo (nombre, orden)
SELECT * FROM (VALUES
    ('Trabajo en altura', 1),
    ('Espacios confinados', 2),
    ('Trabajo en caliente', 3),
    ('Izaje de cargas', 4),
    ('Excavaciones', 5),
    ('Trabajos eléctricos (bloqueo y etiquetado)', 6)
) AS v(nombre, orden)
WHERE NOT EXISTS (SELECT 1 FROM ss_petar_tipo t WHERE t.nombre = v.nombre);

INSERT INTO ss_petar_item (tipo_id, texto, orden)
SELECT t.id, v.texto, v.orden
FROM (VALUES
    ('Trabajo en altura', 'Arnés de cuerpo entero en buen estado, con etiqueta de inspección vigente', 1),
    ('Trabajo en altura', 'Línea de anclaje / línea de vida instalada y anclada a punto certificado', 2),
    ('Trabajo en altura', 'Doble línea de enganche (100% amarre) verificada', 3),
    ('Trabajo en altura', 'Área señalizada y delimitada debajo de la zona de trabajo', 4),
    ('Trabajo en altura', 'Condiciones climáticas verificadas (sin viento fuerte, lluvia o tormenta eléctrica)', 5),
    ('Trabajo en altura', 'Plan de rescate en altura conocido por la cuadrilla', 6),

    ('Espacios confinados', 'Medición de gases realizada y registrada (O2, LEL, CO, H2S) antes de ingresar', 1),
    ('Espacios confinados', 'Ventilación forzada instalada y funcionando', 2),
    ('Espacios confinados', 'Vigía permanente asignado fuera del espacio confinado', 3),
    ('Espacios confinados', 'Equipo de rescate y comunicación disponible en el punto', 4),
    ('Espacios confinados', 'Bloqueo/aislamiento de energías peligrosas hacia el espacio confinado', 5),

    ('Trabajo en caliente', 'Área libre de materiales combustibles en un radio de 10 metros', 1),
    ('Trabajo en caliente', 'Extintor de incendios disponible en el punto de trabajo', 2),
    ('Trabajo en caliente', 'Vigía de fuego asignado durante y después del trabajo', 3),
    ('Trabajo en caliente', 'Pantallas/mantas ignífugas instaladas si aplica', 4),
    ('Trabajo en caliente', 'Equipos de soldadura/corte inspeccionados (mangueras, válvulas, conexiones)', 5),

    ('Izaje de cargas', 'Equipo de izaje inspeccionado y con certificado vigente', 1),
    ('Izaje de cargas', 'Operador certificado y rigger designado', 2),
    ('Izaje de cargas', 'Peso de la carga conocido y dentro de la capacidad del equipo', 3),
    ('Izaje de cargas', 'Radio de giro señalizado y área despejada de personal', 4),
    ('Izaje de cargas', 'Eslingas/grilletes/ganchos inspeccionados, sin daños visibles', 5),

    ('Excavaciones', 'Estudio de suelos / clasificación del terreno disponible', 1),
    ('Excavaciones', 'Entibado o talud según profundidad y tipo de suelo', 2),
    ('Excavaciones', 'Verificación de interferencias (redes eléctricas, agua, gas) antes de excavar', 3),
    ('Excavaciones', 'Acceso/salida segura (escalera o rampa) cada 8 metros o menos', 4),
    ('Excavaciones', 'Señalización y barrera perimetral instalada', 5),

    ('Trabajos eléctricos (bloqueo y etiquetado)', 'Circuito identificado y diagrama unifilar verificado', 1),
    ('Trabajos eléctricos (bloqueo y etiquetado)', 'Bloqueo (candado) y etiquetado (tarjeta) instalados por el ejecutante', 2),
    ('Trabajos eléctricos (bloqueo y etiquetado)', 'Ausencia de tensión verificada con multímetro/detector antes de tocar', 3),
    ('Trabajos eléctricos (bloqueo y etiquetado)', 'EPP dieléctrico verificado (guantes, herramientas aisladas)', 4),
    ('Trabajos eléctricos (bloqueo y etiquetado)', 'Puesta a tierra temporal instalada si aplica', 5)
) AS v(tipo_nombre, texto, orden)
JOIN ss_petar_tipo t ON t.nombre = v.tipo_nombre
WHERE NOT EXISTS (SELECT 1 FROM ss_petar_item i WHERE i.tipo_id = t.id AND i.texto = v.texto);

COMMIT;

-- ============================================================================
-- Verificación (correr después; no modifica nada)
-- ============================================================================
-- SELECT count(*) FROM ss_petar_tipo;  -- 6
-- SELECT count(*) FROM ss_petar_item;  -- 31
