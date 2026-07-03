-- ============================================================================
-- Seed plantillas de Checklist SSOMA + ítems REALES (InicioProyecto.csv)
-- Ejecutar en PRODUCCIÓN.
-- NOTA: Hace DELETE+INSERT — ejecutar sólo si las tablas están vacías o
--       si se desea reemplazar data placeholder previa.
-- ============================================================================
BEGIN;

-- ── Limpiar seed anterior (placeholder) ─────────────────────────────────────
DELETE FROM ss_checklist_plantilla_item;
DELETE FROM ss_checklist_plantilla;

-- ── PLANTILLAS ───────────────────────────────────────────────────────────────
INSERT INTO ss_checklist_plantilla (nombre, descripcion, tipo_activacion, evento_activacion, es_obligatorio, orden, activo, created_at, updated_at)
VALUES
  ('Inicio de Proyecto',   'Verificación de condiciones previas al inicio de obra',           'automatico', 'inicio_proyecto',   true,  1,  true, NOW(), NOW()),
  ('Inicio de Demolicion', 'Verificaciones antes de iniciar trabajos de demolición',           'automatico', 'inicio_demolicion', true,  2,  true, NOW(), NOW()),
  ('Torre Grua',           'Inspección y habilitación para montaje de Torre Grúa',             'manual',     'TORRE_GRUA',        false, 3,  true, NOW(), NOW()),
  ('PLACING BOOM',         'Verificación de condiciones para habilitación de Placing Boom',    'manual',     'PLACING_BOOM',      false, 4,  true, NOW(), NOW()),
  ('Grúa Móvil',           'Documentación y condiciones para operación de Grúa Móvil',        'manual',     'GRUA_MOVIL',        false, 5,  true, NOW(), NOW()),
  ('SUNAFIL',              'Documentación y condiciones para fiscalización SUNAFIL',           'manual',     NULL,                false, 6,  true, NOW(), NOW()),
  ('ITSE',                 'Inspección Técnica de Seguridad en Edificaciones (obra)',          'manual',     NULL,                false, 7,  true, NOW(), NOW()),
  ('ITSE OFICINA CENTRAL', 'Inspección Técnica de Seguridad en Edificaciones - oficina',      'manual',     NULL,                false, 8,  true, NOW(), NOW()),
  ('Municipalidad',        'Documentación y condiciones para fiscalización municipal',         'manual',     NULL,                false, 9,  true, NOW(), NOW()),
  ('Habilitacion de Sala de Ventas', 'Checklist de habilitación de sala de ventas',           'manual',     NULL,                false, 10, true, NOW(), NOW()),
  ('Señales de seguridad (Planos aprobados)', 'Verificaciones para instalación de señalética según planos aprobados', 'manual', NULL, false, 11, true, NOW(), NOW()),
  ('ESTUDIO DE SUELO(CALICATA)', 'Documentación para ejecución de estudio de suelo / calicata', 'manual',   NULL,                false, 12, true, NOW(), NOW()),
  ('Seguridad de Información de SO', 'Controles de seguridad de información en Salud Ocupacional', 'manual', NULL,              false, 13, true, NOW(), NOW()),
  ('Estación de Primeros Auxilios', 'Insumos y equipos requeridos en la estación de primeros auxilios', 'manual', NULL,         false, 14, true, NOW(), NOW()),
  ('AUDITORÍA MINTRA',     'Documentación para auditoría MINTRA Ley 29783',                   'manual',     NULL,                false, 15, true, NOW(), NOW()),
  ('MONITOREO DE MEDIO AMBIENTE', 'Parámetros de monitoreo ambiental requeridos',             'manual',     NULL,                false, 16, true, NOW(), NOW()),
  ('MONITOREO OCUPACIONAL', 'Parámetros de monitoreo ocupacional requeridos',                 'manual',     NULL,                false, 17, true, NOW(), NOW()),
  ('Declaración de RS en la Plataforma SINGERSOL', 'Pasos para declarar RS en SINGERSOL',    'manual',     NULL,                false, 18, true, NOW(), NOW());

-- ── ÍTEMS — Inicio de Proyecto ───────────────────────────────────────────────
INSERT INTO ss_checklist_plantilla_item (plantilla_id, descripcion, tiene_adjunto_ref, orden, activo, created_at, updated_at)
SELECT p.id, v.dsc, v.adj, v.ord, true, NOW(), NOW()
FROM ss_checklist_plantilla p
CROSS JOIN (VALUES
  ('Revisar La normativa local, ordenanzas municipales, del distrito donde está ubicado el proyecto', true, 1),
  ('Verificar si se cuenta con licencia de obra, permiso de interferencia de vías para carga de descarga y uso de media vereda para el cerco perimétrico', true, 2),
  ('Solicitar el letrero informativo de obra de acuerdo a los datos de la licencia', true, 3),
  ('Solicitar servicios higiénicos con lavatorio y duchas portátiles', false, 4),
  ('Coordinar el ingreso de personal pdr, vigía y monitor de obra', false, 5),
  ('Realizar el Plan de Seguridad y Salud en el Trabajo y elementos', false, 6),
  ('Solicitar la inscripción en RENOCC de la obra', false, 7),
  ('Coordinar el ingreso de las concesionarias de alimentos', false, 8),
  ('Implementar el panel informativo de obra', false, 9),
  ('Implementar la estación de emergencia', false, 10),
  ('Implementar los cilindros de colores para los residuos', false, 11),
  ('Realizar el Plan de Respuesta ante Emergencias', false, 12),
  ('Realizar Plan para la Vigilancia, Prevención y Control Covid-19 de los trabajadores', false, 13),
  ('Coordinar la Implementación del comedor', false, 14),
  ('Coordinar la implementación de los vestuarios', false, 15),
  ('Realizar y publicar el mapa de riesgo', false, 16),
  ('Realizar la implementación del Comité de Seguridad y Salud en el Trabajo', false, 17),
  ('Realizar Inducción SSOMA al personal nuevo que ingresa a obra', false, 18),
  ('Validar la documentación de subcontratista nuevo que ingresa a obra', false, 19),
  ('Pegar el letrero de licencia de obra en el frontis', false, 20),
  ('Elaborar y publicar el IPERC de LineaBase', false, 21),
  ('Verificar las protecciones colectivas.', false, 22),
  ('Verificar los puntales según necesidad y realizar el pedido con tiempo.', false, 23),
  ('Solicitar insumos para señalización y delimitación de áreas,', false, 24),
  ('Plano de protecciones colectivas.', false, 25),
  ('Conformación de brigadas de emergencia', false, 26),
  ('Publicación de política SST', false, 27),
  ('Realizar y publicación plano de evacuación', false, 28),
  ('Enviar requerimiento de extintores con sus bases.', false, 29),
  ('Realizar implementación de chalecos de visitantes y cascos.', false, 30),
  ('Definir puntos de lavados de manos.', false, 31),
  ('Definir punto de hidratación', false, 32),
  ('Definir donde se ubicaran los cilindros para gestión de residuos solidos', false, 33),
  ('Implementación de almacén MATPEL', false, 34),
  ('Plan de Medio Ambiente', false, 35),
  ('Implementar SERVICIOS DEL AREA BIENESTAR', false, 36),
  ('Pozo a tierra en cada predio que se tenga licencia.', false, 37),
  ('Tablero electrico provisional estandarizado.', false, 38),
  ('Verificar que las instalaciones electricas, agua y gas esten debidamente anuladas.', false, 39),
  ('Verificar ubicacion de torre grúa para que no exponga a caseta de ventas u otros.', false, 40),
  ('Verificar que se tenga un presupuesto inicial real en lo referente a SSOMA.', false, 41),
  ('Identificación con brazaletes para Brigadistas de Emergencias y Comité de Seguridad', false, 42)
) AS v(dsc, adj, ord)
WHERE p.nombre = 'Inicio de Proyecto';

