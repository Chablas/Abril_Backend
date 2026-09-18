-- ============================================================================
-- EPP — Carga inicial desde "EPPS AUTORIZADO SSOMA ACTUALIZADO ACTUAL.xls"
-- (hoja "EPP Aprobado"): Familias, Ítems (con su descripción/comentario del
-- Excel) y Modelos/Marcas autorizados por cada ítem.
-- Ejecutar DESPUÉS de ssoma_epp_feature.sql y ssoma_epp_familia.sql.
-- Idempotente (NOT EXISTS por nombre en cada nivel).
-- ============================================================================
BEGIN;

-- ── Familias ──────────────────────────────────────────────────────────────
INSERT INTO ss_epp_familia (nombre, categoria_id, orden, activo, created_at)
SELECT v.nombre, c.id, 0, true, NOW()
FROM (VALUES
  ('Barbiquejo',                         'Cabeza'),
  ('Casco tipo jockey',                  'Cabeza'),
  ('Cortavientos para casco',            'Cabeza'),
  ('Tafilete',                           'Cabeza'),
  ('Gorra',                              'Cabeza'),
  ('Lentes de seguridad',                'Ojos y Rostro'),
  ('Sobrelentes',                        'Ojos y Rostro'),
  ('Careta facial para esmerilar',       'Ojos y Rostro'),
  ('Careta de soldar',                   'Ojos y Rostro'),
  ('Tapones de oído',                    'Auditiva'),
  ('Orejeras',                           'Auditiva'),
  ('Guantes anticorte',                  'Manos'),
  ('Guantes antivibratorio',             'Manos'),
  ('Guantes de cuero',                   'Manos'),
  ('Guantes de jebe',                    'Manos'),
  ('Guantes de nitrilo',                 'Manos'),
  ('Mangas de cuero',                    'Manos'),
  ('Botines cuero punta de acero',       'Pies'),
  ('Botas de jebe',                      'Pies'),
  ('Escarpines',                         'Pies'),
  ('Protector solar',                    'Cuerpo'),
  ('Blusa manga larga',                  'Cuerpo'),
  ('Camisa manga larga',                 'Cuerpo'),
  ('Camisaco',                           'Cuerpo'),
  ('Casaca',                             'Cuerpo'),
  ('Pantalón',                           'Cuerpo'),
  ('Polo',                               'Cuerpo'),
  ('Traje Tyvek',                        'Cuerpo'),
  ('Chaleco',                            'Cuerpo'),
  ('Hombreras',                          'Cuerpo'),
  ('Rodilleras',                         'Cuerpo'),
  ('Mandil',                             'Cuerpo'),
  ('Arnés de seguridad',                 'Altura'),
  ('Línea de anclaje',                   'Altura'),
  ('Línea de restricción',               'Altura'),
  ('Línea de conexión',                  'Altura'),
  ('Línea anticaída autorretráctil',     'Altura'),
  ('Línea de posición',                  'Altura'),
  ('Respirador cara completa',           'Respiratoria'),
  ('Filtros respirador',                 'Respiratoria'),
  ('Mascarilla',                         'Respiratoria')
) AS v(nombre, categoria_nombre_dummy)
JOIN ss_epp_categoria c ON c.nombre = v.categoria_nombre_dummy
WHERE NOT EXISTS (SELECT 1 FROM ss_epp_familia f WHERE f.nombre = v.nombre AND f.categoria_id = c.id);

