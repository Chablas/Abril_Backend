-- Restaura el archivo_url correcto en ss_hab_trabajador (ítem "Vida ley")
-- para los trabajadores donde se confirmó que el archivo vigente es en
-- realidad un documento SCTR (u otro) cargado por error, mientras que existe
-- un archivo Vida Ley legítimo en una póliza anterior (ss_sctr_vidaley).
--
-- NO toca estado ni vigencia — solo el archivo_url, que es lo único que
-- estaba mal. Corre primero el SELECT de verificación (parte 1), revisa que
-- "archivo_actual" coincida con lo esperado antes de tocar nada, y recién
-- después corre la parte 2 (UPDATE) dentro de la misma transacción.
--
-- Excluidos de este script (requieren confirmación manual porque el
-- "archivo recuperable" pertenece a una empresa distinta a la del
-- trabajador — la heurística puede estar equivocada en estos 6 casos):
--   46557961 CANALES GELDRES ALFREDO (Seshat / archivo nombrado Salerno)
--   70274904 HIVET JURIETA MAMANI CONDORI (Seshat / archivo nombrado Lares)
--   73381575 SILVERA DIAZ SHIRLEY MILAGROS (Seshat / archivo nombrado Thabit)
--   76841365 VICTOR ALEJANDRO COLONIO BARRUETO (Seshat / archivo nombrado Thabit)
--   70417362 HERQUINIO TURIN JOSE LUIS (sin empresa vinculada activa)
--   76912235 JOSSELYN ORIANA FALLA ÑAHUERO (sin empresa vinculada activa)

BEGIN;

