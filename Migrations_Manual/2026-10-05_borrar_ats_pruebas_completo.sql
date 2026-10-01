-- ⚠️ DESTRUCTIVO — borra TODOS los ATS, ATS grupales, PETAR y PETAR grupales existentes, con todo lo que cuelga de ellos
-- (confirmado por el usuario: "todo era prueba"). Reemplaza al script 2026-10-02_borrar_ats_prueba.sql, que no conocía
-- ATS grupales, observaciones, integrantes ni eventos.
--
-- NO se toca (es configuración, no instancias de ATS):
--   ss_ats_autorizacion_permiso  (autorización y firma digital de cada trabajador)
--   ss_ats_consentimiento        (consentimiento de imagen/geolocalización)
--   ss_ats_proyecto_qr           (QR fijo de cada obra: el que ya imprimiste sigue valiendo)
--   catálogos: peligros, riesgos, controles, plantillas, pasos, EPP, herramientas, tipos/ítems de PETAR
--
-- Los archivos (selfies, firmas, PDFs) ya subidos al almacenamiento NO se borran con este script: quedan huérfanos.
--
-- ═══ PASO 1 — mira estos números ANTES de borrar (deben ser solo tus pruebas) ═══
SELECT
    (SELECT count(*) FROM ss_ats)                    AS ats,
    (SELECT count(*) FROM ss_ats_grupo)              AS ats_grupales,
    (SELECT count(*) FROM ss_ats_observacion)        AS observaciones,
    (SELECT count(*) FROM ss_ats_grupo_integrante)   AS integrantes,
    (SELECT count(*) FROM ss_petar)                  AS petar,
    (SELECT count(*) FROM ss_petar_grupo)            AS petar_grupales,
    (SELECT count(*) FROM ss_documento_correlativo)  AS contadores_de_codigo;

-- ═══ PASO 2 — corre TODO este bloque junto (empieza con BEGIN: nada queda definitivo hasta el COMMIT) ═══
BEGIN;

-- PETAR (de lo más interno a lo más externo)
DELETE FROM ss_petar_grupo_item_respuesta;
DELETE FROM ss_petar_item_respuesta;
DELETE FROM ss_petar_audit_log;
DELETE FROM ss_petar_izaje_grua;
DELETE FROM ss_petar;
DELETE FROM ss_petar_grupo;

-- ATS grupal
DELETE FROM ss_ats_observacion;
DELETE FROM ss_ats_grupo_evento;
DELETE FROM ss_ats_grupo_integrante;
DELETE FROM ss_ats_grupo_riesgo_detalle;
DELETE FROM ss_ats_grupo_herramienta_seleccionada;
DELETE FROM ss_ats_grupo_epp_seleccionado;
DELETE FROM ss_ats_grupo_paso_seleccionado;

-- ATS individual
DELETE FROM ss_ats_audit_log;
DELETE FROM ss_ats_riesgo_detalle;
DELETE FROM ss_ats_herramienta_seleccionada;
DELETE FROM ss_ats_epp_seleccionado;
DELETE FROM ss_ats_paso_seleccionado;
DELETE FROM ss_ats;
DELETE FROM ss_ats_grupo;

-- Reinicia la numeración: el próximo ATS de cada obra vuelve a ser BUG-ATS-0001, etc.
DELETE FROM ss_documento_correlativo;

-- Comprobación dentro de la transacción: todo debe salir en 0
SELECT
    (SELECT count(*) FROM ss_ats)          AS ats,
    (SELECT count(*) FROM ss_ats_grupo)    AS ats_grupales,
    (SELECT count(*) FROM ss_petar)        AS petar,
    (SELECT count(*) FROM ss_petar_grupo)  AS petar_grupales;

-- ═══ PASO 3 — decide, ejecutando UNA de estas dos líneas por separado ═══
-- COMMIT;     -- confirma el borrado (definitivo)
-- ROLLBACK;   -- deshace todo, no se borró nada