-- ── ÍTEMS — Inicio de Demolicion ────────────────────────────────────────────
INSERT INTO ss_checklist_plantilla_item (plantilla_id, descripcion, tiene_adjunto_ref, orden, activo, created_at, updated_at)
SELECT p.id, v.dsc, v.adj, v.ord, true, NOW(), NOW()
FROM ss_checklist_plantilla p
CROSS JOIN (VALUES
  ('Realizar requerimiento inicial de señalización interna, EPPs, Conos, barras retractiles, malla raschel para cerco perimétrico, elementos para el botiquín y estación de emergencia, formatos de SST, segregación de residuos solidos, panel informativo, gestión de visitas y otro que se requiera para el inicio de obra', true, 1),
  ('Solicitar el letrero informativo de obra de acuerdo a los datos de la licencia de obra DEMOLICIÓN', false, 2),
  ('Solicitar servicios higiénicos con lavatorio y duchas portátiles', true, 3),
  ('Coordinar el ingreso de personal pdr, vigía y monitor de obra', false, 4),
  ('Realizar el Plan de Seguridad y Salud en el Trabajo y elementos', false, 5),
  ('Solicitar la inscripción en RENOCC de la obra', false, 6),
  ('Coordinar el ingreso de las concesionarias de alimentos', false, 7),
  ('Implementar la estación de emergencia', false, 8),
  ('Implementar los cilindros de colores para los residuos', false, 9),
  ('Realizar el Plan de Respuesta ante Emergencias', false, 10),
  ('Realizar Plan para la Vigilancia, Prevención y Control Covid-19 de los trabajadores', false, 11),
  ('Coordinar la Implementación del comedor', false, 12),
  ('Coordinar la implementación de los vestuarios', false, 13),
  ('Realizar y publicar el mapa de riesgo', false, 14),
  ('Realizar la implementación del Comité de Seguridad y Salud en el Trabajo', false, 15),
  ('Realizar Inducción SSOMA al personal nuevo que ingresa a obra', false, 16),
  ('Validar la documentación de subcontratista nuevo que ingresa a obra', false, 17),
  ('Pegar el letrero de licencia de obra en el frontis', false, 18),
  ('Elaborar y publicar el IPERC de LineaBase', false, 19),
  ('Verificar las protecciones colectivas.', false, 20),
  ('Verificar los puntales según necesidad y realizar el pedido con tiempo.', false, 21),
  ('Solicitar insumos para señalización y delimitación de áreas,', false, 22),
  ('Plano de protecciones colectivas.', false, 23),
  ('Conformación de brigadas de emergencia', false, 24),
  ('Realizar y publicación plano de evacuación', false, 25),
  ('Enviar requerimiento de extintores con sus bases.', false, 26),
  ('Realizar implementación de chalecos de visitantes y cascos.', false, 27),
  ('Definir puntos de lavados de manos.', false, 28),
  ('Definir punto de hidratación', false, 29),
  ('Definir donde se ubicaran los cilindros para gestión de residuos solidos', false, 30),
  ('Implementación de almacén MATPEL', false, 31),
  ('Plan de Medio Ambiente', false, 32),
  ('Implementar SERVICIOS DEL AREA BIENESTAR', false, 33),
  ('Pozo a tierra en cada predio que se tenga licencia.', false, 34),
  ('Tablero electrico provisional estandarizado.', false, 35),
  ('Maletin de Primeros auxilios segun D.S.011', false, 36),
  ('Verificar y coordinar la colocación de protecciones colectivas para los vecinos con anticipación', false, 37),
  ('Verificar la autorización para la ejecución de obra - Municipalidad de Pueblo Libre', false, 38),
  ('Verificar que la manguera de la cisterna no presente fisuras por donde salga el agua al momento de humedecer el área.', false, 39),
  ('Revisar La normativa local, ordenanzas municipales, del distrito donde está ubicado el proyecto', true, 40),
  ('Realizar requerimiento inicial de letreros de señalización vertical de acuerdo al Plano de desvío del permiso de interferencia de vías (Cerco perimetrico y vias)', false, 41),
  ('Aterramiento de la estructura metálica (cerco perimetrico) según normativa de protección eléctrica.', false, 42),
  ('Instalación de canaleta pasacables tipo rampa para control de interferencias en vereda interferida', false, 43),
  ('Señalización luminosa mediante toletes en maniobra nocturna', false, 44),
  ('Monitoreo aéreo preventivo mediante dron para evaluación de zonas críticas', false, 45),
  ('Verificación de líneas energizadas mediante multímetro tipo pinza', false, 46),
  ('Ejecución de calicatas técnicas como parte del análisis previo de riesgos.', false, 47),
  ('Utilización de bomba de presión de agua para control de emisiones particuladas.', false, 48),
  ('Realizar la toma de evidencia fotográfica de los vecinos con el dron del área de Marketing.', false, 49)
) AS v(dsc, adj, ord)
WHERE p.nombre = 'Inicio de Demolicion';

