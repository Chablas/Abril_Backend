-- Elimina los cursos de ejemplo antiguos (caída al mismo nivel, prevención de caídas genérico, etc.)
-- dejando SOLO "Riesgo al subir y bajar escaleras fijas". Revisar el SELECT antes de correr el DELETE
-- (regla D1: un solo entorno real, doble confirmación antes de borrar).

-- 1) Verificar qué se va a borrar:
SELECT id, titulo FROM curso WHERE titulo <> 'Riesgo al subir y bajar escaleras fijas';

-- 2) Si la lista de arriba es correcta, correr esto (borra en orden por FKs):
DELETE FROM curso_intento_respuesta WHERE curso_intento_id IN (
  SELECT id FROM curso_intento WHERE curso_id IN (
    SELECT id FROM curso WHERE titulo <> 'Riesgo al subir y bajar escaleras fijas'
  )
);
DELETE FROM curso_intento_evidencia WHERE curso_intento_id IN (
  SELECT id FROM curso_intento WHERE curso_id IN (
    SELECT id FROM curso WHERE titulo <> 'Riesgo al subir y bajar escaleras fijas'
  )
);
DELETE FROM curso_intento WHERE curso_id IN (
  SELECT id FROM curso WHERE titulo <> 'Riesgo al subir y bajar escaleras fijas'
);
DELETE FROM curso_slide WHERE curso_id IN (
  SELECT id FROM curso WHERE titulo <> 'Riesgo al subir y bajar escaleras fijas'
);
DELETE FROM curso WHERE titulo <> 'Riesgo al subir y bajar escaleras fijas';
