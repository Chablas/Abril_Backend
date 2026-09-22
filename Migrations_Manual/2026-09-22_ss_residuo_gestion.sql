-- Módulo de Gestión de Residuos de Obra (SSOMA, /ssoma/gestion/residuos).
-- Cubre: registro de viajes/retiros de residuos por obra, catálogos de tipo de
-- residuo (con factor de conversión m3->t) y de EO-RS/escombreras con su
-- checklist documentario, autorizaciones DME por obra, declaración anual
-- (DAMRS/SIGERSOL) por razón social (contributor) con desglose mensual y por
-- tipo de manejo, constancias mensuales de disposición/eliminación por
-- contratista y destino, y repositorio de plantillas/manuales de referencia
-- (manuales SIGERSOL, modelo de declaración jurada, modelo de características
-- de residuos sólidos).
--
-- Nota de diseño: el "Consolidado de Servicios" y el resumen por EO-RS que hoy
-- se llevan a mano en Excel (GP-FOR-036 y la hoja resumen por RUC) NO se
-- replican como tablas de captura: se calculan por consulta sobre
-- ss_residuo_viaje, para eliminar la doble digitación que hoy produce
-- descuadres entre hojas del Excel.

-- Catálogo: tipos de residuo (codificados según SIGERSOL/MINAM) con factor de
-- conversión m3 -> t (ej. material de construcción/excavación = 1.8,
-- demolición = 2.3, vigente por rango de fechas).
CREATE TABLE ss_residuo_tipo (
    id                  serial PRIMARY KEY,
    codigo_sigersol     varchar(50),
    nombre              varchar(150) NOT NULL,
    es_peligroso        boolean NOT NULL DEFAULT false,
    activo              boolean NOT NULL DEFAULT true,
    creado_en           timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE ss_residuo_tipo_factor (
    id                  serial PRIMARY KEY,
    residuo_tipo_id     int NOT NULL REFERENCES ss_residuo_tipo(id),
    factor_m3_a_ton     numeric(6,3) NOT NULL,
    vigencia_desde      date NOT NULL,
    vigencia_hasta      date,
    creado_en           timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_residuo_tipo_factor_tipo ON ss_residuo_tipo_factor (residuo_tipo_id);

-- Catálogo: EO-RS / transportistas / escombreras / plantas de valorización.
CREATE TABLE ss_residuo_eo_rs (
    id                  serial PRIMARY KEY,
    ruc                 varchar(11) NOT NULL,
    razon_social        varchar(255) NOT NULL,
    tipo_operador       varchar(30) NOT NULL, -- TRANSPORTISTA | DISPOSICION_FINAL | VALORIZACION | COMERCIALIZADORA
    numero_registro_minam varchar(100),
    vigencia_registro   date,
    direccion           varchar(255),
    ambito_gestion      varchar(20), -- MUNICIPAL | NO_MUNICIPAL
    activo              boolean NOT NULL DEFAULT true,
    creado_en           timestamptz NOT NULL DEFAULT now(),
    actualizado_en      timestamptz
);

CREATE UNIQUE INDEX ux_residuo_eo_rs_ruc ON ss_residuo_eo_rs (ruc) WHERE activo = true;

-- Checklist documentario por EO-RS (SOAT, tarjeta de circulación, licencia,
-- registro EO-RS-MINAM, permiso MTC para peligrosos, etc.) con vigencia.
CREATE TABLE ss_residuo_eo_rs_documento (
    id                  serial PRIMARY KEY,
    eo_rs_id            int NOT NULL REFERENCES ss_residuo_eo_rs(id),
    tipo_documento      varchar(50) NOT NULL, -- SOAT | TARJETA_CIRCULACION | LICENCIA_CONDUCIR | REGISTRO_EO_RS | PERMISO_MTC_PELIGROSOS | OTRO
    numero              varchar(100),
    vigencia_desde      date,
    vigencia_hasta      date,
    archivo_url         varchar(500),
    cumple              boolean NOT NULL DEFAULT false,
    creado_en           timestamptz NOT NULL DEFAULT now(),
    actualizado_en      timestamptz
);

CREATE INDEX ix_residuo_eo_rs_documento_eo_rs ON ss_residuo_eo_rs_documento (eo_rs_id);

-- Autorización de Disposición de Material Excedente (DME) por obra.
CREATE TABLE ss_residuo_autorizacion_dme (
    id                  serial PRIMARY KEY,
    project_id          int NOT NULL REFERENCES project(project_id),
    municipalidad       varchar(150) NOT NULL,
    numero_resolucion   varchar(100) NOT NULL,
    escombrera_destino_id int REFERENCES ss_residuo_eo_rs(id),
    vigencia_desde      date NOT NULL,
    vigencia_hasta      date NOT NULL,
    placas_autorizadas  varchar(500),
    estado              varchar(20) NOT NULL DEFAULT 'VIGENTE', -- VIGENTE | VENCIDA | ANULADA
    archivo_url         varchar(500),
    creado_en           timestamptz NOT NULL DEFAULT now(),
    actualizado_en      timestamptz
);

CREATE INDEX ix_residuo_autorizacion_dme_project ON ss_residuo_autorizacion_dme (project_id);

-- Registro operativo por viaje/retiro (fuente única de verdad, GP-FOR-035).
CREATE TABLE ss_residuo_viaje (
    id                  bigserial PRIMARY KEY,
    project_id          int NOT NULL REFERENCES project(project_id),
    autorizacion_dme_id int REFERENCES ss_residuo_autorizacion_dme(id),
    residuo_tipo_id     int NOT NULL REFERENCES ss_residuo_tipo(id),
    eo_rs_id            int NOT NULL REFERENCES ss_residuo_eo_rs(id),
    contratista         varchar(255),
    origen              varchar(100), -- DEMOLICION | EXCAVACION | CONSTRUCCION | OTRO
    fecha               date NOT NULL,
    cantidad_m3         numeric(12,2) NOT NULL,
    cantidad_ton        numeric(12,3), -- calculado desde ss_residuo_tipo_factor, editable ante ajuste manual
    tipo_manejo         varchar(20) NOT NULL, -- ALMACENADO | TRATADO | ACONDICIONADO | VALORIZADO | COMERCIALIZADO | DISPOSICION_FINAL
    gestor_receptor     varchar(255),
    destino_final       varchar(500),
    numero_registro     varchar(150),
    numero_certificado  varchar(150),
    archivo_url         varchar(500),
    activo              boolean NOT NULL DEFAULT true,
    creado_en           timestamptz NOT NULL DEFAULT now(),
    creado_por          int NOT NULL,
    actualizado_en      timestamptz
);

CREATE INDEX ix_residuo_viaje_project_fecha ON ss_residuo_viaje (project_id, fecha);
CREATE INDEX ix_residuo_viaje_eo_rs ON ss_residuo_viaje (eo_rs_id);
CREATE INDEX ix_residuo_viaje_tipo ON ss_residuo_viaje (residuo_tipo_id);

-- Declaración anual (DAMRS/SIGERSOL) por razón social (contributor).
CREATE TABLE ss_residuo_declaracion (
    id                  serial PRIMARY KEY,
    contributor_id      int NOT NULL REFERENCES contributor(contributor_id),
    periodo_anio        int NOT NULL,
    estado              varchar(20) NOT NULL DEFAULT 'BORRADOR', -- BORRADOR | PRESENTADA
    fecha_presentacion  date,
    archivo_constancia_url varchar(500),
    creado_en           timestamptz NOT NULL DEFAULT now(),
    actualizado_en      timestamptz
);

CREATE UNIQUE INDEX ux_residuo_declaracion_contributor_periodo
    ON ss_residuo_declaracion (contributor_id, periodo_anio);

-- Detalle por tipo de residuo: cantidad acumulada del periodo anterior +
-- generación mensual (ene-dic), calculado desde ss_residuo_viaje y editable
-- antes de marcar la declaración como presentada.
CREATE TABLE ss_residuo_declaracion_detalle (
    id                  serial PRIMARY KEY,
    declaracion_id      int NOT NULL REFERENCES ss_residuo_declaracion(id),
    residuo_tipo_id     int NOT NULL REFERENCES ss_residuo_tipo(id),
    cantidad_acumulada_anterior numeric(12,3) NOT NULL DEFAULT 0,
    ene numeric(12,3) NOT NULL DEFAULT 0,
    feb numeric(12,3) NOT NULL DEFAULT 0,
    mar numeric(12,3) NOT NULL DEFAULT 0,
    abr numeric(12,3) NOT NULL DEFAULT 0,
    may numeric(12,3) NOT NULL DEFAULT 0,
    jun numeric(12,3) NOT NULL DEFAULT 0,
    jul numeric(12,3) NOT NULL DEFAULT 0,
    ago numeric(12,3) NOT NULL DEFAULT 0,
    sep numeric(12,3) NOT NULL DEFAULT 0,
    oct numeric(12,3) NOT NULL DEFAULT 0,
    nov numeric(12,3) NOT NULL DEFAULT 0,
    dic numeric(12,3) NOT NULL DEFAULT 0,
    -- desglose por tipo de manejo (debe cuadrar contra el total generado)
    almacenado          numeric(12,3) NOT NULL DEFAULT 0,
    tratado             numeric(12,3) NOT NULL DEFAULT 0,
    acondicionado       numeric(12,3) NOT NULL DEFAULT 0,
    valorizado          numeric(12,3) NOT NULL DEFAULT 0,
    comercializado       numeric(12,3) NOT NULL DEFAULT 0,
    disposicion_final   numeric(12,3) NOT NULL DEFAULT 0,
    creado_en           timestamptz NOT NULL DEFAULT now(),
    actualizado_en      timestamptz
);

CREATE UNIQUE INDEX ux_residuo_declaracion_detalle_tipo
    ON ss_residuo_declaracion_detalle (declaracion_id, residuo_tipo_id);

-- EO-RS intervinientes declarados por cada declaración (recolección/
-- transporte, tratamiento, valorización), con su N° de servicios y total
-- transportado al año, tal como lo pide el formulario de SIGERSOL.
CREATE TABLE ss_residuo_declaracion_eo_rs (
    id                  serial PRIMARY KEY,
    declaracion_id      int NOT NULL REFERENCES ss_residuo_declaracion(id),
    eo_rs_id            int NOT NULL REFERENCES ss_residuo_eo_rs(id),
    etapa               varchar(30) NOT NULL, -- RECOLECCION_TRANSPORTE | TRATAMIENTO | VALORIZACION
    numero_servicios_anio int NOT NULL DEFAULT 0,
    total_residuo_ton   numeric(12,3) NOT NULL DEFAULT 0,
    creado_en           timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_residuo_declaracion_eo_rs_declaracion ON ss_residuo_declaracion_eo_rs (declaracion_id);

-- Constancias de disposición final / eliminación, periodicidad mensual, por
-- contratista y por destino/lugar de disposición.
CREATE TABLE ss_residuo_constancia (
    id                  serial PRIMARY KEY,
    project_id          int NOT NULL REFERENCES project(project_id),
    eo_rs_id            int REFERENCES ss_residuo_eo_rs(id),
    contratista         varchar(255),
    destino             varchar(500),
    periodo_anio        int NOT NULL,
    periodo_mes         int NOT NULL, -- 1-12
    numero_certificado  varchar(150),
    archivo_url         varchar(500) NOT NULL,
    creado_en           timestamptz NOT NULL DEFAULT now(),
    creado_por          int NOT NULL
);

CREATE INDEX ix_residuo_constancia_project_periodo ON ss_residuo_constancia (project_id, periodo_anio, periodo_mes);

-- Constancia final de obra (cierre de proyecto o de periodo declarado).
CREATE TABLE ss_residuo_constancia_final (
    id                  serial PRIMARY KEY,
    project_id          int NOT NULL REFERENCES project(project_id),
    fecha_emision       date NOT NULL,
    archivo_url         varchar(500) NOT NULL,
    observaciones       varchar(500),
    creado_en           timestamptz NOT NULL DEFAULT now(),
    creado_por          int NOT NULL
);

CREATE INDEX ix_residuo_constancia_final_project ON ss_residuo_constancia_final (project_id);

-- Repositorio de plantillas/manuales de referencia (manuales SIGERSOL, modelo
-- de declaración jurada, modelo de características de residuos sólidos), con
-- versión y CRUD simple.
CREATE TABLE ss_residuo_documento_referencia (
    id                  serial PRIMARY KEY,
    tipo                varchar(50) NOT NULL, -- MANUAL_SIGERSOL | MODELO_DECLARACION_JURADA | MODELO_CARACTERISTICAS_RESIDUOS | OTRO
    nombre              varchar(255) NOT NULL,
    descripcion         varchar(500),
    archivo_url         varchar(500) NOT NULL,
    version             varchar(20),
    activo              boolean NOT NULL DEFAULT true,
    creado_en           timestamptz NOT NULL DEFAULT now(),
    creado_por          int NOT NULL,
    actualizado_en      timestamptz
);

CREATE INDEX ix_residuo_documento_referencia_tipo ON ss_residuo_documento_referencia (tipo, activo);
