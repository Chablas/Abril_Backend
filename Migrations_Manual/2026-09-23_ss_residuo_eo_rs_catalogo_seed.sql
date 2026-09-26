-- ============================================================================
-- Catálogo inicial de EO-RS / transportistas / destinos, tomado del historial
-- real de la obra KAURÍ (GP-FOR-035/036). Ajustar número de registro MINAM y
-- vigencia cuando se tenga la constancia/registro autoritativo real de cada
-- uno (por ahora se deja el N° de resolución tal como aparece en el Excel,
-- que en varios casos es el N° del instrumento de gestión ambiental del
-- destino, no necesariamente el registro EO-RS-MINAM del transportista).
--
-- Idempotente (se puede re-correr sin duplicar, por RUC).
-- ============================================================================
BEGIN;

INSERT INTO ss_residuo_eo_rs (ruc, razon_social, tipo_operador, numero_registro_minam, direccion, ambito_gestion, activo)
SELECT v.ruc, v.razon_social, v.tipo_operador, v.numero_registro_minam, v.direccion, v.ambito_gestion, true
FROM (VALUES
  ('20605624210', 'CONSORCIO GARCIA & RAL S.A.C.', 'TRANSPORTISTA', NULL::varchar, NULL::varchar, 'NO_MUNICIPAL'),
  ('20601250005', 'ALL TRUCKS SAC', 'TRANSPORTISTA', NULL::varchar, NULL::varchar, 'NO_MUNICIPAL'),
  ('20553171602', 'MAQUINARIAS Y TRANSPORTE DE AGREGADOS RIVERA GOMEZ S.A.C.', 'TRANSPORTISTA', NULL::varchar, NULL::varchar, 'NO_MUNICIPAL'),
  ('PENDIENTE01', 'MP RECICLA S.A.C.', 'VALORIZACION', 'N° 006-2019-PRODUCE/DVMYPE-I/DGAAMI', 'Parcela N 48 int. 01 c.c. Santa Rosa de Collanac, Cieneguilla, Lima', 'NO_MUNICIPAL'),
  ('PENDIENTE02', 'ASOCIACIÓN DE ESTUDIOS ECOLÓGICOS E INVESTIGACIÓN CIVIL SOSTENIBLE - ADEICS', 'DISPOSICION_FINAL', 'N° 092-2022-VIVIENDA/VMCS-DGAA', 'Talud colindante al Circuito de Playas Costa Verde - Tramo San Miguel, Lima', 'MUNICIPAL'),
  ('PENDIENTE03', 'MINERA JICAMARCA EIRL', 'DISPOSICION_FINAL', 'N° 0082-2012-GRL-GRDE-DREM', 'Mz DA1-1 Lt 1 Sector Unión Bellavista Anexo 22 Jicamarca, San Antonio, Huarochirí, Lima', 'NO_MUNICIPAL')
) AS v(ruc, razon_social, tipo_operador, numero_registro_minam, direccion, ambito_gestion)
WHERE NOT EXISTS (SELECT 1 FROM ss_residuo_eo_rs WHERE ruc = v.ruc);

COMMIT;

-- ============================================================================
-- Verificación (correr después; no modifica nada)
-- ============================================================================
-- SELECT ruc, razon_social, tipo_operador, numero_registro_minam, ambito_gestion
-- FROM ss_residuo_eo_rs
-- ORDER BY razon_social;
--
-- PENDIENTE: el Excel original no traía columna RUC para MP RECICLA, ADEICS ni
-- MINERA JICAMARCA (solo para los 3 transportistas), así que quedaron con un
-- placeholder literal 'PENDIENTE01/02/03' en vez de un número inventado, para
-- no cargar un RUC falso que pueda coincidir con el de otra empresa real.
-- Reemplazar con el RUC real en cuanto se consiga, por ejemplo:
--   UPDATE ss_residuo_eo_rs SET ruc = '20xxxxxxxxx' WHERE ruc = 'PENDIENTE01'; -- MP RECICLA
--   UPDATE ss_residuo_eo_rs SET ruc = '20xxxxxxxxx' WHERE ruc = 'PENDIENTE02'; -- ADEICS
--   UPDATE ss_residuo_eo_rs SET ruc = '20xxxxxxxxx' WHERE ruc = 'PENDIENTE03'; -- MINERA JICAMARCA
