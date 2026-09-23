-- ============================================================================
-- Evaluación 360° de Staff
--
-- El Residente de cada proyecto (workers.puesto_id -> puesto.categoria_id =
-- CategoriaIds.Residente = 8) evalúa, de forma IDENTIFICADA (a diferencia de
-- ev_evaluacion_jefe_ssoma/ev_evaluacion_gestion_ssoma, que son anónimas), a
-- todo el staff de SU proyecto: trabajadores workers_estado_id = 1 (activo),
-- obra_oficina_staff_id = 2 (Staff), mismo proyecto (worker_vinculaciones sin
-- fecha_fin) y cuyo puesto_id esté en la lista de 16 puestos evaluables
-- (ver Shared/Constants/PuestoIds.cs -> StaffEvaluablePuestoIds).
--
-- Comparte el calendario de ev_periodo con los demás flujos de Evaluaciones.
--
-- Idempotente: usa IF NOT EXISTS / ON CONFLICT DO NOTHING, se puede re-correr.
-- ============================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS ev_staff_plantilla (
    id          serial PRIMARY KEY,
    puesto_id   int NOT NULL REFERENCES puesto(puesto_id),
    criterio    text NOT NULL,
    tipo        varchar(20) NOT NULL CHECK (tipo IN ('FUNCIONAL', 'TRANSVERSAL')),
    orden       int NOT NULL DEFAULT 0,
    activo      boolean NOT NULL DEFAULT true
);

CREATE TABLE IF NOT EXISTS ev_evaluacion_staff (
    id                  serial PRIMARY KEY,
    periodo_id          int NOT NULL REFERENCES ev_periodo(id),
    evaluador_user_id   int NOT NULL REFERENCES app_user(user_id),
    evaluado_worker_id  int NOT NULL REFERENCES workers(id),
    project_id          int NOT NULL REFERENCES project(project_id),
    nota                numeric(5,2),
    comentario          text,
    created_at          timestamp NOT NULL DEFAULT now(),
    UNIQUE (periodo_id, evaluador_user_id, evaluado_worker_id)
);

CREATE TABLE IF NOT EXISTS ev_evaluacion_staff_detalle (
    id              serial PRIMARY KEY,
    evaluacion_id   int NOT NULL REFERENCES ev_evaluacion_staff(id),
    plantilla_id    int REFERENCES ev_staff_plantilla(id),
    criterio        text NOT NULL,
    puntaje         int NOT NULL CHECK (puntaje BETWEEN 1 AND 5)
);

CREATE INDEX IF NOT EXISTS ix_ev_eval_staff_evaluado
    ON ev_evaluacion_staff (evaluado_worker_id, periodo_id);
CREATE INDEX IF NOT EXISTS ix_ev_eval_staff_project
    ON ev_evaluacion_staff (project_id, periodo_id);

-- ── Criterios FUNCIONALES por puesto ─────────────────────────────────────────
-- Se insertan solo si el puesto todavía no tiene ninguna fila en la plantilla
-- (evita duplicar si el script se corre más de una vez).