-- ── Ítems (nombre técnico + comentario del Excel como descripción) ─────────
INSERT INTO ss_epp_item (nombre_tecnico, nombre_comercial, familia_id, descripcion, activo, created_at)
SELECT v.nombre_tecnico, v.nombre_tecnico, f.id, NULLIF(v.descripcion, ''), true, NOW()
FROM (VALUES
  ('Barbiquejo para casco',                                                        'Barbiquejo',                     'Cualquiera de las marcas es viable'),
  ('Casco tipo jockey blanco (staff)',                                             'Casco tipo jockey',              ''),
  ('Casco tipo jockey colores (obrero)',                                           'Casco tipo jockey',              ''),
  ('Cortavientos para casco',                                                      'Cortavientos para casco',        'Según estándar de logística'),
  ('Tafilete',                                                                     'Tafilete',                       ''),
  ('Gorra de soldador (chavito) jean',                                             'Gorra',                          'Según estándar de logística'),
  ('Lentes de seguridad claro/negros',                                             'Lentes de seguridad',            ''),
  ('Lentes de seguridad para amoladores de techo',                                 'Lentes de seguridad',            ''),
  ('Sobrelentes transparente',                                                     'Sobrelentes',                    ''),
  ('Sobrelentes oscuros',                                                          'Sobrelentes',                    ''),
  ('Careta facial para esmerilar',                                                 'Careta facial para esmerilar',   ''),
  ('Mica para careta facial para esmerilar',                                       'Careta facial para esmerilar',   ''),
  ('Careta cabezal amarillo con ratchet',                                          'Careta facial para esmerilar',   'Compra autorizada para post venta'),
  ('Careta de soldar adaptable al casco',                                          'Careta de soldar',               'Según estándar de logística'),
  ('Mica filtrante #11',                                                           'Careta de soldar',               'Según estándar de logística'),
  ('Tapones de oído con cuerda',                                                   'Tapones de oído',                ''),
  ('Orejeras para casco tipo copa adaptable al casco',                             'Orejeras',                       ''),
  ('Orejeras para casco staff',                                                    'Orejeras',                       ''),
  ('Orejeras para casco tipo copa tipo vincha',                                    'Orejeras',                       ''),
  ('Guantes anticorte látex',                                                      'Guantes anticorte',              ''),
  ('Guantes anticorte nitrilo',                                                    'Guantes anticorte',              ''),
  ('Guantes antivibratorio',                                                       'Guantes antivibratorio',         ''),
  ('Guantes cuero badana',                                                         'Guantes de cuero',               ''),
  ('Guantes de cuero caña larga',                                                  'Guantes de cuero',               'Según estándar de logística'),
  ('Guantes jebe negro',                                                           'Guantes de jebe',                'Según estándar de logística'),
  ('Guantes tipo Hycron nitrilo azules caña corta',                                'Guantes de nitrilo',             ''),
  ('Mangas de cuero para soldador',                                                'Mangas de cuero',                'Según estándar de logística'),
  ('Botines cuero c/punta acero económicas',                                       'Botines cuero punta de acero',   'Solo comprar uno de estos modelos'),
  ('Botines cuero c/punta acero staff',                                            'Botines cuero punta de acero',   ''),
  ('Botas jebe punta reforzada',                                                   'Botas de jebe',                  'Según estándar de logística'),
  ('Escarpines',                                                                   'Escarpines',                     'Según estándar de logística'),
  ('Bloqueador solar x 1 Lt',                                                      'Protector solar',                ''),
  ('Blusa manga larga color celeste',                                              'Blusa manga larga',              'Según estándar de logística'),
  ('Camisa manga larga color celeste c/logotipo',                                  'Camisa manga larga',             'Según estándar de logística'),
  ('Camisaco manga larga',                                                         'Camisaco',                       'Según estándar de logística'),
  ('Casaca impermeable',                                                           'Casaca',                         'Según estándar de logística'),
  ('Casaca de soldador',                                                           'Casaca',                         'Según estándar de logística'),
  ('Pantalón azul',                                                                'Pantalón',                       'Según estándar de logística'),
  ('Pantalón jean soldador',                                                       'Pantalón',                       'Según estándar de logística'),
  ('Polo azul s/logotipo de la empresa',                                           'Polo',                           'Según estándar de logística'),
  ('Traje Tyvek',                                                                  'Traje Tyvek',                    'No comprar SEGPRO'),
  ('Chaleco',                                                                      'Chaleco',                        'Según estándar de logística'),
  ('Hombreras de cuero',                                                           'Hombreras',                      'Según estándar de logística'),
  ('Rodilleras para trabajos en piso',                                             'Rodilleras',                     ''),
  ('Mandiles de soldador',                                                         'Mandil',                         'Según estándar de logística'),
  ('Arnés de seguridad de 3 anillos',                                              'Arnés de seguridad',             ''),
  ('Línea de anclaje doble con absorbedor de impactos',                            'Línea de anclaje',               ''),
  ('Línea de restricción doble regulable',                                         'Línea de restricción',           'Dar soporte con la cotización'),
  ('Línea de conexión 1.80m, cinta poliéster, gancho 3/4" y gancho 2 1/4"',        'Línea de conexión',              ''),
  ('Línea anticaída autorretráctil',                                               'Línea anticaída autorretráctil', ''),
  ('Línea de posición 3 puntas',                                                   'Línea de posición',              ''),
  ('Fullface (respirador cara completa)',                                         'Respirador cara completa',       ''),
  ('Filtro para gases',                                                            'Filtros respirador',             ''),
  ('Filtro para polvo',                                                            'Filtros respirador',             ''),
  ('Mascarilla 2 filtros para polvo',                                              'Mascarilla',                     ''),
  ('Mascarilla KN95',                                                              'Mascarilla',                     '')
) AS v(nombre_tecnico, familia_nombre, descripcion)
JOIN ss_epp_familia f ON f.nombre = v.familia_nombre
WHERE NOT EXISTS (SELECT 1 FROM ss_epp_item i WHERE i.nombre_tecnico = v.nombre_tecnico AND i.familia_id = f.id);