-- ── ÍTEMS — Torre Grua ───────────────────────────────────────────────────────
INSERT INTO ss_checklist_plantilla_item (plantilla_id, descripcion, tiene_adjunto_ref, orden, activo, created_at, updated_at)
SELECT p.id, v.dsc, v.adj, v.ord, true, NOW(), NOW()
FROM ss_checklist_plantilla p
CROSS JOIN (VALUES
  ('Certificado de instalación y Operatividad', false, 1),
  ('Memoria de calculo para zapata de Torre Grúa', false, 2),
  ('Manual de operación', false, 3),
  ('Procedimiento seguro de Montaje de Grau Torre', false, 4),
  ('Plan y programa de Mantenimiento de la Torre Grúa', false, 5),
  ('Póliza TREC', false, 6),
  ('Póliza CAR', false, 7),
  ('Certificado del Operador de la Torre Grau', false, 8),
  ('Certificado del Rigger', false, 9),
  ('CV documentados de los técnicos de montaje', false, 10),
  ('Seguro SCTR y Vida Ley', false, 11),
  ('EMO y Test de Altura', false, 12),
  ('Matriz de comunicaciones', false, 13),
  ('Matriz IPERC', false, 14),
  ('PETS – Trabajos en altura', false, 15),
  ('Plan de emergencia', false, 16),
  ('Tabla de carga', false, 17),
  ('Certificacion externa de Torre Grúa por entidad acreditadora SGS PERÚ (Plazo: 1re mes desde montaje)', false, 18),
  ('Plan rigging', false, 19)
) AS v(dsc, adj, ord)
WHERE p.nombre = 'Torre Grua';

-- ── ÍTEMS — PLACING BOOM ─────────────────────────────────────────────────────
INSERT INTO ss_checklist_plantilla_item (plantilla_id, descripcion, tiene_adjunto_ref, orden, activo, created_at, updated_at)
SELECT p.id, v.dsc, v.adj, v.ord, true, NOW(), NOW()
FROM ss_checklist_plantilla p
CROSS JOIN (VALUES
  ('Certificado de Operatividad de la Placing Boom', false, 1),
  ('Póliza TREC', false, 2),
  ('Certficado de Mantenimiento', false, 3)
) AS v(dsc, adj, ord)
WHERE p.nombre = 'PLACING BOOM';

-- ── ÍTEMS — Grúa Móvil ───────────────────────────────────────────────────────
INSERT INTO ss_checklist_plantilla_item (plantilla_id, descripcion, tiene_adjunto_ref, orden, activo, created_at, updated_at)
SELECT p.id, v.dsc, v.adj, v.ord, true, NOW(), NOW()
FROM ss_checklist_plantilla p
CROSS JOIN (VALUES
  ('Ficha técnica de la Grúa Móvil', false, 1),
  ('Certificado de operatividad', false, 2),
  ('Certificado de equipo de izaje (eslingas, grilletes, cables)', false, 3),
  ('Manual de operación', false, 4),
  ('Póliza TREC', false, 5),
  ('Póliza CAR', false, 6),
  ('Certificado del Operador de la Torre Grau', false, 7),
  ('Certificado del Rigger', false, 8),
  ('Seguro SCTR y Vida Ley', false, 9),
  ('EMO y Test de Altura', false, 10),
  ('Matriz de comunicaciones', false, 11),
  ('Matriz IPERC', false, 12),
  ('PETS – Trabajos en altura', false, 13),
  ('Plan de emergencia', false, 14),
  ('Tabla de carga', false, 15),
  ('Cartilla de mantenimiento de maquinaria', false, 16),
  ('Radios de comunicación portátil (Operador de la Grúa Móvil y Rigger)', false, 17)
) AS v(dsc, adj, ord)
WHERE p.nombre = 'Grúa Móvil';

-- ── ÍTEMS — SUNAFIL ──────────────────────────────────────────────────────────
INSERT INTO ss_checklist_plantilla_item (plantilla_id, descripcion, tiene_adjunto_ref, orden, activo, created_at, updated_at)
SELECT p.id, v.dsc, v.adj, v.ord, true, NOW(), NOW()
FROM ss_checklist_plantilla p
CROSS JOIN (VALUES
  ('Accesos libres, rutas de evacuación, señalización y protecciones colectivas', false, 1),
  ('Índice de accidentabilidad', false, 2),
  ('kardex de entrega de EPPs, y el cumplimiento de estas en campo', false, 3),
  ('Condiciones de seguridad', false, 4),
  ('Servicios de bienestar deben estar de acuerdo la norma G050, en la cantidad de baños no se debe contabilizar los designados a staff y damas, estos deben ser adicionales.', false, 5),
  ('Las rampas de acceso debe contar con barandas de protección colectiva', false, 6),
  ('El comedor debe estar limpio', false, 7),
  ('Los casilleros debe estar de acuerdo a la cantidad de trabajadores y en buen estado', false, 8),
  ('Las rutas de evacuación deben estar libres', false, 9),
  ('Los trabajadores en campo deben contar con todos sus equipos de protección personal y en buen estado', false, 10),
  ('La obra debe contar con señalización de obligación, evacuación, advertencia y prohibición', false, 11),
  ('Orden y limpieza general de obra', false, 12),
  ('Barandas de protección en el perímetro de la obra', false, 13),
  ('Ductos cerrados', false, 14),
  ('Tranquera en Piso 1, del area del elevador', false, 15),
  ('Tableros Electricos, conectados a la linea de tierra, al igual que su diagrama unifiliar y sus diferenciales', false, 16),
  ('Instalacion de Rodapie, zonas donde existan trabajos', false, 17),
  ('Orden y limpieza en area del comedor y vestuarios.', false, 18),
  ('Verificacion del TR5 Y Tregistro del personal registrado en charla y hojas de asistencia al momento de la visita', false, 19),
  ('Certificados de operativas de Extintores', false, 20),
  ('SCTR y Vida ley del Personal asistente el dia de la visita', false, 21),
  ('Certificado de los pozo a tierra', false, 22),
  ('Carnet RETCC del Personal asistente el dia de la visita', false, 23),
  ('Facturas y Recibos de pago de los SCTR y Vida Ley', false, 24),
  ('Se verifico la cantidad de Duchas, urinario, Lavatorios e Inodoros, según la cantidad de personal en obra, De igual manera su limpieza. También se debe considerar los baños y duchas de acuerdo al genero.', false, 25),
  ('El acceso a la prelosa debe realizarse mediante escaleras de andamio o escaleras de características similares, evitando el uso de escaleras telescópicas o lineales', false, 26),
  ('Mantener un orden y limpieza adecuado en los vestuarios, asegurando que cada casillero esté correctamente rotulada y sin alambres como seguro', false, 27),
  ('Se deben colocar tapas o rodapiés en los ductos con barandas', false, 28),
  ('Colocar tapas a las canaletas de desagüe en las rampas de los sótanos', false, 29),
  ('Los cables de las oficinas deben estar correctamente instaladas y los tomacorrientes fijado (no colgados)', false, 30),
  ('Plano de protecciones colectivas de toda la obra', false, 31),
  ('Relación de todos los trabajadores contratistas y de casa; indicando los siguientes datos: Apellidos y Nombres (en orden alfabético de apellidos), DNI, Fecha de Ingreso, Cargo y Razón social.', false, 32),
  ('Contrato de las empresas contratistas indicando la razón social y número de RUC, así como los trabajos a realizar en la obra.', false, 33),
  ('Relación de todos los contratistas indicando los siguientes datos: Razón Social, RUC, domicilio fiscal y teléfonos de contacto.', false, 34),
  ('Licencia de construcción de la obra.', false, 35),
  ('Registro de charla de seguridad', false, 36),
  ('AST (Análisis Seguro de Trabajo) del día anterior y del día presente', false, 37),
  ('Plan de Seguridad y Salud en el Trabajo', false, 38),
  ('Certificado de operatividad de maquinaria', false, 39)
) AS v(dsc, adj, ord)
WHERE p.nombre = 'SUNAFIL';