INSERT INTO ev_staff_plantilla (puesto_id, criterio, tipo, orden)
SELECT * FROM (VALUES
    -- Prevencionista de Riesgos (281)
    (281, 'Cumplimiento del programa de inspecciones de seguridad (IPERC, checklist de EPP, orden y limpieza) en campo.', 'FUNCIONAL', 1),
    (281, 'Calidad y consistencia de charlas de 5 minutos / ATS antes de tareas críticas.', 'FUNCIONAL', 2),
    (281, 'Efectividad en la identificación y cierre de actos/condiciones subestándar.', 'FUNCIONAL', 3),
    (281, 'Gestión de incidentes/accidentes: tiempo de reporte, investigación y plan de acción correctivo.', 'FUNCIONAL', 4),
    (281, 'Cumplimiento de permisos de trabajo de alto riesgo (trabajos en altura, caliente, espacios confinados, izaje).', 'FUNCIONAL', 5),
    (281, 'Manejo de indicadores SSOMA del frente y su reporte oportuno al residente.', 'FUNCIONAL', 6),

    -- Coordinador SSOMA (119)
    (119, 'Cumplimiento del Plan Anual de Seguridad y Salud en el Trabajo del proyecto.', 'FUNCIONAL', 1),
    (119, 'Liderazgo y supervisión efectiva del equipo de prevencionistas.', 'FUNCIONAL', 2),
    (119, 'Gestión documentaria SSOMA ante entidades (SUNAFIL, cliente, seguros) sin observaciones.', 'FUNCIONAL', 3),
    (119, 'Análisis de tendencias de incidentes y propuesta de acciones preventivas.', 'FUNCIONAL', 4),
    (119, 'Coordinación con producción/calidad para incorporar seguridad en la planificación semanal.', 'FUNCIONAL', 5),
    (119, 'Gestión de capacitación e inducción de personal nuevo y subcontratistas.', 'FUNCIONAL', 6),

    -- Administrador de Obra (9)
    (9, 'Oportunidad y exactitud en el control de planillas y pago a subcontratistas.', 'FUNCIONAL', 1),
    (9, 'Gestión logística: abastecimiento oportuno de materiales/insumos.', 'FUNCIONAL', 2),
    (9, 'Control de caja chica, rendiciones de gastos y documentación contable sin observaciones.', 'FUNCIONAL', 3),
    (9, 'Administración de contratos con proveedores y subcontratistas.', 'FUNCIONAL', 4),
    (9, 'Gestión de almacén (control de stock, kardex, custodia de bienes).', 'FUNCIONAL', 5),
    (9, 'Cumplimiento normativo laboral (SCTR, EPS, boletas, EPP) y cero contingencias administrativas.', 'FUNCIONAL', 6),

    -- Asistente de Producción (57)
    (57, 'Precisión y oportunidad en el reporte diario de avance físico de partidas.', 'FUNCIONAL', 1),
    (57, 'Apoyo efectivo en el control de cuadrillas.', 'FUNCIONAL', 2),
    (57, 'Registro y actualización de cuaderno de obra / bitácora de campo.', 'FUNCIONAL', 3),
    (57, 'Coordinación con almacén para disponibilidad de materiales.', 'FUNCIONAL', 4),
    (57, 'Identificación temprana de desviaciones en campo y comunicación oportuna.', 'FUNCIONAL', 5),
    (57, 'Cumplimiento de instrucciones técnicas y planos vigentes en la ejecución.', 'FUNCIONAL', 6),

    -- Asistente de Oficina Técnica (55)
    (55, 'Exactitud en la elaboración de metrados de campo y sustento de valorizaciones.', 'FUNCIONAL', 1),
    (55, 'Actualización y control de versiones de planos, especificaciones técnicas y RFI.', 'FUNCIONAL', 2),
    (55, 'Apoyo en la elaboración de reportes de avance físico-valorizado.', 'FUNCIONAL', 3),
    (55, 'Organización y trazabilidad del archivo técnico del proyecto.', 'FUNCIONAL', 4),
    (55, 'Soporte en cuadros comparativos de costos y cubicación de materiales.', 'FUNCIONAL', 5),
    (55, 'Cumplimiento de plazos internos de entrega de información.', 'FUNCIONAL', 6),

    -- Asistente de Calidad (43)
    (43, 'Apoyo en inspecciones de calidad (protocolos de recepción, punch list).', 'FUNCIONAL', 1),
    (43, 'Registro y seguimiento de no conformidades hasta su levantamiento.', 'FUNCIONAL', 2),
    (43, 'Control y archivo de protocolos de calidad, certificados y ensayos.', 'FUNCIONAL', 3),
    (43, 'Apoyo en verificación de cumplimiento de procedimientos constructivos.', 'FUNCIONAL', 4),
    (43, 'Coordinación con producción para programación de puntos de inspección (PIT).', 'FUNCIONAL', 5),
    (43, 'Precisión en el registro fotográfico y documentación de evidencias.', 'FUNCIONAL', 6),

    -- Arquitecto de Calidad (28)
    (28, 'Control de calidad de acabados frente a especificaciones técnicas.', 'FUNCIONAL', 1),
    (28, 'Gestión y cierre oportuno de no conformidades de arquitectura.', 'FUNCIONAL', 2),
    (28, 'Verificación de compatibilidad entre planos de arquitectura y ejecución real.', 'FUNCIONAL', 3),
    (28, 'Elaboración y seguimiento de checklist/protocolos de entrega de unidades.', 'FUNCIONAL', 4),
    (28, 'Coordinación con proveedores de acabados para validar muestras y mockups.', 'FUNCIONAL', 5),
    (28, 'Índice de reclamos post-entrega atribuibles a defectos de calidad.', 'FUNCIONAL', 6),

    -- Arquitecto de Producción (31)
    (31, 'Cumplimiento del cronograma de partidas de arquitectura a su cargo.', 'FUNCIONAL', 1),
    (31, 'Resolución oportuna de incompatibilidades entre especialidades en campo.', 'FUNCIONAL', 2),
    (31, 'Rendimiento de cuadrillas de acabados bajo su supervisión.', 'FUNCIONAL', 3),
    (31, 'Gestión de interferencias de diseño detectadas en obra.', 'FUNCIONAL', 4),
    (31, 'Optimización de secuencia constructiva de acabados.', 'FUNCIONAL', 5),
    (31, 'Control de desperdicio de materiales de acabado.', 'FUNCIONAL', 6),

    -- Arquitecto Coordinador de Obra (27)
    (27, 'Efectividad en la coordinación interdisciplinaria para evitar interferencias.', 'FUNCIONAL', 1),
    (27, 'Gestión y trazabilidad de consultas técnicas (RFI) de arquitectura.', 'FUNCIONAL', 2),
    (27, 'Control de cambios de diseño y su impacto en costo/plazo.', 'FUNCIONAL', 3),
    (27, 'Supervisión transversal del cumplimiento del diseño arquitectónico.', 'FUNCIONAL', 4),
    (27, 'Liderazgo en reuniones de coordinación de acabados con subcontratistas.', 'FUNCIONAL', 5),
    (27, 'Calidad y oportunidad de la documentación ''as built'' de arquitectura.', 'FUNCIONAL', 6),

    -- Ingeniero de Oficina Técnica (161)
    (161, 'Exactitud y sustento técnico de las valorizaciones mensuales.', 'FUNCIONAL', 1),
    (161, 'Control del resultado operativo (costo real vs. presupuesto meta).', 'FUNCIONAL', 2),
    (161, 'Gestión de adicionales, deductivos y ampliaciones de plazo.', 'FUNCIONAL', 3),
    (161, 'Elaboración y actualización de cronograma valorizado (curva S).', 'FUNCIONAL', 4),
    (161, 'Calidad y trazabilidad del control documentario técnico.', 'FUNCIONAL', 5),
    (161, 'Cumplimiento de plazos de entrega de reportes de costos/avance.', 'FUNCIONAL', 6),

    -- Ingeniero de Calidad (158)
    (158, 'Gestión del plan de puntos de inspección y ensayos (PIT/ITP).', 'FUNCIONAL', 1),
    (158, 'Efectividad en identificación, registro y cierre de no conformidades.', 'FUNCIONAL', 2),
    (158, 'Índice de reprocesos/observaciones por partida.', 'FUNCIONAL', 3),
    (158, 'Gestión de certificados de calidad de materiales y proveedores.', 'FUNCIONAL', 4),
    (158, 'Auditorías internas de calidad a subcontratistas.', 'FUNCIONAL', 5),
    (158, 'Elaboración de dossier de calidad y expediente de cierre.', 'FUNCIONAL', 6),

    -- Ingeniero de Producción (163)
    (163, 'Cumplimiento del cronograma general de obra.', 'FUNCIONAL', 1),
    (163, 'Rendimientos de mano de obra y equipos vs. presupuestado.', 'FUNCIONAL', 2),
    (163, 'Gestión eficiente de cuadrillas propias y subcontratistas.', 'FUNCIONAL', 3),
    (163, 'Anticipación y resolución de interferencias (look-ahead de 3 semanas).', 'FUNCIONAL', 4),
    (163, 'Control de desperdicio de materiales y uso eficiente de recursos.', 'FUNCIONAL', 5),
    (163, 'Coordinación efectiva con SSOMA y Calidad.', 'FUNCIONAL', 6),

    -- Ingeniero de Planeamiento BIM (162)
    (162, 'Actualización oportuna del modelo BIM conforme a cambios de diseño.', 'FUNCIONAL', 1),
    (162, 'Efectividad en detección y resolución de interferencias (clash detection).', 'FUNCIONAL', 2),
    (162, 'Calidad del BIM Execution Plan y reportes de coordinación.', 'FUNCIONAL', 3),
    (162, 'Precisión de cubicaciones y metrados extraídos del modelo (4D/5D).', 'FUNCIONAL', 4),
    (162, 'Soporte en planificación (simulación 4D) para anticipar conflictos.', 'FUNCIONAL', 5),
    (162, 'Capacitación y soporte al equipo de obra en uso de BIM.', 'FUNCIONAL', 6),

    -- Ingeniero Practicante (169)
    (169, 'Calidad y precisión de reportes, cálculos o registros técnicos asignados.', 'FUNCIONAL', 1),
    (169, 'Curva de aprendizaje y autonomía progresiva.', 'FUNCIONAL', 2),
    (169, 'Cumplimiento de plazos en tareas de apoyo.', 'FUNCIONAL', 3),
    (169, 'Iniciativa para identificar y comunicar hallazgos.', 'FUNCIONAL', 4),
    (169, 'Manejo adecuado de herramientas técnicas requeridas por el área.', 'FUNCIONAL', 5),
    (169, 'Cumplimiento de protocolos de seguridad y disciplina en obra.', 'FUNCIONAL', 6),

    -- Coordinador Administrativo de Obra (103)
    (103, 'Cumplimiento normativo laboral sin contingencias.', 'FUNCIONAL', 1),
    (103, 'Eficiencia en la coordinación logística entre obra y sede central.', 'FUNCIONAL', 2),
    (103, 'Control presupuestal de gastos administrativos y generales de obra.', 'FUNCIONAL', 3),
    (103, 'Gestión y supervisión del personal administrativo y de servicios generales.', 'FUNCIONAL', 4),
    (103, 'Calidad y oportunidad de reportes administrativos/financieros.', 'FUNCIONAL', 5),
    (103, 'Gestión de relaciones con autoridades locales y trámites externos.', 'FUNCIONAL', 6),

    -- Supervisor de Instalaciones (301)
    (301, 'Cumplimiento del cronograma de instalaciones (eléctricas, sanitarias, HVAC).', 'FUNCIONAL', 1),
    (301, 'Verificación de compatibilidad de instalaciones con arquitectura/estructuras antes de ejecutar.', 'FUNCIONAL', 2),
    (301, 'Calidad de pruebas y protocolos de instalaciones.', 'FUNCIONAL', 3),
    (301, 'Coordinación con subcontratistas especializados para evitar interferencias entre sí.', 'FUNCIONAL', 4),
    (301, 'Gestión de no conformidades hasta su levantamiento.', 'FUNCIONAL', 5),
    (301, 'Elaboración de as built de instalaciones.', 'FUNCIONAL', 6)
) AS v(puesto_id, criterio, tipo, orden)
WHERE NOT EXISTS (SELECT 1 FROM ev_staff_plantilla sp WHERE sp.puesto_id = v.puesto_id AND sp.tipo = 'FUNCIONAL');