-- ============================================================
-- PARTE 1: SOLO LECTURA — verificar antes de aplicar
-- ============================================================
WITH correcciones (dni, archivo_correcto) AS (
    VALUES
    ('70931762', 'habilitacion/sctr/20260602_Oficina_Central_enero_2027_VidaLey_Aquitania_Inmobiliaria_S.A.C_20260302_124333.pdf'),
    ('72839787', 'habilitacion/sctr/20260602_Oficina_Central_enero_2027_VidaLey_Aquitania_Inmobiliaria_S.A.C_20260302_124155.pdf'),
    ('73632502', 'habilitacion/sctr/20260602_Oficina_Central_enero_2027_VidaLey_Aquitania_Inmobiliaria_S.A.C_20260302_124352.pdf'),
    ('75264771', 'habilitacion/sctr/20260602_Oficina_Central_enero_2027_VidaLey_Aquitania_Inmobiliaria_S.A.C_20260302_125137.pdf'),
    ('72620469', 'habilitacion/sctr/20260602_Oficina_Central_enero_2027_VidaLey_Aquitania_Inmobiliaria_S.A.C_20260302_125137.pdf'),
    ('74077542', 'habilitacion/sctr/20260602_Oficina_Central_enero_2027_VidaLey_Aquitania_Inmobiliaria_S.A.C_20260302_124155.pdf'),
    ('46279306', 'habilitacion/sctr/20260602_Oficina_Central_enero_2027_VidaLey_Aquitania_Inmobiliaria_S.A.C_20260520_153151.pdf'),
    ('75519110', 'habilitacion/sctr/20260602_Oficina_Central_marzo_2027_VidaLey_Salerno_Inmobiliaria_SAC_20260331_122146.pdf'),
    ('71418677', 'habilitacion/sctr/20260602_Oficina_Central_junio_2026_VidaLey_Bahia_de_oro_Inmobiliaria_S.A.C_20260505_092223.pdf'),
    ('07528011', 'habilitacion/sctr/20260602_Oficina_Central_junio_2026_VidaLey_Bahia_de_oro_Inmobiliaria_S.A.C_20260406_112141.pdf'),
    ('47579621', 'habilitacion/sctr/20260602_Oficina_Central_junio_2026_VidaLey_Bahia_de_oro_Inmobiliaria_S.A.C_20260520_104616.pdf'),
    ('76099108', 'habilitacion/sctr/20260602_Oficina_Central_junio_2026_VidaLey_Bahia_de_oro_Inmobiliaria_S.A.C_20250901_173338.pdf'),
    ('71250519', 'habilitacion/sctr/20260602_Staff_Obra_junio_2026_VidaLey_Bahia_de_oro_Inmobiliaria_S.A.C_20260205_101127.pdf'),
    ('72288870', 'habilitacion/sctr/20260602_Staff_junio_2026_VidaLey_Bahia_de_oro_Inmobiliaria_S.A.C_20251217_121529.pdf'),
    ('03231819', 'habilitacion/sctr/20260525_VIDALEY_CAMELIA_MAYO.pdf'),
    ('73429505', 'habilitacion/sctr/20260602_Staff_junio_2026_VidaLey_Bahia_de_oro_Inmobiliaria_S.A.C_20251021_102636.pdf'),
    ('74162750', 'habilitacion/sctr/20260602_Oficina_Central_junio_2026_VidaLey_Bahia_de_oro_Inmobiliaria_S.A.C_20260520_092142.pdf'),
    ('49051108', 'habilitacion/sctr/20260602_Oficina__Central_agosto_2026_VidaLey_Corporación_Inmobiliaria_Nérida_María_S.A.C_20250904_090648.pdf'),
    ('07214887', 'habilitacion/sctr/20260602_Oficina__Central_agosto_2026_VidaLey_Corporación_Inmobiliaria_Nérida_María_S.A.C_20250904_090648.pdf'),
    ('10536252', 'habilitacion/sctr/20260602_Oficina__Central_agosto_2026_VidaLey_Corporación_Inmobiliaria_Nérida_María_S.A.C_20250904_090648.pdf'),
    ('47098320', 'habilitacion/sctr/20260602_Oficina__Central_agosto_2026_VidaLey_Corporación_Inmobiliaria_Nérida_María_S.A.C_20250904_090648.pdf'),
    ('73228666', 'habilitacion/sctr/20260602_Oficina__Central_agosto_2026_VidaLey_Corporación_Inmobiliaria_Nérida_María_S.A.C_20250904_090648.pdf'),
    ('43102350', 'habilitacion/sctr/20260602_Oficina__Central_agosto_2026_VidaLey_Corporación_Inmobiliaria_Nérida_María_S.A.C_20250904_090648.pdf'),
    ('72127330', 'habilitacion/sctr/20260602_Staff_agosto_2026_VidaLey_Lares_Inmobiliaria_S.A.C_20260224_145838.pdf'),
    ('75390759', 'habilitacion/sctr/20260602_Staff_agosto_2026_VidaLey_Lares_Inmobiliaria_S.A.C_20250902_102831.pdf'),
    ('42347491', 'habilitacion/sctr/20260602_Staff_agosto_2026_VidaLey_Lares_Inmobiliaria_S.A.C_20250902_102831.pdf'),
    ('73681859', 'habilitacion/sctr/20260602_Staff_agosto_2026_VidaLey_Lares_Inmobiliaria_S.A.C_20250902_102831.pdf'),
    ('46127713', 'habilitacion/sctr/20260602_Staff_agosto_2026_VidaLey_Lares_Inmobiliaria_S.A.C_20250902_102831.pdf'),
    ('45987227', 'habilitacion/sctr/20260602_Staff_agosto_2026_VidaLey_Lares_Inmobiliaria_S.A.C_20250902_102831.pdf'),
    ('45447033', 'habilitacion/sctr/20260602_Oficina_Central_agosto_2026_VidaLey_Lares_Inmobiliaria_S.A.C_20260520_144202.pdf'),
    ('73419671', 'habilitacion/sctr/20260602_Oficina_Central_agosto_2026_VidaLey_Lares_Inmobiliaria_S.A.C_20260520_121343.pdf'),
    ('73865634', 'habilitacion/sctr/20260602_Oficina_Central_agosto_2026_VidaLey_Lares_Inmobiliaria_S.A.C_20260520_144202.pdf'),
    ('75336228', 'habilitacion/sctr/20260602_Oficina_Central_agosto_2026_VidaLey_Lares_Inmobiliaria_S.A.C_20260520_144202.pdf'),
    ('70477135', 'habilitacion/sctr/20260602_Oficina_Centralagosto_2026_VidaLey_Neo_Inversiones_Inmobiliarias_S.A.C_2025.pdf'),
    ('72262939', 'habilitacion/sctr/20260525_VIDALEY_CAMELIA_MAYO.pdf'),
    ('46638649', 'habilitacion/sctr/20260525_VIDALEY_CAMELIA_MAYO.pdf')
)
SELECT
    corr.dni,
    per.full_name AS nombre,
    h.id AS hab_trabajador_id,
    h.archivo_url AS archivo_actual,
    corr.archivo_correcto,
    (h.archivo_url = corr.archivo_correcto) AS ya_esta_correcto