-- ── ÍTEMS — ITSE ─────────────────────────────────────────────────────────────
INSERT INTO ss_checklist_plantilla_item (plantilla_id, descripcion, tiene_adjunto_ref, orden, activo, created_at, updated_at)
SELECT p.id, v.dsc, v.adj, v.ord, true, NOW(), NOW()
FROM ss_checklist_plantilla p
CROSS JOIN (VALUES
  ('SEÑALETICADE LUZ DE EMERGENCIA', false, 1),
  ('SEÑALETICA DE RIESGO ELECTRICO', false, 2),
  ('SEÑALETICA DE ZONA SEGURA DE SISMO', false, 3),
  ('SEÑALETICA DE DIRECCION DE SALIDA', false, 4),
  ('SEÑALETICA DE SALIDA', false, 5),
  ('SEÑALETICA DE SERVICIO HIGIENICO', false, 6),
  ('SEÑALETICA PROHIBIDO EL INGRESO', false, 7),
  ('SEÑALETICA BOTIQUIN', false, 8),
  ('SEÑALETICA EXTINTOR', false, 9),
  ('SEÑALETICA NO FUMAR', false, 10),
  ('SEÑALETICA ATENCION PREFERENCIAL', false, 11),
  ('SEÑALETICA NO DISCRIMINACION', false, 12),
  ('CERTIFICADO DE LUZ DE EMERGENCIA', false, 13),
  ('FUNCIONAMIENTO DE LUZ DE EMERGENCIA', false, 14),
  ('LUZ DE EMERGENCIA CERCA A TABLERO ELEC.', false, 15),
  ('GABINETE ELECTRICO DE METAL', false, 16),
  ('TABLERO ELECTRICO  CON MANDIL', false, 17),
  ('LLAVES TERMICAS CORRESPONDE AL CABLEADO', false, 18),
  ('DIFERENCIAL POR LLAVE TERMICA', false, 19),
  ('TOMAS DE CORRIENTE CON ESPIGA A TIERRA', false, 20),
  ('TABLERO Y LLAVES ROTULADAS', false, 21),
  ('RESERVA DE TABLERO CON TAPAS', false, 22),
  ('CABLE A TIERRA DE COLOR AMARILLO', false, 23),
  ('NO SE USA CABLES MELLIZOS', false, 24),
  ('PROTOCOLO POZO A TIERRA', false, 25),
  ('CERTIFICADO DE CALIBRACION', false, 26),
  ('SEÑALIZACION POZO A TIERRA', false, 27),
  ('CANALIZACION DE CABLEADO', false, 28),
  ('CERTIFICADO DE EXTINTORES', false, 29),
  ('EXTINTOR PQS', false, 30),
  ('EXTINTOR CO2', false, 31),
  ('TARJETA DE INSPECCION', false, 32),
  ('GABINETE O PODIO EXTINTOR', false, 33),
  ('CERT.LAMINA ESPEJO', false, 34),
  ('CERT.LAMINA LUNAS', false, 35),
  ('CERTI. FUNC. Y ATERRADO DE A/C', false, 36),
  ('ATERRO DE SPLITER', false, 37),
  ('ATERRADO CONDENSADOR Y CARCASA', false, 38),
  ('ESTRUCT.SOPORTE S/OXIDO', false, 39),
  ('CONTENEDOR S/OXIDO', false, 40),
  ('ESTRUC. SIN DAÑOS(HUMEDAD,RAJADURA,ETC)', false, 41),
  ('ESTRUCT.CONECTADA A TIERRA', false, 42),
  ('Rampa antiderrape', false, 43),
  ('PASAMANO ESTABLES', false, 44),
  ('CERTIF. DE FUMIGACION', false, 45),
  ('LICENCIA DE PUBLICIDAD', false, 46),
  ('CARTA DE GARANTIA DE CONTENEDOR', false, 47),
  ('CERTIFICADO DE ESCUADRAS', false, 48),
  ('PUERTAS ABREN EN SENTIDO DE CIRCU.', false, 49),
  ('ATERRADO DE RACK DE COMUNICACION', false, 50),
  ('ATERRADO DE ESTRUCTURA METALICA(RETIRO DE SALA)', false, 51),
  ('vidrios pavonados', false, 52),
  ('SALAS CON AREAS MENORES A 250 MTS PULSADOR /MAYORES CON SISTEMA ACI', false, 53),
  ('LIC. DE FUNCIONAMIENTO(EXHIBIDO)', false, 54)
) AS v(dsc, adj, ord)
WHERE p.nombre = 'ITSE';