-- ── 5 competencias TRANSVERSALES, idénticas para los 16 puestos, orden 7-11 ──

INSERT INTO ev_staff_plantilla (puesto_id, criterio, tipo, orden)
SELECT puesto_id, criterio, 'TRANSVERSAL', orden
FROM (VALUES
    (281), (28), (161), (158), (9), (57), (119), (163), (162), (31), (55), (43), (103), (169), (27), (301)
) AS puestos(puesto_id)
CROSS JOIN (VALUES
    ('Compromiso: Alinea sus acciones, tiempo y esfuerzo con los objetivos de la organización y del equipo.', 7),
    ('Iniciativa: Actúa de forma proactiva, anticipándose a los problemas o necesidades y transformando las ideas en acciones concretas.', 8),
    ('Trabajo en Equipo: Colabora de forma activa con otras personas para alcanzar una meta común.', 9),
    ('Comunicación: Transmite, recibe e interpreta información, ideas y emociones de manera clara, eficaz y adecuada al contexto.', 10),
    ('Planificación y Organización: Definir metas de manera eficaz, establecer prioridades, y asignar acciones, plazos y recursos necesarios para cumplir con los objetivos.', 11)
) AS v(criterio, orden)
WHERE NOT EXISTS (
    SELECT 1 FROM ev_staff_plantilla sp
    WHERE sp.puesto_id = puestos.puesto_id AND sp.tipo = 'TRANSVERSAL'
);

COMMIT;

-- ============================================================================
-- Verificación (correr después; no modifica nada)
-- ============================================================================
-- SELECT table_name FROM information_schema.tables
-- WHERE table_name IN ('ev_staff_plantilla','ev_evaluacion_staff','ev_evaluacion_staff_detalle');
-- Esperado: las 3 filas.
--
-- SELECT puesto_id, tipo, count(*) FROM ev_staff_plantilla GROUP BY puesto_id, tipo ORDER BY puesto_id, tipo;
-- Esperado: 16 puestos x 2 filas (FUNCIONAL=6, TRANSVERSAL=5) = 32 filas, 176 criterios en total.
