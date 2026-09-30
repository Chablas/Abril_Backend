-- ============================================================================
-- Documentos de los propietarios: carpeta de SharePoint de DEV y DEMO
-- ============================================================================
-- SOLO DEV Y DEMO. Prod va con 20260930_PropietarioDocumentoFolder_Prod.sql. La base no sabe en
-- qué ambiente está (las tres se llaman abril), así que lo decide el archivo que se corre.
--
-- Carpeta «App Convivir - Documentos de Propietarios» de la biblioteca Desarrollo (sitio
-- bibliotecanm), para que las pruebas no dejen archivos en la biblioteca de producción. Al subir,
-- el backend crea dentro {Proyecto}/{Torre - Dpto} - {DNI} {Nombre}/.
--
-- Requiere 20260930_PropietarioDocumentos.sql (aborta si falta la tabla). Idempotente: si esta
-- carpeta ya es la vigente no cambia nada; si hay otra vigente, la da de baja (state = false) y
-- registra esta. Por eso hay que volver a correrlo después de clonar la base desde prod: el clon
-- trae la carpeta de producción. Los documentos ya subidos no se mueven (cada uno guarda su
-- drive_id + item_id).
-- ============================================================================

BEGIN;

DO $$
BEGIN
    IF to_regclass('public.propietario_documento_folder') IS NULL THEN
        RAISE EXCEPTION 'Falta correr antes 20260930_PropietarioDocumentos.sql: no existe la tabla propietario_documento_folder.';
    END IF;
END $$;

UPDATE propietario_documento_folder
   SET state             = false,
       updated_date_time = now()
 WHERE state AND active
   AND link_url <> 'https://abrilinmob.sharepoint.com/sites/bibliotecanm/Desarrollo/Forms/AllItems.aspx?id=%2Fsites%2Fbibliotecanm%2FDesarrollo%2FApp%20Convivir%20%2D%20Documentos%20de%20Propietarios&viewid=087a516f%2Da398%2D433d%2Db208%2Df4aa51591a4d';

INSERT INTO propietario_documento_folder (link_url, folder_name)
SELECT 'https://abrilinmob.sharepoint.com/sites/bibliotecanm/Desarrollo/Forms/AllItems.aspx?id=%2Fsites%2Fbibliotecanm%2FDesarrollo%2FApp%20Convivir%20%2D%20Documentos%20de%20Propietarios&viewid=087a516f%2Da398%2D433d%2Db208%2Df4aa51591a4d',
       'Desarrollo / App Convivir - Documentos de Propietarios'
WHERE NOT EXISTS (SELECT 1 FROM propietario_documento_folder WHERE state AND active);

COMMIT;

-- Verificación: una sola fila, «Desarrollo / App Convivir - Documentos de Propietarios».
SELECT propietario_documento_folder_id, folder_name, link_url, created_date_time
FROM propietario_documento_folder
WHERE state AND active;
