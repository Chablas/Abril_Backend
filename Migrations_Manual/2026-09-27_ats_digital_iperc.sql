-- ============================================================================
-- ATS Digital — modelo IPERC completo (peligro→riesgo, riesgo base/residual,
-- EPP, herramientas, pasos por puesto, plantillas editables).
--
-- AUTOCONTENIDO: crea ss_ats, ss_ats_audit_log y ss_ats_consentimiento si no
-- existen (2026-09-26_ats_digital.sql nunca llegó a correr — "ss_ats" no
-- existía). No depende de ningún otro archivo.
--
-- El modelo plano viejo (ss_ats_peligro/ss_ats_detalle de una sola columna
-- "medida_control") nunca llegó a crearse, así que ya no hace falta ningún
-- DROP acá — de haberlo dejado, en una segunda corrida intentaría borrar la
-- tabla NUEVA (mismo nombre) y fallaría por sus propios dependientes
-- (ss_ats_riesgo, ss_ats_plantilla_peligro, ss_ats_riesgo_detalle).
--
-- Idempotente: se puede volver a correr sin romper nada (CREATE ... IF NOT
-- EXISTS / ON CONFLICT DO NOTHING en los seeds).
-- ============================================================================

BEGIN;

-- ── Cabecera (si no existía) ─────────────────────────────────────────────────

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
CREATE INDEX IF NOT EXISTS ix_ss_ats_selfie_hash ON ss_ats (selfie_hash);

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

-- ── Catálogos ────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS ss_ats_categoria_paso (
    id      serial PRIMARY KEY,
    nombre  text NOT NULL,
    orden   smallint NOT NULL DEFAULT 0,
    activo  boolean NOT NULL DEFAULT true
);

CREATE TABLE IF NOT EXISTS ss_ats_paso (
    id           serial PRIMARY KEY,
    categoria_id int NOT NULL REFERENCES ss_ats_categoria_paso(id),
    texto        text NOT NULL,
    orden        smallint NOT NULL DEFAULT 0,
    activo       boolean NOT NULL DEFAULT true
);

CREATE TABLE IF NOT EXISTS ss_ats_paso_puesto (
    id        serial PRIMARY KEY,
    paso_id   int NOT NULL REFERENCES ss_ats_paso(id),
    puesto_id int NOT NULL REFERENCES puesto(puesto_id),
    UNIQUE (paso_id, puesto_id)
);

CREATE TABLE IF NOT EXISTS ss_ats_peligro (
    id      serial PRIMARY KEY,
    nombre  text NOT NULL,
    orden   smallint NOT NULL DEFAULT 0,
    activo  boolean NOT NULL DEFAULT true
);

CREATE TABLE IF NOT EXISTS ss_ats_riesgo (
    id         serial PRIMARY KEY,
    peligro_id int NOT NULL REFERENCES ss_ats_peligro(id),
    nombre     text NOT NULL,
    orden      smallint NOT NULL DEFAULT 0,
    activo     boolean NOT NULL DEFAULT true
);

CREATE TABLE IF NOT EXISTS ss_ats_epp (
    id      serial PRIMARY KEY,
    nombre  text NOT NULL,
    orden   smallint NOT NULL DEFAULT 0,
    activo  boolean NOT NULL DEFAULT true
);

CREATE TABLE IF NOT EXISTS ss_ats_herramienta (
    id        serial PRIMARY KEY,
    nombre    text NOT NULL,
    categoria varchar(40) NOT NULL,
    orden     smallint NOT NULL DEFAULT 0,
    activo    boolean NOT NULL DEFAULT true
);