-- ── ÍTEMS — ITSE OFICINA CENTRAL ─────────────────────────────────────────────
INSERT INTO ss_checklist_plantilla_item (plantilla_id, descripcion, tiene_adjunto_ref, orden, activo, created_at, updated_at)
SELECT p.id, v.dsc, v.adj, v.ord, true, NOW(), NOW()
FROM ss_checklist_plantilla p
CROSS JOIN (VALUES
  ('LIC. DE FUNCIONAMIENTO(EXHIBIDO)', false, 1),
  ('SEÑALETICA DE AFORO', false, 2),
  ('SEÑALETICA DE LUZ DE EMERGENCIA', false, 3),
  ('SEÑALETICA DE RIESGO ELECTRICO', false, 4),
  ('SEÑALETICA DE ALARMA CONTRAINCENDIO', false, 5),
  ('SEÑALETICA DE DIRECCION DE SALIDA', false, 6),
  ('SEÑALETICA DE SALIDA', false, 7),
  ('SEÑALETICA DE SERVICIO HIGIENICO', false, 8),
  ('SEÑALETICA PROHIBIDO EL INGRESO', false, 9),
  ('SEÑALETICA NO FUMAR', false, 10),
  ('SEÑALETICA EXTINTORES', false, 11),
  ('SEÑALETICA ATENCION PREFERENCIAL', false, 12),
  ('SEÑALETICA NO DISCRIMINACION', false, 13),
  ('SEÑALETICA DE PROH. INGRESO', false, 14),
  ('SEÑALETICA DE USO DE ARNES (TECHO TECNICO)', false, 15),
  ('DECLARACIÓN JURADA DE USO DE ARNES', false, 16),
  ('CERTIFICADO DE LUZ DE EMERGENCIA', false, 17),
  ('FUNCIONAMIENTO DE LUZ DE EMERGENCIA', false, 18),
  ('LUZ DE EMERGENCIA CERCA A TABLERO ELEC.', false, 19),
  ('GABINETE ELECTRICO DE METAL', false, 20),
  ('TABLERO ELECTRICO CON MANDIL', false, 21),
  ('LLAVES TERMICAS CORRESPONDE AL CABLEADO', false, 22),
  ('DIFERENCIAL POR LLAVE TERMICA', false, 23),
  ('TOMAS DE CORRIENTE CON ESPIGA A TIERRA', false, 24),
  ('TABLERO Y LLAVES ROTULADAS', false, 25),
  ('DIRECTORIO Y DIAGRAMA UNIFILAR ACTUALIZADO DE CADA TABLERO', false, 26),
  ('RESERVA DE TABLERO CON TAPAS', false, 27),
  ('CABLE A TIERRA DE COLOR AMARILLO DE TABLEROS', false, 28),
  ('NO SE USA CABLES MELLIZOS', false, 29),
  ('PROTOCOLO POZO A TIERRA', false, 30),
  ('CERTIFICADO DE CALIBRACION', false, 31),
  ('SEÑALIZACION POZO A TIERRA', false, 32),
  ('CANALIZACION DE CABLEADO', false, 33),
  ('CERTIFICADO DE EXTINTORES', false, 34),
  ('EXTINTOR PQS (recargados y enumeración correlativa)', false, 35),
  ('EXTINTOR CO2 (recargados y enumeración correlativa)', false, 36),
  ('TARJETA DE INSPECCION', false, 37),
  ('GABINETE O PODIO EXTINTOR EN BUEN ESTADO, SIN OXIDO (todo extintor en exterior en gabinete)', false, 38),
  ('CERT.LAMINA ESPEJO', false, 39),
  ('CERT.LAMINA VIDRIOS', false, 40),
  ('CERTI. FUNC. Y ATERRADO DE A/C (AIRE ACONDICIONADO)', false, 41),
  ('ATERRADO DE SPLITER', false, 42),
  ('ATERRADO CONDENSADOR Y CARCASA (cable amarillo visible)', false, 43),
  ('ESTRUCT.SOPORTE SIN OXIDO (realizar mantenimiento)', false, 44),
  ('ESTRUC. SIN DAÑOS (HUMEDAD, RAJADURA, ETC)', false, 45),
  ('ESTRUCT.CONECTADA A TIERRA (cable amarillo visible)', false, 46),
  ('CERTIF. DE MANTENIMIENTO DE GRUPO ELECTROGENO (cable de aterrado visible)', false, 47),
  ('GRUPO ELECTROGENO SIN OXIDO (PINTURA EN BUEN ESTADO)', false, 48),
  ('CABLES CUBIERTOS CON TUBO CORRUGADO', false, 49),
  ('TOMAS DE BAÑOS CON HIDROBOX', false, 50),
  ('ATERRAMIENTO DE MESAS DE TRABAJO Y ESCRITORIOS (colocar sticker visible en escritorio)', false, 51),
  ('RETIRAR EXTENSIONES EXTERNAS PROVISIONALES', false, 52),
  ('ATERRAMIENTO DE SERVIDOS Y RACKS DE DATA (CABLE AMARILLO VISIBLE)', false, 53),
  ('ALMACENES ORDENADOS Y CON RUTAS DESPEJADAS', false, 54),
  ('PASILLOS DESPEJADOS DE MATERIALES', false, 55),
  ('PINTURA DE EDIFICIO SIN MOHO, RAJADURAS, ETC', false, 56),
  ('PASAMANO ESTABLES', false, 57),
  ('CERTIF. DE FUMIGACION', false, 58),
  ('CERTIFICADO DE ALARMA CONTRA INCENDIOS', false, 59),
  ('BARANDAS DE TECHO TECNICO SIN OXIDO, PINTURA EN BUEN ESTADO', false, 60),
  ('PLAN DE SEGURIDAD ACTUALIZADA( AFORO, TIEMPO DE EVACUACIÓN, ACTA DE CAPACITACIONES)', false, 61),
  ('CRONOGRAMA DE CAPACITACIONES Y SIMULACROS', false, 62),
  ('DECLARACION JURADA DE USO DE ARNES EN TECHO TECNICO y/o PETS del proveedor que hizo el servicio, indicando uso de arnés en su procedimiento', false, 63),
  ('BRIGADA DE SEGURIDAD ACTUALIZADA', false, 64),
  ('TODAS LAS SEÑALETICAS FOTOLUMINISCENTES', false, 65),
  ('PLANO UNIFILAR ACTUALIZADO CON LAS REMODELACIONES', false, 66),
  ('PLANO INDECI ACTUALIZADO', false, 67),
  ('PLANO DE UBICACIÓN ACTUALIZADO', false, 68),
  ('DOCUMENTACIÓN EN FISICO PARA PASAR POR MESA DE PARTES SI LO SOLICITAN', false, 69),
  ('REVISIÓN DE ENCHUFES (DEBEN SER DE TRES ESPIGAS)', false, 70)
) AS v(dsc, adj, ord)
WHERE p.nombre = 'ITSE OFICINA CENTRAL';