FROM correcciones corr
JOIN person per ON per.document_identity_code = corr.dni
JOIN workers w ON w.person_id = per.person_id
JOIN ss_item_trabajador it ON it.es_sctr_vidaley = true AND it.activo = true AND it.nombre ILIKE '%Vida%'
JOIN ss_hab_trabajador h ON h.worker_id = w.id AND h.item_id = it.id
ORDER BY corr.dni;

-- Revisa el resultado de arriba. Si "archivo_actual" coincide con lo que
-- esperabas ver reemplazado (el SCTR/otro mal cargado) y "ya_esta_correcto"
-- es false en todos, puedes seguir a la Parte 2. Si algo no cuadra, no
-- sigas — ROLLBACK y avísame.

-- ============================================================
-- PARTE 2: UPDATE real (dentro de la misma transacción)
-- Descomenta este bloque solo después de validar la Parte 1.
-- ============================================================
/*
WITH correcciones (dni, archivo_correcto) AS (
    VALUES
    ('70931762', 'habilitacion/sctr/20260602_Oficina_Central_enero_2027_VidaLey_Aquitania_Inmobiliaria_S.A.C_20260302_124333.pdf'),
    ('72839787', 'habilitacion/sctr/20260602_Oficina_Central_enero_2027_VidaLey_Aquitania_Inmobiliaria_S.A.C_20260302_124155.pdf'),
    ('73632502', 'habilitacion/sctr/20260602_Oficina_Central_enero_2027_VidaLey_Aquitania_Inmobiliaria_S.A.C_20260302_124352.pdf'),
    ('75264771', 'habilitacion/sctr/20260602_Oficina_Central_enero_2027_VidaLey_Aquitania_Inmobiliaria_S.A.C_20260302_125137.pdf'),
    ('72620469', 'habilitacion/sctr/20260602_Oficina_Central_enero_2027_VidaLey_Aquitania_Inmobiliaria_S.A.C_20260302_125137.pdf'),
    ('74077542', 'habilitacion/sctr/20260602_Oficina_Central_enero_2027_VidaLey_Aquitania_Inmobiliaria_S.A.C_20260302_124155.pdf'),
    ('46279306', 'habilitacion/sctr/20260602_Oficina_Central_enero_2027_VidaLey_Aquitania_Inmobiliaria_S.A.C_20260520_153151.pdf'),
    ('75519110', 'habilitacion/sctr/20260602_Oficina_Central_marzo_2027_VidaLey_Salerno_Inmobiliaria_SAC_20260331_122146.pdf'),
    ('71418677', 'habilitacion/sctr/20260602_Oficina_Central_junio_2026_VidaLey_Bahia_de_oro_Inmobiliaria_S.A.C_20260505_092223.pdf'),
    ('07528011', 'habilitacion/sctr/20260602_Oficina_Central_junio_2026_VidaLey_Bahia_de_oro_Inmobiliaria_S.A.C_20260406_112141.pdf'),
    ('47579621', 'habilitacion/sctr/20260602_Oficina_Central_junio_2026_VidaLey_Bahia_de_oro_Inmobiliaria_S.A.C_20260520_104616.pdf'),
    ('76099108', 'habilitacion/sctr/20260602_Oficina_Central_junio_2026_VidaLey_Bahia_de_oro_Inmobiliaria_S.A.C_20250901_173338.pdf'),
    ('71250519', 'habilitacion/sctr/20260602_Staff_Obra_junio_2026_VidaLey_Bahia_de_oro_Inmobiliaria_S.A.C_20260205_101127.pdf'),
    ('72288870', 'habilitacion/sctr/20260602_Staff_junio_2026_VidaLey_Bahia_de_oro_Inmobiliaria_S.A.C_20251217_121529.pdf'),
    ('03231819', 'habilitacion/sctr/20260525_VIDALEY_CAMELIA_MAYO.pdf'),
    ('73429505', 'habilitacion/sctr/20260602_Staff_junio_2026_VidaLey_Bahia_de_oro_Inmobiliaria_S.A.C_20251021_102636.pdf'),
    ('74162750', 'habilitacion/sctr/20260602_Oficina_Central_junio_2026_VidaLey_Bahia_de_oro_Inmobiliaria_S.A.C_20260520_092142.pdf'),
    ('49051108', 'habilitacion/sctr/20260602_Oficina__Central_agosto_2026_VidaLey_Corporación_Inmobiliaria_Nérida_María_S.A.C_20250904_090648.pdf'),
    ('07214887', 'habilitacion/sctr/20260602_Oficina__Central_agosto_2026_VidaLey_Corporación_Inmobiliaria_Nérida_María_S.A.C_20250904_090648.pdf'),
    ('10536252', 'habilitacion/sctr/20260602_Oficina__Central_agosto_2026_VidaLey_Corporación_Inmobiliaria_Nérida_María_S.A.C_20250904_090648.pdf'),
    ('47098320', 'habilitacion/sctr/20260602_Oficina__Central_agosto_2026_VidaLey_Corporación_Inmobiliaria_Nérida_María_S.A.C_20250904_090648.pdf'),
    ('73228666', 'habilitacion/sctr/20260602_Oficina__Central_agosto_2026_VidaLey_Corporación_Inmobiliaria_Nérida_María_S.A.C_20250904_090648.pdf'),
    ('43102350', 'habilitacion/sctr/20260602_Oficina__Central_agosto_2026_VidaLey_Corporación_Inmobiliaria_Nérida_María_S.A.C_20250904_090648.pdf'),
    ('72127330', 'habilitacion/sctr/20260602_Staff_agosto_2026_VidaLey_Lares_Inmobiliaria_S.A.C_20260224_145838.pdf'),
    ('75390759', 'habilitacion/sctr/20260602_Staff_agosto_2026_VidaLey_Lares_Inmobiliaria_S.A.C_20250902_102831.pdf'),
    ('42347491', 'habilitacion/sctr/20260602_Staff_agosto_2026_VidaLey_Lares_Inmobiliaria_S.A.C_20250902_102831.pdf'),
    ('73681859', 'habilitacion/sctr/20260602_Staff_agosto_2026_VidaLey_Lares_Inmobiliaria_S.A.C_20250902_102831.pdf'),
    ('46127713', 'habilitacion/sctr/20260602_Staff_agosto_2026_VidaLey_Lares_Inmobiliaria_S.A.C_20250902_102831.pdf'),
    ('45987227', 'habilitacion/sctr/20260602_Staff_agosto_2026_VidaLey_Lares_Inmobiliaria_S.A.C_20250902_102831.pdf'),
    ('45447033', 'habilitacion/sctr/20260602_Oficina_Central_agosto_2026_VidaLey_Lares_Inmobiliaria_S.A.C_20260520_144202.pdf'),
    ('73419671', 'habilitacion/sctr/20260602_Oficina_Central_agosto_2026_VidaLey_Lares_Inmobiliaria_S.A.C_20260520_121343.pdf'),
    ('73865634', 'habilitacion/sctr/20260602_Oficina_Central_agosto_2026_VidaLey_Lares_Inmobiliaria_S.A.C_20260520_144202.pdf'),
    ('75336228', 'habilitacion/sctr/20260602_Oficina_Central_agosto_2026_VidaLey_Lares_Inmobiliaria_S.A.C_20260520_144202.pdf'),
    ('70477135', 'habilitacion/sctr/20260602_Oficina_Centralagosto_2026_VidaLey_Neo_Inversiones_Inmobiliarias_S.A.C_2025.pdf'),
    ('72262939', 'habilitacion/sctr/20260525_VIDALEY_CAMELIA_MAYO.pdf'),
    ('46638649', 'habilitacion/sctr/20260525_VIDALEY_CAMELIA_MAYO.pdf')
),
objetivo AS (
    SELECT h.id AS hab_trabajador_id, corr.archivo_correcto
    FROM correcciones corr
    JOIN person per ON per.document_identity_code = corr.dni
    JOIN workers w ON w.person_id = per.person_id
    JOIN ss_item_trabajador it ON it.es_sctr_vidaley = true AND it.activo = true AND it.nombre ILIKE '%Vida%'
    JOIN ss_hab_trabajador h ON h.worker_id = w.id AND h.item_id = it.id
)
UPDATE ss_hab_trabajador h
SET archivo_url = o.archivo_correcto,
    updated_at = now()
FROM objetivo o
WHERE h.id = o.hab_trabajador_id;
*/

-- Después de correr la Parte 2, revisa el conteo de filas afectadas
-- (debe ser 36) y recién ahí:
COMMIT;
-- o, si algo salió mal:
-- ROLLBACK;