-- ── Plantillas ───────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS ss_ats_plantilla (
    id         serial PRIMARY KEY,
    nombre     text NOT NULL,
    puesto_id  int REFERENCES puesto(puesto_id),
    activo     boolean NOT NULL DEFAULT true,
    created_at timestamp NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS ss_ats_plantilla_peligro (
    id           serial PRIMARY KEY,
    plantilla_id int NOT NULL REFERENCES ss_ats_plantilla(id),
    peligro_id   int NOT NULL REFERENCES ss_ats_peligro(id),
    UNIQUE (plantilla_id, peligro_id)
);

CREATE TABLE IF NOT EXISTS ss_ats_plantilla_epp (
    id           serial PRIMARY KEY,
    plantilla_id int NOT NULL REFERENCES ss_ats_plantilla(id),
    epp_id       int NOT NULL REFERENCES ss_ats_epp(id),
    UNIQUE (plantilla_id, epp_id)
);

CREATE TABLE IF NOT EXISTS ss_ats_plantilla_herramienta (
    id            serial PRIMARY KEY,
    plantilla_id  int NOT NULL REFERENCES ss_ats_plantilla(id),
    herramienta_id int NOT NULL REFERENCES ss_ats_herramienta(id),
    UNIQUE (plantilla_id, herramienta_id)
);

-- ── ss_ats: columnas nuevas sobre la cabecera existente ─────────────────────

ALTER TABLE ss_ats ADD COLUMN IF NOT EXISTS puesto_id int REFERENCES puesto(puesto_id);
ALTER TABLE ss_ats ADD COLUMN IF NOT EXISTS plantilla_id int REFERENCES ss_ats_plantilla(id);

-- ── Detalle de la instancia (snapshot, inmutable tras firmar) ───────────────

CREATE TABLE IF NOT EXISTS ss_ats_paso_seleccionado (
    id               serial PRIMARY KEY,
    ats_id           int NOT NULL REFERENCES ss_ats(id),
    paso_id          int NOT NULL REFERENCES ss_ats_paso(id),
    categoria_nombre text NOT NULL,
    texto            text NOT NULL,
    aplica           boolean NOT NULL DEFAULT false,
    orden            smallint NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_ss_ats_paso_seleccionado_ats ON ss_ats_paso_seleccionado (ats_id);

CREATE TABLE IF NOT EXISTS ss_ats_epp_seleccionado (
    id      serial PRIMARY KEY,
    ats_id  int NOT NULL REFERENCES ss_ats(id),
    epp_id  int NOT NULL REFERENCES ss_ats_epp(id),
    nombre  text NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_ss_ats_epp_seleccionado_ats ON ss_ats_epp_seleccionado (ats_id);

CREATE TABLE IF NOT EXISTS ss_ats_herramienta_seleccionada (
    id             serial PRIMARY KEY,
    ats_id         int NOT NULL REFERENCES ss_ats(id),
    herramienta_id int NOT NULL REFERENCES ss_ats_herramienta(id),
    nombre         text NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_ss_ats_herramienta_seleccionada_ats ON ss_ats_herramienta_seleccionada (ats_id);

CREATE TABLE IF NOT EXISTS ss_ats_riesgo_detalle (
    id              serial PRIMARY KEY,
    ats_id          int NOT NULL REFERENCES ss_ats(id),
    peligro_id      int NOT NULL REFERENCES ss_ats_peligro(id),
    riesgo_id       int NOT NULL REFERENCES ss_ats_riesgo(id),
    peligro_nombre  text NOT NULL,
    riesgo_nombre   text NOT NULL,
    riesgo_base     varchar(1) NOT NULL CHECK (riesgo_base IN ('A', 'M', 'B')),
    controles       text NOT NULL,
    riesgo_residual varchar(1) NOT NULL CHECK (riesgo_residual IN ('A', 'M', 'B')),
    orden           smallint NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_ss_ats_riesgo_detalle_ats ON ss_ats_riesgo_detalle (ats_id);

COMMIT;

-- ============================================================================
-- Seed de catálogos (SSO-FO-018.m ATS SUPERVISIÓN) — idempotente
-- ============================================================================
BEGIN;

-- Categorías de pasos
INSERT INTO ss_ats_categoria_paso (nombre, orden)
SELECT * FROM (VALUES
    ('Trabajos de gabinete', 1),
    ('Liberación de Seguridad', 2),
    ('Liberación de Producción', 3),
    ('Liberación de Calidad', 4),
    ('Supervisión en campo', 5)
) AS v(nombre, orden)
WHERE NOT EXISTS (SELECT 1 FROM ss_ats_categoria_paso c WHERE c.nombre = v.nombre);

-- Pasos por categoría
INSERT INTO ss_ats_paso (categoria_id, texto, orden)
SELECT c.id, v.texto, v.orden
FROM (VALUES
    ('Trabajos de gabinete', 'Verificación de la programación de actividades', 1),
    ('Trabajos de gabinete', 'Recibir y responder correos', 2),
    ('Trabajos de gabinete', 'Recibir y atender llamadas telefónicas', 3),
    ('Trabajos de gabinete', 'Ordenar documentación competente', 4),
    ('Liberación de Seguridad', 'Desarrollar la charla de seguridad', 1),
    ('Liberación de Seguridad', 'Verificación de los EPPs del personal', 2),
    ('Liberación de Seguridad', 'Verificación de las herramientas manuales y eléctricas', 3),
    ('Liberación de Seguridad', 'Verificación de los documentos de seguridad (ATS, PETAR, CHECK LIST)', 4),
    ('Liberación de Seguridad', 'Llenado del ATS para salir a campo', 5),
    ('Liberación de Producción', 'Planificación de actividades a desarrollar con los contratistas', 1),
    ('Liberación de Producción', 'Realizar un programa de liberación de partidas del avance de acabados', 2),
    ('Liberación de Producción', 'Planificación de la llegada de materiales y de eliminación de desmonte', 3),
    ('Liberación de Producción', 'Verificación de documentación competente', 4),
    ('Liberación de Calidad', 'Liberación de calidad con andamio colgante', 1),
    ('Liberación de Calidad', 'Seguimiento a la planificación de actividades a desarrollar de los contratistas', 2),
    ('Supervisión en campo', 'Verificación de actos y condiciones inseguras en obra', 1),
    ('Supervisión en campo', 'Verificación del avance de las actividades planificadas', 2),
    ('Supervisión en campo', 'Verificación el orden y limpieza de la ruta de evacuación', 3)
) AS v(categoria_nombre, texto, orden)
JOIN ss_ats_categoria_paso c ON c.nombre = v.categoria_nombre
WHERE NOT EXISTS (SELECT 1 FROM ss_ats_paso p WHERE p.categoria_id = c.id AND p.texto = v.texto);

-- Peligros y sus riesgos asociados
INSERT INTO ss_ats_peligro (nombre, orden)
SELECT * FROM (VALUES
    ('Ruido', 1),
    ('Radiación solar', 2),
    ('Trabajo a distinto nivel (altura)', 3),
    ('Proyección de partículas (esquirlas de concreto)', 4),
    ('Manipulación de escaleras', 5),
    ('Disco en movimiento', 6),
    ('Superficies cortantes', 7),
    ('Superficies calientes', 8),
    ('Energía eléctrica', 9),
    ('Ductos y bordes de fachada', 10),
    ('Partículas suspendidas (polvo)', 11),
    ('Posturas forzadas', 12),
    ('Manipulación manual de cargas (traslado, levantamiento)', 13),
    ('Manipulación de herramientas manuales', 14),
    ('Partes giratorias', 15),
    ('Trabajos sobre cabeza', 16),
    ('Tránsito de personas por acceso de la obra y/o escaleras fijas', 17),
    ('Montaje de estructuras cerca, dentro o en el frontis de vecinos', 18),
    ('Cargas suspendidas', 19),
    ('Subir y bajar escaleras de obra', 20),
    ('Manipulación de materiales y/o herramientas en altura', 21),
    ('Maquinaria, equipos en movimiento (Volquetes, Excavadora, Minicargador, Camiones Mixer, faja transportadora, etc)', 22),
    ('Brillo de pantalla', 23)
) AS v(nombre, orden)
WHERE NOT EXISTS (SELECT 1 FROM ss_ats_peligro p WHERE p.nombre = v.nombre);

INSERT INTO ss_ats_riesgo (peligro_id, nombre, orden)
SELECT p.id, v.riesgo, v.orden
FROM (VALUES
    ('Ruido', 'Exposición al ruido', 1),
    ('Radiación solar', 'Exposición a rayos solares', 1),
    ('Trabajo a distinto nivel (altura)', 'Caída de personas a distinto nivel', 1),
    ('Trabajo a distinto nivel (altura)', 'Caída de objetos a distinto nivel', 2),
    ('Proyección de partículas (esquirlas de concreto)', 'Contacto con ojos', 1),
    ('Proyección de partículas (esquirlas de concreto)', 'Incendios/Explosiones', 2),
    ('Manipulación de escaleras', 'Atrición de dedos', 1),
    ('Manipulación de escaleras', 'Caída a distinto nivel', 2),
    ('Disco en movimiento', 'Contacto con superficie cortante', 1),
    ('Superficies cortantes', 'Contacto con superficie cortante', 1),
    ('Superficies calientes', 'Contacto con superficie caliente', 1),
    ('Energía eléctrica', 'Contacto directo e indirecto con E.E.', 1),
    ('Ductos y bordes de fachada', 'Caída de personas a distinto nivel', 1),
    ('Ductos y bordes de fachada', 'Caída de objetos, herramientas a distinto nivel', 2),
    ('Partículas suspendidas (polvo)', 'Inhalación de polvo', 1),
    ('Posturas forzadas', 'Sobreesfuerzos', 1),
    ('Manipulación manual de cargas (traslado, levantamiento)', 'Sobreesfuerzos', 1),
    ('Manipulación de herramientas manuales', 'Golpeado por', 1),
    ('Manipulación de herramientas manuales', 'Contacto con superficie cortante', 2),
    ('Partes giratorias', 'Atrapamiento', 1),
    ('Trabajos sobre cabeza', 'Proyección de materiales', 1),
    ('Tránsito de personas por acceso de la obra y/o escaleras fijas', 'Caídas a distinto o mismo nivel', 1),
    ('Montaje de estructuras cerca, dentro o en el frontis de vecinos', 'Choques, impacto, atropellos', 1),
    ('Cargas suspendidas', 'Aplastamiento', 1),
    ('Subir y bajar escaleras de obra', 'Tropiezos y caídas', 1),
    ('Manipulación de materiales y/o herramientas en altura', 'Caída de herramientas y/o materiales a distinto nivel', 1),
    ('Maquinaria, equipos en movimiento (Volquetes, Excavadora, Minicargador, Camiones Mixer, faja transportadora, etc)', 'Atrapamiento, aplastamiento', 1),
    ('Brillo de pantalla', 'Afectación visual', 1)
) AS v(peligro_nombre, riesgo, orden)
JOIN ss_ats_peligro p ON p.nombre = v.peligro_nombre
WHERE NOT EXISTS (SELECT 1 FROM ss_ats_riesgo r WHERE r.peligro_id = p.id AND r.nombre = v.riesgo);

-- EPP
INSERT INTO ss_ats_epp (nombre, orden)
SELECT * FROM (VALUES
    ('Casco', 1), ('Barbiquejo', 2), ('Tapones auditivos', 3), ('Orejeras', 4),
    ('Lentes de seguridad', 5), ('Sobrelentes', 6),
    ('Respirador doble vía', 7), ('Filtro contra polvo', 8), ('Cartuchos contra vapores', 9),
    ('Bloqueador solar', 10), ('Careta facial', 11), ('Careta de soldar con mica filtrante', 12),
    ('Guantes anticorte', 13), ('Guantes tipo hyflex', 14), ('Guantes de nitrilo', 15),
    ('Guantes de cuero', 16), ('Guantes de jebe', 17), ('Mandil de cuero, escarpines', 18),
    ('Zapatos c/punta baquelita', 19), ('Zapatos c/punta acero', 20), ('Botas de jebe c/punta de acero', 21),
    ('Arnés y línea de anclaje doble', 22), ('Tambor retráctil', 23),
    ('Línea de posicionamiento o restricción', 24)
) AS v(nombre, orden)
WHERE NOT EXISTS (SELECT 1 FROM ss_ats_epp e WHERE e.nombre = v.nombre);

-- Herramientas y equipos
INSERT INTO ss_ats_herramienta (nombre, categoria, orden)
SELECT * FROM (VALUES
    ('Martillo', 'Herramientas manuales', 1), ('Picos', 'Herramientas manuales', 2),
    ('Mazo', 'Herramientas manuales', 3), ('Combas', 'Herramientas manuales', 4),
    ('Palas', 'Herramientas manuales', 5), ('Destornilladores', 'Herramientas manuales', 6),
    ('Espátulas', 'Herramientas manuales', 7), ('Punzones', 'Herramientas manuales', 8),
    ('Llaves mixtas', 'Herramientas manuales', 9), ('Serruchos', 'Herramientas manuales', 10),
    ('Rastrillos', 'Herramientas manuales', 11), ('Tijera de corte', 'Herramientas manuales', 12),
    ('Tórtoles', 'Herramientas manuales', 13), ('Alicates', 'Herramientas manuales', 14),
    ('Carretillas (buggy)', 'Herramientas manuales', 15), ('Pata de cabra', 'Herramientas manuales', 16),
    ('Cuchillas', 'Herramientas manuales', 17), ('Nivel', 'Herramientas manuales', 18),
    ('Barretillas', 'Herramientas manuales', 19), ('Winchas', 'Herramientas manuales', 20),
    ('Cortadora de enchape', 'Herramientas manuales', 21), ('Cinceles', 'Herramientas manuales', 22),

    ('Esmeril angular', 'Equipos de poder', 1), ('Taladro eléctrico', 'Equipos de poder', 2),
    ('Taladro inalámbrico', 'Equipos de poder', 3), ('Sierra circular', 'Equipos de poder', 4),
    ('Rotomartillo', 'Equipos de poder', 5), ('Apisonador', 'Equipos de poder', 6),
    ('Vibrador de concreto', 'Equipos de poder', 7),
    ('Cortador tecnopor (soplete c/balón de gas)', 'Equipos de poder', 8),
    ('Multímetro', 'Equipos de poder', 9), ('Pinza amperimétrica', 'Equipos de poder', 10),
    ('Equipo oxicorte', 'Equipos de poder', 11), ('Batidora', 'Equipos de poder', 12),
    ('Tronzadora', 'Equipos de poder', 13),

    ('Andamios', 'Varios', 1), ('Escalera tijera', 'Varios', 2),
    ('Escalera telescópica', 'Varios', 3), ('Escalera lineal', 'Varios', 4),
    ('Escalera de pasos', 'Varios', 5), ('Plataforma voladiza', 'Varios', 6),
    ('Andamio colgante', 'Varios', 7), ('Silla colgante', 'Varios', 8)
) AS v(nombre, categoria, orden)
WHERE NOT EXISTS (SELECT 1 FROM ss_ats_herramienta h WHERE h.nombre = v.nombre);

COMMIT;

-- ============================================================================
-- Verificación (correr después; no modifica nada)
-- ============================================================================
-- SELECT count(*) FROM ss_ats_categoria_paso;  -- 5
-- SELECT count(*) FROM ss_ats_paso;             -- 18
-- SELECT count(*) FROM ss_ats_peligro;          -- 23
-- SELECT count(*) FROM ss_ats_riesgo;           -- 28
-- SELECT count(*) FROM ss_ats_epp;              -- 24
-- SELECT count(*) FROM ss_ats_herramienta;      -- 43
