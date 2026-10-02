-- Reemplaza el texto de los 5 criterios de "Oficina Técnica" en la evaluación de contratistas
-- por los del CSV oficial (CriterioEvalContratista.csv). Mismas filas, mismo orden: no se
-- agregan ni se desactivan. Es seguro porque ev_evaluacion_contratista_detalle guarda su
-- propia copia del texto (columna criterio), así que el historial no cambia.
-- area_nombre y puesto_evaluador no se tocan (siguen coincidiendo con ResolverArea).

-- 1) Verificar antes (esperado: 5 filas activas, orden 1..5)
SELECT id, criterio, orden, activo FROM ev_contratista_plantilla
WHERE area_nombre = 'Oficina Técnica' AND activo ORDER BY orden;

BEGIN;

UPDATE ev_contratista_plantilla SET criterio = 'La empresa cuenta con los recursos económicos para culminar la obra dentro del presupuesto asignado'
WHERE area_nombre = 'Oficina Técnica' AND activo AND orden = 1;

UPDATE ev_contratista_plantilla SET criterio = 'La empresa maneja adecuadamente los cambios en el alcance del trabajo sin solicitar adicionales fuera de mercado.'
WHERE area_nombre = 'Oficina Técnica' AND activo AND orden = 2;

UPDATE ev_contratista_plantilla SET criterio = 'La empresa presenta su documentación de facturación oportunamente.'
WHERE area_nombre = 'Oficina Técnica' AND activo AND orden = 3;

UPDATE ev_contratista_plantilla SET criterio = 'La empresa muestra actitud de colaboración para conciliar penalidades/sobrecostos.'
WHERE area_nombre = 'Oficina Técnica' AND activo AND orden = 4;

UPDATE ev_contratista_plantilla SET criterio = 'La empresa cumple con levantar las observaciones por reprocesos asociados a su trabajo sin generar sobrecosto a ABRIL'
WHERE area_nombre = 'Oficina Técnica' AND activo AND orden = 5;

-- 2) Verificar antes de confirmar (esperado: las mismas 5 filas con el texto nuevo)
SELECT id, criterio, orden, activo FROM ev_contratista_plantilla
WHERE area_nombre = 'Oficina Técnica' AND activo ORDER BY orden;

COMMIT;