-- ── ÍTEMS — Municipalidad ────────────────────────────────────────────────────
INSERT INTO ss_checklist_plantilla_item (plantilla_id, descripcion, tiene_adjunto_ref, orden, activo, created_at, updated_at)
SELECT p.id, v.dsc, v.adj, v.ord, true, NOW(), NOW()
FROM ss_checklist_plantilla p
CROSS JOIN (VALUES
  ('Registro de Charla diaria.', false, 1),
  ('SCTR vigente para verificación.', false, 2),
  ('Registro del ultimo mantenimiento de la torre grúa.', false, 3),
  ('Certificado de Operatividad de bobcat.', false, 4),
  ('Certificado de Operador de bobcat.', false, 5),
  ('ATS y permisos de trabajos de riesgo del día.', false, 6),
  ('Certificado de Operatividad de la Placing Boom', false, 7),
  ('Procedimiento de trabajo seguro de las actividades realizadas', false, 8),
  ('Certificado de disposición final de residuos sólidos', false, 9),
  ('Servicios de bienestar de acuerdo a la G050', false, 10),
  ('Protecciones colectivas', false, 11),
  ('Uso de EPP en campo', false, 12),
  ('Póliza CAR', false, 13),
  ('Permiso de interferencia de vías', false, 14),
  ('Señalización y rutas de acceso', false, 15),
  ('Pozo a tierra.', false, 16),
  ('Linea de vida vertical  independiente en el uso de andamios', false, 17),
  ('Inspección de estintor', false, 18),
  ('Inspección de extintor', false, 19),
  ('Tableros eléctricos: No debe existir conexiones en paralelo, las llaves deben ser del amperaje correcto, el diagrama unifilar debe estar de acuerdo con lo que se tiene en el tablero, las luminarias y los tomacorrientes deben ser independientes.', false, 20),
  ('Plano de las mallas anticaídas', false, 21),
  ('Cálculo de memoria de las mallas anticaída', false, 22),
  ('Certificado de las mallas anticaída', false, 23),
  ('Protocolo del Pozo a Tierra', false, 24),
  ('Plan de Emergencia Actualizado', false, 25),
  ('Plan de Seguridad y Salud en el Trabajo', false, 26),
  ('Plan de Manejo Ambiental', false, 27),
  ('Constancia de botadero autorizado', false, 28),
  ('Memoria ambiental de operación de seguridad vial y medidas de mitigación ambiental actualizado', false, 29)
) AS v(dsc, adj, ord)
WHERE p.nombre = 'Municipalidad';

-- ── ÍTEMS — Habilitacion de Sala de Ventas ───────────────────────────────────
INSERT INTO ss_checklist_plantilla_item (plantilla_id, descripcion, tiene_adjunto_ref, orden, activo, created_at, updated_at)
SELECT p.id, v.dsc, v.adj, v.ord, true, NOW(), NOW()
FROM ss_checklist_plantilla p
CROSS JOIN (VALUES
  ('Permisos de la Municipalidad', false, 1),
  ('Permiso de vias', false, 2),
  ('protocolo pozo a tierra', false, 3),
  ('Tablero provisional completo', false, 4),
  ('extintores vigentes con certificados', false, 5),
  ('vestuarios', false, 6),
  ('oficina tecnica', false, 7),
  ('SSHH', false, 8),
  ('Almacen', false, 9),
  ('Señalizacion de vias y ambientes', false, 10)
) AS v(dsc, adj, ord)
WHERE p.nombre = 'Habilitacion de Sala de Ventas';

-- ── ÍTEMS — Señales de seguridad (Planos aprobados) ─────────────────────────
INSERT INTO ss_checklist_plantilla_item (plantilla_id, descripcion, tiene_adjunto_ref, orden, activo, created_at, updated_at)
SELECT p.id, v.dsc, v.adj, v.ord, true, NOW(), NOW()
FROM ss_checklist_plantilla p
CROSS JOIN (VALUES
  ('Solo se debe colocar las señales de seguridad que figuran en el plano aprobado, no descartar ninguna ni adicionar otra.', false, 1),
  ('Todas las señales de seguridad serán colocadas a la altura de 1.80 m.', false, 2),
  ('Cuando se detecte en campo la ubicación de un extintor en una zona inaccesible, reportarlo al área de proyectos para que compatibilice los planos.', false, 3),
  ('Una vez culminado la colocación de todas las señales de seguridad, reportarlo al área de proyectos para que procedan con una revisión anticipada, con la finalidad de detectar posibles observaciones.', false, 4),
  ('Al hacer el metrado de las señales de seguridad, no considerar aquellas señales que se encuentran dentro de los departamentos.', false, 5),
  ('Tener en cuenta el tipo de las señales de seguridad, estas pueden ser señales fotoluminicentes o señales luminosas, consultar al área de proyectos.', false, 6),
  ('Considerar los protectores de extintores, para aquellos que se encuetran a la imterperie.', false, 7),
  ('Considerar un botiquin de primeros auxilios, con los insumos basicos, este botiquin debe ser entregado al conserje del lobby,', false, 8),
  ('Consultar al área de proyectos, si la señal de pase de manguera se colocarán en todas las caras de los muros, o solo en la cara donde está la conexión de bomberos hacia los pasillos.', false, 9),
  ('Al hacer el requerimiento de los extintores, tambien solicitar los ganchos tipo "L" pernos y tarugos.', false, 10),
  ('Para el requerimiento de los extintores, tener en cuenta el tipo y tamaño, considerar PQS de 6 kg., (consultar con proyectos)', false, 11),
  ('Se debe recepcionar por correo el plano que tiene la ultima versión', false, 12),
  ('Consultar a la Jefatura de Calidad a que distancia del nivel del suelo (1.5m o 1.2m) se debe instalar el gancho tipo "L" (soporte del extintor)', false, 13),
  ('Antes de instalar el gancho tipo "L" (Soporte del extintor) verificar que en la parte interior de la pared  no se encuentren tomacorrientes.', false, 14),
  ('Antes de instalar el gancho tipo "L" (Soporte del extintor) verificar que en la parte superir no se encuenten conecciones ni ventas de extracción ya que se instalara la señaletica de extintor en dicha área.', false, 15),
  ('Consultar con la Jefatura de Calidad si el extiror de C02 que esta ubicado en el lobby ira en pedestal o con el gancho tipo "L"', false, 16),
  ('Verificar que la distancia a recorrer hasta el extintor desde el lobby cumpla con la NTP 350.043-1', false, 17),
  ('Antes de instalar las señales de seguridad en áreas comunes, consultar por el plano de detalles para evitar el cruce con las decoraciones', false, 18)
) AS v(dsc, adj, ord)
WHERE p.nombre = 'Señales de seguridad (Planos aprobados)';

