-- ⚠️ DESTRUCTIVO — borra TODOS los ATS existentes y sus PETAR asociados (confirmado explícito
-- del usuario: "borra todos los ATS de prueba, todos son prueba hasta ahora"). NO toca
-- ss_ats_autorizacion_permiso ni ss_ats_consentimiento (son configuración del trabajador, no
-- instancias de ATS) ni ningún catálogo (peligros/riesgos/plantillas/controles).

-- 1) Verificación ANTES de borrar — revisa estos números antes de correr el DELETE de abajo.
SELECT
    (SELECT count(*) FROM ss_ats) AS total_ats,
    (SELECT count(*) FROM ss_petar) AS total_petar,
    (SELECT count(*) FROM ss_ats_paso_seleccionado) AS total_pasos,
    (SELECT count(*) FROM ss_ats_epp_seleccionado) AS total_epps,
    (SELECT count(*) FROM ss_ats_herramienta_seleccionada) AS total_herramientas,
    (SELECT count(*) FROM ss_ats_riesgo_detalle) AS total_riesgos_detalle,
    (SELECT count(*) FROM ss_ats_audit_log) AS total_audit;

-- 2) DELETE — descomenta y corre solo si los números de arriba tienen sentido (son tus pruebas).
/*
DELETE FROM ss_petar_item_respuesta WHERE petar_id IN (SELECT id FROM ss_petar);
DELETE FROM ss_petar_audit_log WHERE petar_id IN (SELECT id FROM ss_petar);
DELETE FROM ss_petar_izaje_grua WHERE petar_id IN (SELECT id FROM ss_petar);
DELETE FROM ss_petar;

DELETE FROM ss_ats_audit_log;
DELETE FROM ss_ats_riesgo_detalle;
DELETE FROM ss_ats_herramienta_seleccionada;
DELETE FROM ss_ats_epp_seleccionado;
DELETE FROM ss_ats_paso_seleccionado;
DELETE FROM ss_ats;
*/