-- ── Modelos / marcas autorizadas por ítem ───────────────────────────────────
INSERT INTO ss_epp_modelo (epp_item_id, marca, modelo, codigo_referencia, activo, created_at)
SELECT i.id, v.marca, v.modelo, NULL, true, NOW()
FROM (VALUES
  ('Barbiquejo para casco',                                        'Clute',       'N/A'),
  ('Barbiquejo para casco',                                        'Bellsafe',    'N/A'),
  ('Barbiquejo para casco',                                        'A&A',         'N/A'),
  ('Casco tipo jockey blanco (staff)',                              '3M',          'H700'),
  ('Casco tipo jockey colores (obrero)',                            'Tridente',    'CAV 4'),
  ('Tafilete',                                                      'Tridente',    'C/Ratchet'),
  ('Lentes de seguridad claro/negros',                              'Clute',       'Astro'),
  ('Lentes de seguridad para amoladores de techo',                  'SteelPro',    'N/A'),
  ('Sobrelentes transparente',                                      'SPRO',        'Cosmic'),
  ('Sobrelentes oscuros',                                           'SPRO',        'Cosmic'),
  ('Careta facial para esmerilar',                                  'Clute',       'N/A'),
  ('Careta facial para esmerilar',                                  'Steelpro',    'N/A'),
  ('Careta facial para esmerilar',                                  'SegPro',      'V-200'),
  ('Mica para careta facial para esmerilar',                        'Clute',       'N/A'),
  ('Mica para careta facial para esmerilar',                        'Steelpro',    'N/A'),
  ('Mica para careta facial para esmerilar',                        'SegPro',      'V-200'),
  ('Careta cabezal amarillo con ratchet',                           'SegPro',      'N/A'),
  ('Tapones de oído con cuerda',                                    'SegPro',      'Confort 2000'),
  ('Tapones de oído con cuerda',                                    'Clute',       'Elite'),
  ('Orejeras para casco tipo copa adaptable al casco',              'Clute',       'Hunter'),
  ('Orejeras para casco staff',                                     '3M',          'N/A'),
  ('Orejeras para casco tipo copa tipo vincha',                     'Clute',       'EY2'),
  ('Guantes anticorte látex',                                       'SegPro',      'CUT 5'),
  ('Guantes anticorte látex',                                       'Clute',       'CUT 5'),
  ('Guantes anticorte nitrilo',                                     'Tecseg',      'TECLFEX CUT 5'),
  ('Guantes antivibratorio',                                        'Delta Pro',   'N/A'),
  ('Guantes cuero badana',                                          'SPRO',        'Badana'),
  ('Guantes tipo Hycron nitrilo azules caña corta',                 'SteelPro',    'Nitritop'),
  ('Botines cuero c/punta acero económicas',                        'SegPro',      'Harder'),
  ('Botines cuero c/punta acero económicas',                        'Clute',       'Nanterre'),
  ('Botines cuero c/punta acero económicas',                        'Nacional',    'Montero'),
  ('Botines cuero c/punta acero staff',                             'Boots Industrial', 'N/A'),
  ('Bloqueador solar x 1 Lt',                                       '3M',          'x 1 Lt'),
  ('Bloqueador solar x 1 Lt',                                       'Bahía',       'N/A'),
  ('Traje Tyvek',                                                   'ChemDefend',  'Series250'),
  ('Arnés de seguridad de 3 anillos',                               'SegPro',      'T5323'),
  ('Línea de anclaje doble con absorbedor de impactos',             'SegPro',      'TE 6106-1'),
  ('Línea de anclaje doble con absorbedor de impactos',             'Clute',       'Cly2gaa'),
  ('Línea de conexión 1.80m, cinta poliéster, gancho 3/4" y gancho 2 1/4"', 'Hauk', 'XN1G'),
  ('Línea de conexión 1.80m, cinta poliéster, gancho 3/4" y gancho 2 1/4"', 'Steelpro', 'N/A'),
  ('Línea anticaída autorretráctil',                                'DeltaPlus',   'MEDBloC AN13006c2'),
  ('Línea de posición 3 puntas',                                    'Hauk',        'Falta modelo'),
  ('Fullface (respirador cara completa)',                           'SteelPro',    'N/A'),
  ('Filtro para gases',                                             'SteelPro',    'ERGONIC F500VG A1E1'),
  ('Filtro para gases',                                             '3M',          'Cartucho 6003'),
  ('Filtro para polvo',                                             'SteelPro',    'ERGONIC F410C P3 R C'),
  ('Mascarilla 2 filtros para polvo',                                'SteelPro',   '100'),
  ('Mascarilla KN95',                                               'Clute',       'KN95'),
  ('Rodilleras para trabajos en piso',                              'SegPro',      'RSSP')
) AS v(item_nombre, marca, modelo)
JOIN ss_epp_item i ON i.nombre_tecnico = v.item_nombre
WHERE NOT EXISTS (
    SELECT 1 FROM ss_epp_modelo m WHERE m.epp_item_id = i.id AND m.marca = v.marca AND m.modelo = v.modelo
);

COMMIT;

-- ── Verificación ─────────────────────────────────────────────────────────────
SELECT c.nombre AS categoria, f.nombre AS familia, i.nombre_tecnico, COUNT(m.id) AS modelos
FROM ss_epp_item i
JOIN ss_epp_familia f ON f.id = i.familia_id
JOIN ss_epp_categoria c ON c.id = f.categoria_id
LEFT JOIN ss_epp_modelo m ON m.epp_item_id = i.id
GROUP BY c.nombre, f.nombre, i.nombre_tecnico
ORDER BY c.nombre, f.nombre, i.nombre_tecnico;