-- ── ÍTEMS — ESTUDIO DE SUELO(CALICATA) ──────────────────────────────────────
INSERT INTO ss_checklist_plantilla_item (plantilla_id, descripcion, tiene_adjunto_ref, orden, activo, created_at, updated_at)
SELECT p.id, v.dsc, v.adj, v.ord, true, NOW(), NOW()
FROM ss_checklist_plantilla p
CROSS JOIN (VALUES
  ('EMO', false, 1),
  ('Curriculum Vitae - CV', false, 2),
  ('Seguro Complemetario de trabajo de Riesgo -  SCTR', false, 3),
  ('Pòliza de Vida Ley', false, 4),
  ('Factura de pago SCTR y Vida Ley', false, 5),
  ('Hoja de asistencia medica de SCTR  firmada y sellada por grte. gral.', false, 6),
  ('Plan SSOMA', false, 7),
  ('Plan de rescate', false, 8),
  ('Procedimiento Escrito de Trabajo Seguro', false, 9),
  ('IPERC', false, 10),
  ('Supervisor de Campo', false, 11)
) AS v(dsc, adj, ord)
WHERE p.nombre = 'ESTUDIO DE SUELO(CALICATA)';

-- ── ÍTEMS — Seguridad de Información de SO ───────────────────────────────────
INSERT INTO ss_checklist_plantilla_item (plantilla_id, descripcion, tiene_adjunto_ref, orden, activo, created_at, updated_at)
SELECT p.id, v.dsc, v.adj, v.ord, true, NOW(), NOW()
FROM ss_checklist_plantilla p
CROSS JOIN (VALUES
  ('Medico ocupacional debe tener los archivos de descarga en una carpeta cifrada (contraseña).', false, 1),
  ('Enfermero o medico deben tener toda la informacion fisica bajo llave.', false, 2),
  ('Enfermero o medico ocupacional deben tener todos los archivos de su latop o pc con contraseñas.', false, 3),
  ('Solo medico o enfermero deben tener acceso  a caulquier base de datos y aplicaciones (ni el diseñador, ni programador deben tener acceso).', false, 4),
  ('Al enviar los EMOs a los trabajadores deben tener contraseña (DNI).', false, 5)
) AS v(dsc, adj, ord)
WHERE p.nombre = 'Seguridad de Información de SO';

-- ── ÍTEMS — Estación de Primeros Auxilios ────────────────────────────────────
INSERT INTO ss_checklist_plantilla_item (plantilla_id, descripcion, tiene_adjunto_ref, orden, activo, created_at, updated_at)
SELECT p.id, v.dsc, v.adj, v.ord, true, NOW(), NOW()
FROM ss_checklist_plantilla p
CROSS JOIN (VALUES
  ('Paquetes de guantes desechables (02)', false, 1),
  ('Paquetes de apósitos o gasas absorbentes de 32 pulgadas cuadradas (02)', false, 2),
  ('Rollo de esparadrapo 5cm x 4.5cm (01)', false, 3),
  ('Rollos de venda elástica de 2 pulgadas x 5 yardas (02)', false, 4),
  ('Rollos de venda elástica de 5 pulgadas x 5 yardas (02)', false, 5),
  ('Rollos de venda elástica de 8 pulgadas x 5 yardas (02)', false, 6),
  ('Venda triangular 40 x 40 x 56 pulgadas (01)', false, 7),
  ('Paleta baja lengua (10)', false, 8),
  ('Venditas autoadhesivas (01)', false, 9),
  ('Frasco de solución de cloruro de sodio al 9/1000 x litro (01)', false, 10),
  ('Lava ojo portátil (01)', false, 11),
  ('Paquetes de gasa tipo jelonet [para quemaduras] (06)', false, 12),
  ('Tijera de trauma punta roma (01)', false, 13),
  ('Camilla rígida con protector de cabeza - inmovilizador de cabeza (01)', false, 14),
  ('Camilla tipo canastilla (01)', false, 15),
  ('Frazada (01)', false, 16),
  ('Resucitador manual o pocket mask (01)', false, 17),
  ('Collarín regulable (01)', false, 18),
  ('Torniquete (01)', false, 19),
  ('Instructivo de primeros auxilios (01)', false, 20),
  ('Registro de control de entrada y salida de insumos (01)', false, 21),
  ('Fédula inmovilizadora (01)', false, 22),
  ('Frasco de Yodopovidona de 120 ml (01)', false, 23),
  ('Frasco de Agua Oxigenada de 120 ml (01)', false, 24),
  ('Frasco de Alcohol de 250 ml (01)', false, 25),
  ('Paquete de Gasas esteriles de 10x10 cm (05)', false, 26),
  ('Paquete de Algodón por 100 gr (01)', false, 27),
  ('Frasco de Colirio (lavado de ojos) (02)', false, 28),
  ('Pinza (01)', false, 29),
  ('Cuerda para rescate 50 metros (01)', false, 30),
  ('Extintor PQS 9 Kg mínimo (01)', false, 31),
  ('Kit Antiderrame (01)', false, 32)
) AS v(dsc, adj, ord)
WHERE p.nombre = 'Estación de Primeros Auxilios';

