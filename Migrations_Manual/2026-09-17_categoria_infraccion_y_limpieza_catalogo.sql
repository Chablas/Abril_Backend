-- Formaliza la categoría de cada infracción (Falta / Menor / Moderada / Grave / MuyGrave) del
-- Anexo 4 como columna propia del catálogo -- hasta ahora esa clasificación solo vivía en el
-- nombre de texto libre, mientras el formulario de alta pedía además un "Severidad" de opción
-- libre (CRÍTICO/ALTO/MEDIO/BAJO) que no tenía ninguna relación real con la infracción elegida
-- y podía contradecirla. De ahora en más la categoría la define el catálogo, no el usuario.

ALTER TABLE ssoma_rac_infraccion ADD COLUMN IF NOT EXISTS categoria varchar(20);

UPDATE ssoma_rac_infraccion SET categoria = 'Falta'    WHERE nombre = 'Falta leve';
UPDATE ssoma_rac_infraccion SET categoria = 'Menor'    WHERE nombre = 'Infracción menor';
UPDATE ssoma_rac_infraccion SET categoria = 'Moderada' WHERE nombre = 'Infracción moderada';
UPDATE ssoma_rac_infraccion SET categoria = 'Grave'    WHERE nombre = 'Infracción grave';

-- Infracción muy grave -- el Anexo 4 no le fija monto (da lugar a RESCISIÓN DE CONTRATO), pero
-- necesita existir en el catálogo para poder tipificar el caso; el backend la reconoce por
-- categoria='MuyGrave' y no le calcula un monto en soles.
INSERT INTO ssoma_rac_infraccion (nombre, factor_uit, monto_fijo, descripcion, categoria, activo)
SELECT 'Infracción muy grave', NULL, NULL,
  'Da lugar a RESCISIÓN DE CONTRATO, no a una penalidad económica. Incluye, entre otras: adulteración o falsificación de documentos; agresión física o verbal a un miembro del staff, otras contratistas, personal de serenazgo, policía u otros; ocultar o intentar ocultar accidentes.',
  'MuyGrave', true
WHERE NOT EXISTS (SELECT 1 FROM ssoma_rac_infraccion WHERE nombre = 'Infracción muy grave');

-- Limpieza de duplicados/residuos de seeds anteriores (p.ej. "basica", "grave" sueltos que no
-- coinciden con los 5 nombres canónicos de arriba y son lo que generaba el combo duplicado en
-- el formulario de Nueva Penalidad). Se desactivan, nunca se borran -- por si ya fueron usados
-- en alguna penalidad existente, esa FK sigue siendo válida.
UPDATE ssoma_rac_infraccion
SET activo = false
WHERE activo = true
  AND nombre NOT IN ('Falta leve', 'Infracción menor', 'Infracción moderada', 'Infracción grave', 'Infracción muy grave');

-- ssoma_penalidad: se añade "categoria", que el backend copia automáticamente de la infracción
-- elegida al registrar. "severidad" se deja en la tabla (nunca se borran columnas, convención
-- del proyecto) pero deja de usarse desde ahora: ya no se pide en el alta ni se muestra en el
-- detalle ni en los PDFs/notificaciones.
ALTER TABLE ssoma_penalidad ADD COLUMN IF NOT EXISTS categoria varchar(20);
