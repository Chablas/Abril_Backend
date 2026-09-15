-- SOLO LECTURA. Para todos los trabajadores ya corregidos en archivo_url
-- (las 3 tandas: Aquitania x27, Bahia de Oro/Lares x10), compara la
-- vigencia actual en ss_hab_trabajador contra la vigencia real de la
-- póliza cuyo archivo_url restauramos — para detectar si quedó desfasada
-- (como pasó con Alan Oceda).

WITH corregidos (dni, archivo_correcto) AS (
    VALUES
    -- Aquitania (36-lote original, 7 de Aquitania)
    ('70931762', 'habilitacion/sctr/20260602_Oficina_Central_enero_2027_VidaLey_Aquitania_Inmobiliaria_S.A.C_20260302_124333.pdf'),
    ('72839787', 'habilitacion/sctr/20260602_Oficina_Central_enero_2027_VidaLey_Aquitania_Inmobiliaria_S.A.C_20260302_124155.pdf'),
    ('73632502', 'habilitacion/sctr/20260602_Oficina_Central_enero_2027_VidaLey_Aquitania_Inmobiliaria_S.A.C_20260302_124352.pdf'),
    ('75264771', 'habilitacion/sctr/20260602_Oficina_Central_enero_2027_VidaLey_Aquitania_Inmobiliaria_S.A.C_20260302_125137.pdf'),
    ('72620469', 'habilitacion/sctr/20260602_Oficina_Central_enero_2027_VidaLey_Aquitania_Inmobiliaria_S.A.C_20260302_125137.pdf'),
    ('74077542', 'habilitacion/sctr/20260602_Oficina_Central_enero_2027_VidaLey_Aquitania_Inmobiliaria_S.A.C_20260302_124155.pdf'),
    ('46279306', 'habilitacion/sctr/20260602_Oficina_Central_enero_2027_VidaLey_Aquitania_Inmobiliaria_S.A.C_20260520_153151.pdf'),
    -- Aquitania (10 faltantes)
    ('71386365', 'habilitacion/sctr/20260708_136115_(5)_julio_2026.pdf'),
    ('71387814', 'habilitacion/sctr/20260608_aquitania136115_(1)._MANUEL_ASENCIOS.pdf'),
    ('70052564', 'habilitacion/sctr/20260707_136115_(4)._BACA_ARIAS_SCOOT.pdf'),
    ('76320703', 'habilitacion/sctr/20260708_constancia._josselyn_comeca.pdf'),
    ('71985018', 'habilitacion/sctr/20260805_AQUITANIA_136115_(2)._Ana_lucia.pdf'),
    ('71950632', 'habilitacion/sctr/20260708_136115_(5)_julio_2026.pdf'),
    ('10684733', 'habilitacion/sctr/20260708_136115_(5)_julio_2026.pdf'),
    ('73784920', 'habilitacion/sctr/20260708_136115_(3)._JUNIO.pdf'),
    ('10723799', 'habilitacion/sctr/20260608_136115_(3)._JUNIO.pdf'),
    ('10684905', 'habilitacion/sctr/20260608_136115_(3)._JUNIO.pdf'),
    -- Bahia de Oro / Lares (10)
    ('70982675', 'habilitacion/sctr/20260811_constancia_ABAD_NAUTO_JOHAN.pdf'),
    ('10713842', 'habilitacion/sctr/20260701_BAHIA130356_(2)._RENOVACION.pdf'),
    ('71660883', 'habilitacion/sctr/20260701_BAHIA130356_(2)._RENOVACION.pdf'),
    ('71542068', 'habilitacion/sctr/20260710_109593_(6)._marco_collantes.pdf'),
    ('74031156', 'habilitacion/sctr/20260701_BAHIA130356_(2)._RENOVACION.pdf'),
    ('76325920', 'habilitacion/sctr/20260701_BAHIA130356_(2)._RENOVACION.pdf'),
    ('70124175', 'habilitacion/sctr/20260701_BAHIA130356_(2)._RENOVACION.pdf'),
    ('42377772', 'habilitacion/sctr/20260701_BAHIA130356_(2)._RENOVACION.pdf'),
    ('46579206', 'habilitacion/sctr/20260701_BAHIA130356_(2)._RENOVACION.pdf'),
    ('70565134', 'habilitacion/sctr/20260701_BAHIA130356_(2)._RENOVACION.pdf')
)
SELECT
    corr.dni,
    per.full_name AS nombre,
    h.id AS hab_trabajador_id,
    h.archivo_url,
    h.vigencia AS vigencia_actual_en_hab,
    s.vigencia AS vigencia_real_de_la_poliza,
    s.id AS poliza_id,
    (h.vigencia IS DISTINCT FROM s.vigencia) AS vigencia_desfasada
FROM corregidos corr
JOIN person per ON per.document_identity_code = corr.dni
JOIN workers w ON w.person_id = per.person_id
JOIN worker_vinculaciones vinc ON vinc.worker_id = w.id AND vinc.fecha_fin IS NULL
JOIN ss_item_trabajador it ON it.es_sctr_vidaley = true AND it.activo = true AND it.nombre ILIKE '%Vida%'
JOIN ss_hab_trabajador h ON h.worker_id = w.id AND h.item_id = it.id
LEFT JOIN ss_sctr_vidaley s ON s.tipo = 'VIDA_LEY' AND s.archivo_url = corr.archivo_correcto
ORDER BY vigencia_desfasada DESC, corr.dni;