-- ── ÍTEMS — AUDITORÍA MINTRA ─────────────────────────────────────────────────
INSERT INTO ss_checklist_plantilla_item (plantilla_id, descripcion, tiene_adjunto_ref, orden, activo, created_at, updated_at)
SELECT p.id, v.dsc, v.adj, v.ord, true, NOW(), NOW()
FROM ss_checklist_plantilla p
CROSS JOIN (VALUES
  ('Política SST (Registro de Difusión y entrega al personal)', false, 1),
  ('Conformación del comité SST  (Publicación del Organigrama en Paneles Informativos)', false, 2),
  ('Objetivos, plan y programa anual de SST(Difusión y entrega al personal)', false, 3),
  ('Reglamento interno de SST (Registro de Entrega y Difusión: INDUCCIÓN)', false, 4),
  ('Matriz IPERC (Publicación en el frente de Trabajo firmado  por SSOMA , registro de entrega y difusión: INDUCCIÓN)', false, 5),
  ('Investigación y reporte de accidentes, enfermedades ocupacionales, incidentes peligrosos (Power Apps)', false, 6),
  ('Estadísticas de Accidentabilidad (Power Apps)', false, 7),
  ('Monitoreos ocupacionales; químicos, físicos, biológicos, disergonómicos, psicosociales, entre otros según la actividad.', false, 8),
  ('Planes y respuesta a emergencias, simulacros, simulaciones (Registro de Difusión y entrega al personal)', false, 9),
  ('Inspecciones planeadas y no planeadas (Power Apps)', false, 10),
  ('Comunicaciones, estadísticas, señalética (Power Apps)', false, 11),
  ('Inducción y capacitación (Power Apps e INDUCCIÓN)', false, 12),
  ('Gestión de contratistas en SST', false, 13),
  ('Auditorías internas y externas en SST', false, 14),
  ('Gestión del cambio', false, 15),
  ('Relación de trabajadores y sus puestos de trabajo (legajos del personal, Perfiles de puesto o MOF) (Administración y Power Apps)', false, 16),
  ('Informe de estadísticas y actividades trimestrales del comité de SST y resumen anual de SST a la Gerencia General', false, 17),
  ('Mapa de riesgos (Publicación en los Frentes de Trabajo firmado por SSOMA Considerar los servicios de bienestar: puntos de hidratación, estación de emergencia, panel informativo, baños y lavamanos)', false, 18),
  ('Conformación de Brigadas de emergencias (Capacitaciones  de los brigadistas y Lista de personal que la conforma)', false, 19),
  ('Evidencia de vigilancia del sistema de gestión de SST y revisión por la dirección en SST', false, 20),
  ('CHECK LIST DE AUDITORIA MINTRA LEY 29783, D.S.005-2012-TR', true, 21)
) AS v(dsc, adj, ord)
WHERE p.nombre = 'AUDITORÍA MINTRA';

-- ── ÍTEMS — MONITOREO DE MEDIO AMBIENTE ──────────────────────────────────────
INSERT INTO ss_checklist_plantilla_item (plantilla_id, descripcion, tiene_adjunto_ref, orden, activo, created_at, updated_at)
SELECT p.id, v.dsc, v.adj, v.ord, true, NOW(), NOW()
FROM ss_checklist_plantilla p
CROSS JOIN (VALUES
  ('MONITOREO DE CALIDAD DE AIRE: Material particulado PM10 Bajo Volumen', false, 1),
  ('MONITOREO DE CALIDAD DE AIRE: Material particulado PM 2.5 Bajo Volumen', false, 2),
  ('MONITOREO DE CALIDAD DE AIRE: Monóxido de carbono (CO) 8 hora', false, 3),
  ('MONITOREO DE CALIDAD DE AIRE: Dióxido de Nitrógeno (NO2) 1 hora', false, 4),
  ('MONITOREO DE CALIDAD DE AIRE: Dióxido de azufre (SO2) 24 horas', false, 5),
  ('Meteorología: Velocidad del viento, Dirección del viento, Humedad relativa, Temperatura y Presión atmosférica', false, 6),
  ('MONITOREO DE CALIDAD DE RUIDO: Diurno', false, 7)
) AS v(dsc, adj, ord)
WHERE p.nombre = 'MONITOREO DE MEDIO AMBIENTE';

-- ── ÍTEMS — MONITOREO OCUPACIONAL ────────────────────────────────────────────
INSERT INTO ss_checklist_plantilla_item (plantilla_id, descripcion, tiene_adjunto_ref, orden, activo, created_at, updated_at)
SELECT p.id, v.dsc, v.adj, v.ord, true, NOW(), NOW()
FROM ss_checklist_plantilla p
CROSS JOIN (VALUES
  ('Iluminación', false, 1),
  ('Ruido por dosimetria', false, 2),
  ('Polvo Inhalable', false, 3),
  ('Polvo respirable', false, 4),
  ('Bacterias', false, 5),
  ('Mohos y levaduras', false, 6),
  ('Coliformes totales', false, 7),
  ('E. Coli', false, 8),
  ('Recuento de Coliformes Totales (superficie irregular)', false, 9),
  ('Postura', false, 10),
  ('Psicosocial', false, 11)
) AS v(dsc, adj, ord)
WHERE p.nombre = 'MONITOREO OCUPACIONAL';

-- ── ÍTEMS — Declaración de RS en la Plataforma SINGERSOL ─────────────────────
INSERT INTO ss_checklist_plantilla_item (plantilla_id, descripcion, tiene_adjunto_ref, orden, activo, created_at, updated_at)
SELECT p.id, v.dsc, v.adj, v.ord, true, NOW(), NOW()
FROM ss_checklist_plantilla p
CROSS JOIN (VALUES
  ('Solicitar el usuario de la pagina de SINGERSOL a la Jefatura de Gestión Administrativa según el proyecto -  Razón Social  a declarar.', false, 1)
) AS v(dsc, adj, ord)
WHERE p.nombre = 'Declaración de RS en la Plataforma SINGERSOL';

COMMIT;

-- Verificación
SELECT p.nombre, COUNT(i.id) AS total_items
FROM ss_checklist_plantilla p
LEFT JOIN ss_checklist_plantilla_item i ON i.plantilla_id = p.id
GROUP BY p.nombre, p.orden
ORDER BY p.orden;
