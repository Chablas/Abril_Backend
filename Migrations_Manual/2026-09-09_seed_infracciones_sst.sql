-- Seed de ssoma_rac_infraccion con la tabla real vigente: "ANEXO 4 - OBLIGACIONES EN
-- SEGURIDAD, SALUD EN EL TRABAJO Y MEDIO AMBIENTE" (cláusula 10 del contrato), que reemplaza
-- al documento anterior que se usó por error (ese tenía montos fijos en soles; el vigente es
-- % de UIT para casi todo, con una sola excepción de monto fijo/porcentual bajo para faltas leves).
--
-- Si ya se había corrido el seed anterior (montos fijos S/100/150/250), este script lo limpia
-- primero -- el DELETE es inofensivo si nunca se aplicó.
DELETE FROM ssoma_rac_infraccion WHERE nombre IN (
  'Uso de EPP en mal estado',
  'Uso de herramientas hechizas o subestándar',
  'Reportes o documentación fuera de plazo',
  'Inasistencia a charla de seguridad',
  'Reincidencia de infracción leve (2 semanas)',
  'Incumplimiento de procedimiento de trabajo seguro',
  'SCTR fuera de plazo',
  'Trabajo sin permiso o documentación habilitante',
  'Reincidencia de infracción moderada (3 semanas)',
  'Labor sin entrenamiento estipulado',
  'No usar EPP',
  'Incumplimiento del Plan de SST con riesgo de accidente'
);

-- Requiere que la migración 2026-09-09_penalidad_independiente_de_rac.sql ya se haya aplicado.

-- ── Falta leve — 5% de 1 UIT (única categoría de excepción con tarifa reducida) ─────────────
INSERT INTO ssoma_rac_infraccion (nombre, factor_uit, monto_fijo, descripcion, activo) VALUES
('Falta leve', 0.05, NULL,
 'Faltas de menor riesgo — 5% de 1 UIT c/u. Incluye, entre otras: inicio de trabajos sin campaña de limpieza previa; no usar doble protección auditiva (primer incidente); falta de botiquín completo; sin cronograma de limpieza de vestuarios/almacenes; sin panel informativo de la empresa; vestuarios/oficinas sin señalización o desordenados; consumo de alimentos fuera de horario en departamentos de obra; uso del celular para fines personales durante tareas (primer incidente); herramientas hechizas o modificadas; tardanza/inasistencia de supervisores a charla (primer incidente); falta de EPP básico (primer incidente); retiro no autorizado de señalización o protecciones colectivas (primer incidente); no realizar charlas grupales (primer incidente); ingreso a áreas restringidas sin autorización (primer incidente); iniciar actividades sin firma de ATS (primer incidente).',
 true);

-- ── Infracción menor — 89% de 1 UIT ─────────────────────────────────────────────────────────
INSERT INTO ssoma_rac_infraccion (nombre, factor_uit, monto_fijo, descripcion, activo) VALUES
('Infracción menor', 0.89, NULL,
 'Incluye, entre otras: usar EPP en mal estado; uso de herramientas hechizas o en condiciones subestándar; no presentar a tiempo reportes o documentación solicitada por el coordinador de SST y medio ambiente; no estar presente en la charla de seguridad previa al inicio de jornada.',
 true);

-- ── Infracción moderada — 392% de 1 UIT ─────────────────────────────────────────────────────
INSERT INTO ssoma_rac_infraccion (nombre, factor_uit, monto_fijo, descripcion, activo) VALUES
('Infracción moderada', 3.92, NULL,
 'Incluye, entre otras: reincidencia en infracción menor en dos semanas; no cumplir con los procedimientos de trabajo seguro entregados al ingreso de obra; no presentar a tiempo el SCTR y vouchers de pago de los trabajadores; realizar trabajos sin la documentación o permiso de trabajo.',
 true);

-- ── Infracción grave — 525% de 1 UIT ────────────────────────────────────────────────────────
INSERT INTO ssoma_rac_infraccion (nombre, factor_uit, monto_fijo, descripcion, activo) VALUES
('Infracción grave', 5.25, NULL,
 'Incluye, entre otras: reincidencia en infracción moderada en tres semanas; trabajador realizando labores sin el entrenamiento estipulado; no usar EPP; incumplir las normas del Plan de SST que resulten o podrían resultar en accidente; riñas dentro de la misma empresa; trabajar bajo efectos de alcohol o drogas no medicadas; trabajos en altura sin cumplir requisitos/elementos de seguridad; mantener deudas con el concesionario de alimentos de la obra; robo o intento de robo.',
 true);

-- ── Infracción muy grave — NO es una penalidad monetaria ────────────────────────────────────
-- Según el Anexo 4, la infracción muy grave (adulteración/falsificación de documentos, agresión
-- física o verbal a personal/terceros, ocultar o intentar ocultar accidentes) da lugar a
-- RESCISIÓN DE CONTRATO, no a un monto — no encaja en el modelo de Penalidad (que siempre calcula
-- un monto en soles) y por eso NO se siembra acá como infracción con tarifa. Si se necesita
-- registrar el caso, usar la categoría "Infracción grave" para dejar constancia económica y
-- tramitar la rescisión de contrato como una acción aparte (fuera de este módulo).

-- ── UIT ──────────────────────────────────────────────────────────────────────────────────────
-- UIT 2026 = S/ 5,500 (D.S. N° 301-2025-EF, MEF/SUNAT).
INSERT INTO ssoma_uit_anio (anio, valor, activo) VALUES (2026, 5500, true);
