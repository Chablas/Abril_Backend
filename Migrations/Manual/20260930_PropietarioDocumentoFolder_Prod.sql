-- ============================================================================
-- Documentos de los propietarios: carpeta de SharePoint de PRODUCCIÓN
-- ============================================================================
-- SOLO PROD. Dev y demo van con 20260930_PropietarioDocumentoFolder_DevDemo.sql (biblioteca
-- Desarrollo). La base no sabe en qué ambiente está (las tres se llaman abril), así que lo decide
-- el archivo que se corre.
--
-- Biblioteca «Documentos de Propietarios» del sitio bibliotecanm. Al subir, el backend crea dentro
-- {Proyecto}/{Torre - Dpto} - {DNI} {Nombre}/. Son datos personales: la biblioteca tiene que tener
-- el acceso restringido (la intranet y la app los descargan por el backend, nadie necesita permiso
-- directo en SharePoint).
--
-- Requiere 20260930_PropietarioDocumentos.sql (aborta si falta la tabla). Idempotente: si esta
-- carpeta ya es la vigente no cambia nada; si hay otra vigente, la da de baja (state = false) y
-- registra esta. Los documentos ya subidos no se mueven: cada uno guarda su drive_id + item_id.
-- El backend la lee en cada guardado, así que da igual correrlo antes o después del deploy.
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
   AND link_url <> 'https://abrilinmob.sharepoint.com/sites/bibliotecanm/Documentos%20de%20Propietarios/Forms/AllItems.aspx';

INSERT INTO propietario_documento_folder (link_url, folder_name)
SELECT 'https://abrilinmob.sharepoint.com/sites/bibliotecanm/Documentos%20de%20Propietarios/Forms/AllItems.aspx',
       'Documentos de Propietarios'
WHERE NOT EXISTS (SELECT 1 FROM propietario_documento_folder WHERE state AND active);

COMMIT;

-- Verificación: una sola fila, «Documentos de Propietarios».
SELECT propietario_documento_folder_id, folder_name, link_url, created_date_time
FROM propietario_documento_folder
WHERE state AND active;
