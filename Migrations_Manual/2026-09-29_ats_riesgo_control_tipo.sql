-- Clasifica cada control sugerido por jerarquía de controles (Eliminación, Sustitución,
-- Ingeniería, Administrativo, EPP) — antes solo era texto libre sin tipo. Se agrega la columna
-- con un valor por defecto inferido con palabras clave; el Coordinador SSOMA puede reclasificar
-- cualquiera desde la pestaña Controles / vista de plantilla, esto es solo un punto de partida.

ALTER TABLE ss_ats_riesgo_control ADD COLUMN IF NOT EXISTS tipo varchar(20) NOT NULL DEFAULT 'Administrativo';

UPDATE ss_ats_riesgo_control
SET tipo = 'Ingenieria'
WHERE tipo = 'Administrativo' AND (
    lower(texto) LIKE '%baranda%' OR lower(texto) LIKE '%guarda%' OR lower(texto) LIKE '%anclaje%'
    OR lower(texto) LIKE '%malla%' OR lower(texto) LIKE '%aislamiento%' OR lower(texto) LIKE '%resguardo%'
    OR lower(texto) LIKE '%ventilaci%' OR lower(texto) LIKE '%extractor%' OR lower(texto) LIKE '%bloqueo%'
    OR lower(texto) LIKE '%loto%' OR lower(texto) LIKE '%señaliza%' AND lower(texto) LIKE '%fisic%'
);

UPDATE ss_ats_riesgo_control
SET tipo = 'Epp'
WHERE tipo = 'Administrativo' AND (
    lower(texto) LIKE '%epp%' OR lower(texto) LIKE '%casco%' OR lower(texto) LIKE '%guante%'
    OR lower(texto) LIKE '%lente%' OR lower(texto) LIKE '%arnés%' OR lower(texto) LIKE '%arnes%'
    OR lower(texto) LIKE '%respirador%' OR lower(texto) LIKE '%tapon%' OR lower(texto) LIKE '%orejera%'
    OR lower(texto) LIKE '%mascarilla%' OR lower(texto) LIKE '%careta%' OR lower(texto) LIKE '%mandil%'
    OR lower(texto) LIKE '%botas%' OR lower(texto) LIKE '%zapato%'
);

UPDATE ss_ats_riesgo_control
SET tipo = 'Eliminacion'
WHERE tipo = 'Administrativo' AND lower(texto) LIKE '%eliminar%' AND lower(texto) NOT LIKE '%epp%';

UPDATE ss_ats_riesgo_control
SET tipo = 'Sustitucion'
WHERE tipo = 'Administrativo' AND (lower(texto) LIKE '%sustituir%' OR lower(texto) LIKE '%reemplazar%');
