-- Catálogo de los 9 pasos del flujo de Contratos (Unidad de Proyectos) — paralelo a
-- project_sub_contractor_status (Adjudicaciones), con un solo ajuste: el paso 8 notifica al
-- correo de Unidad de Proyectos (unidadproyectosnm@abril.pe) en vez de a Staff de Obra.
-- Requiere que project_contract_status ya exista (migración EF AddContratosFeature /
-- Migrations/20260929230825_AddContratosFeature.cs).
--
-- Idempotente por descripción — el orden de inserción define el id (1..9) porque la tabla
-- arranca vacía; en un re-run no se duplica nada.

INSERT INTO project_contract_status (project_contract_status_description)
SELECT 'Cotización / cuadro comparativo'
WHERE NOT EXISTS (SELECT 1 FROM project_contract_status WHERE project_contract_status_description = 'Cotización / cuadro comparativo');

INSERT INTO project_contract_status (project_contract_status_description)
SELECT 'Datos del contrato'
WHERE NOT EXISTS (SELECT 1 FROM project_contract_status WHERE project_contract_status_description = 'Datos del contrato');

INSERT INTO project_contract_status (project_contract_status_description)
SELECT 'Generación de documentos'
WHERE NOT EXISTS (SELECT 1 FROM project_contract_status WHERE project_contract_status_description = 'Generación de documentos');

INSERT INTO project_contract_status (project_contract_status_description)
SELECT 'Envío al contratista'
WHERE NOT EXISTS (SELECT 1 FROM project_contract_status WHERE project_contract_status_description = 'Envío al contratista');

INSERT INTO project_contract_status (project_contract_status_description)
SELECT 'Llegada a Oficina Central'
WHERE NOT EXISTS (SELECT 1 FROM project_contract_status WHERE project_contract_status_description = 'Llegada a Oficina Central');

INSERT INTO project_contract_status (project_contract_status_description)
SELECT 'Procesos de firma'
WHERE NOT EXISTS (SELECT 1 FROM project_contract_status WHERE project_contract_status_description = 'Procesos de firma');

INSERT INTO project_contract_status (project_contract_status_description)
SELECT 'Contrato firmado escaneado'
WHERE NOT EXISTS (SELECT 1 FROM project_contract_status WHERE project_contract_status_description = 'Contrato firmado escaneado');

INSERT INTO project_contract_status (project_contract_status_description)
SELECT 'Notificación a Unidad de Proyectos'
WHERE NOT EXISTS (SELECT 1 FROM project_contract_status WHERE project_contract_status_description = 'Notificación a Unidad de Proyectos');

INSERT INTO project_contract_status (project_contract_status_description)
SELECT 'Cierre'
WHERE NOT EXISTS (SELECT 1 FROM project_contract_status WHERE project_contract_status_description = 'Cierre');

-- Verificación de solo lectura después de correr (D1):
-- SELECT * FROM project_contract_status ORDER BY project_contract_status_id;
