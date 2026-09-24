-- Seed del curso "Riesgo al subir y bajar escaleras fijas" (SST, concientización obra + oficina).
-- Contenido basado en OSHA 1926.1052 / INSST NTP 404 (ver fuentes en el chat).
-- Las imagenUrl quedan vacías a propósito: se completan luego desde /cursos/editor
-- subiendo las fotos reales (POST /api/v1/curso/imagenes) y pegando la URL resultante.
-- Solo INSERT, no destructivo. Revisar antes de correr en pgAdmin (regla D1).

DO $$
DECLARE
  v_curso_id integer;
BEGIN

INSERT INTO curso (titulo, descripcion, categoria_nombre, rol_destino, nota_minima_aprobacion, activo, color_tema, created_at)
VALUES (
  'Riesgo al subir y bajar escaleras fijas',
  'Concientización sobre los riesgos de caída a distinto nivel al usar escaleras fijas, tanto en obra como en oficina.',
  'SST',
  NULL,
  14,
  true,
  '#c9a53b',
  now()
)
RETURNING id INTO v_curso_id;

-- 1) Portada
INSERT INTO curso_slide (curso_id, orden, tipo_codigo, es_evaluable, modo_correccion, configuracion_json, created_at) VALUES
(v_curso_id, 1, 'contenido_texto', false, 'sin_calificar', '{
  "kicker": "SEGURIDAD Y SALUD EN EL TRABAJO",
  "titulo": "Riesgo al subir y bajar escaleras fijas",
  "texto": "Un momento de descuido en una escalera puede cambiarlo todo. Este curso te muestra cómo evitarlo.",
  "imagenUrl": "",
  "iconoDecorativo": "ti-stairs"
}', now()),

-- 2) Concientización
(v_curso_id, 2, 'contenido_texto', false, 'sin_calificar', '{
  "kicker": "ESTO LE PUEDE PASAR A CUALQUIERA",
  "titulo": "En obra y en oficina, el riesgo es el mismo",
  "texto": "Las caídas a distinto nivel son una de las principales causas de accidentes laborales. No importa si trabajas en campo o en un edificio de oficinas: subir o bajar una escalera fija con prisa, distraído o con calzado inadecuado puede provocar una lesión grave.",
  "iconoDecorativo": "ti-alert-triangle"
}', now()),

-- 3) Tarjetas: riesgos principales
(v_curso_id, 3, 'contenido_tarjetas', false, 'sin_calificar', '{
  "kicker": "IDENTIFICA EL PELIGRO",
  "titulo": "Riesgos principales al usar escaleras fijas",
  "tarjetas": [
    {"id": "1", "titulo": "Subir o bajar con prisa", "texto": "Correr o apurarse aumenta el riesgo de resbalar.", "imagenUrl": "", "descripcion": "Subir o bajar corriendo reduce el tiempo de reacción ante un tropiezo y es una de las causas más frecuentes de caída en escaleras fijas."},
    {"id": "2", "titulo": "Saltarse peldaños", "texto": "Subir o bajar de dos en dos peldaños.", "imagenUrl": "", "descripcion": "Al saltarse peldaños se pierde el equilibrio con mayor facilidad, sobre todo al bajar."},
    {"id": "3", "titulo": "No usar el pasamanos", "texto": "No sujetarse con al menos una mano libre.", "imagenUrl": "", "descripcion": "El pasamanos es el punto de apoyo que evita una caída completa ante un resbalón. Toda escalera de 4 o más peldaños debe tener uno."},
    {"id": "4", "titulo": "Calzado inadecuado", "texto": "Suelas resbalosas o desgastadas.", "imagenUrl": "", "descripcion": "El calzado de seguridad con suela antideslizante en buen estado es obligatorio para transitar escaleras fijas en obra."},
    {"id": "5", "titulo": "Peldaños dañados", "texto": "Escalones sueltos, rotos o desgastados.", "imagenUrl": "", "descripcion": "Un peldaño en mal estado debe reportarse de inmediato y no debe usarse hasta su reparación."},
    {"id": "6", "titulo": "Objetos en la escalera", "texto": "Herramientas, cables o líquidos dejados en los peldaños.", "imagenUrl": "", "descripcion": "El orden y la limpieza en las escaleras es responsabilidad de todos: nada debe quedar sobre los peldaños."},
    {"id": "7", "titulo": "Mala iluminación", "texto": "Escalones desnivelados que no se ven a tiempo.", "imagenUrl": "", "descripcion": "Toda escalera debe tener iluminación suficiente en todo su recorrido."},
    {"id": "8", "titulo": "Cargar objetos con ambas manos", "texto": "Sin ninguna mano libre para el pasamanos.", "imagenUrl": "", "descripcion": "Si necesitas cargar algo, usa un medio de transporte (carrito, montacargas) o pide ayuda para mantener una mano libre."},
    {"id": "9", "titulo": "Distracción", "texto": "Usar el celular o hablar mientras se transita.", "imagenUrl": "", "descripcion": "Cualquier distracción durante el tránsito por la escalera retrasa la reacción ante un tropiezo."}
  ]
}', now()),

-- 4) Galería de evidencia (a completar con fotos reales del usuario)
(v_curso_id, 4, 'contenido_galeria_zoom', false, 'sin_calificar', '{
  "kicker": "EVIDENCIA",
  "titulo": "Situaciones reales en nuestros proyectos",
  "imagenes": [
    {"id": "1", "imagenUrl": "", "caption": "Pendiente: subir foto real"},
    {"id": "2", "imagenUrl": "", "caption": "Pendiente: subir foto real"},
    {"id": "3", "imagenUrl": "", "caption": "Pendiente: subir foto real"},
    {"id": "4", "imagenUrl": "", "caption": "Pendiente: subir foto real"}
  ]
}', now()),

-- 5) Tarjetas: controles de la empresa
(v_curso_id, 5, 'contenido_tarjetas', false, 'sin_calificar', '{
  "kicker": "CONTROLES",
  "titulo": "Qué hace la empresa para reducir el riesgo",
  "tarjetas": [
    {"id": "1", "titulo": "Inspección periódica", "texto": "Cada 3 meses, con registro.", "imagenUrl": "", "descripcion": "Todas las escaleras fijas se inspeccionan trimestralmente y se lleva un registro de cada inspección."},
    {"id": "2", "titulo": "Reparación inmediata", "texto": "Todo defecto detectado se corrige antes de seguir usándola.", "imagenUrl": "", "descripcion": "Un peldaño o pasamanos dañado se repara de inmediato; mientras tanto, la escalera se señaliza como fuera de servicio."},
    {"id": "3", "titulo": "Señalización", "texto": "Escaleras en mal estado quedan marcadas.", "imagenUrl": "", "descripcion": "Ninguna escalera con defectos detectados debe usarse hasta ser reparada y despues re-habilitada."},
    {"id": "4", "titulo": "Iluminación y orden", "texto": "Iluminación adecuada y peldaños libres de objetos.", "imagenUrl": "", "descripcion": "Se exige mantener las escaleras iluminadas y libres de herramientas, cables o materiales."}
  ]
}', now()),

-- 6) Tarjetas: se debe / no se debe
(v_curso_id, 6, 'contenido_tarjetas', false, 'sin_calificar', '{
  "kicker": "SE DEBE / NO SE DEBE",
  "titulo": "Cómo usar correctamente una escalera fija",
  "tarjetas": [
    {"id": "1", "titulo": "SÍ: 3 puntos de apoyo", "texto": "Dos manos y un pie, o dos pies y una mano, siempre.", "imagenUrl": "", "descripcion": "La regla de los 3 puntos de contacto reduce drásticamente el riesgo de caída."},
    {"id": "2", "titulo": "NO: correr o saltar peldaños", "texto": "Sube y baja de frente, un peldaño a la vez.", "imagenUrl": "", "descripcion": "Nunca subas o bajes corriendo ni te saltes peldaños, aunque tengas prisa."},
    {"id": "3", "titulo": "SÍ: usar el pasamanos", "texto": "Con al menos una mano libre en todo momento.", "imagenUrl": "", "descripcion": "El pasamanos debe usarse siempre que subas o bajes, sin excepción."},
    {"id": "4", "titulo": "NO: cargar con ambas manos", "texto": "Usa un medio de transporte o pide ayuda.", "imagenUrl": "", "descripcion": "Si necesitas ambas manos para cargar algo, no uses la escalera hasta liberar al menos una mano."}
  ]
}', now()),

-- 7) Ejercicio: marcar en la imagen (pendiente subir foto real, sin evaluar por ahora)
(v_curso_id, 7, 'contenido_texto', false, 'sin_calificar', '{
  "kicker": "EJERCICIO",
  "titulo": "Observa esta escalera",
  "texto": "Pendiente: reemplazar esta pantalla por el tipo \"pregunta_imagen\" cuando se tenga la foto real donde marcar el punto de riesgo.",
  "iconoDecorativo": "ti-photo-search"
}', now()),

-- 8) Ejercicio: ordenar pasos correctos
(v_curso_id, 8, 'pregunta_ordenar', true, 'igualdad_exacta', '{
  "kicker": "EJERCICIO",
  "enunciado": "Ordena los pasos correctos para bajar una escalera fija de forma segura",
  "items": [
    {"id": "1", "texto": "Verifica que la escalera esté en buen estado"},
    {"id": "2", "texto": "Sujeta el pasamanos con al menos una mano"},
    {"id": "3", "texto": "Baja de frente, un peldaño a la vez"},
    {"id": "4", "texto": "Mantén los 3 puntos de apoyo en todo momento"}
  ],
  "respuestaCorrecta": ["1", "2", "3", "4"]
}', now()),

-- 9) Evaluación final (opción múltiple, x2 preguntas basadas en escaleras01.json)
(v_curso_id, 9, 'pregunta_opcion_multiple', true, 'igualdad_exacta', '{
  "kicker": "EVALUACIÓN FINAL",
  "enunciado": "¿Cuál es la regla de los tres puntos de contacto?",
  "opciones": [
    {"id": "a", "texto": "Dos manos y un pie"},
    {"id": "b", "texto": "Dos pies y una mano"},
    {"id": "c", "texto": "Tres puntos de apoyo al mismo tiempo (cualquier combinación de manos/pies)"}
  ],
  "respuestaCorrecta": "c"
}', now()),

(v_curso_id, 10, 'pregunta_opcion_multiple', true, 'igualdad_exacta', '{
  "kicker": "EVALUACIÓN FINAL",
  "enunciado": "Antes de subir una escalera fija, ¿qué debes revisar?",
  "opciones": [
    {"id": "a", "texto": "Que esté sucia"},
    {"id": "b", "texto": "Que la base y los peldaños estén firmes, sin daños"},
    {"id": "c", "texto": "Nada, siempre está en buen estado"}
  ],
  "respuestaCorrecta": "b"
}', now()),

(v_curso_id, 11, 'pregunta_vf', true, 'igualdad_exacta', '{
  "kicker": "EVALUACIÓN FINAL",
  "enunciado": "Cargar una caja con ambas manos mientras subes una escalera fija es una práctica segura si vas con cuidado.",
  "respuestaCorrecta": false
}', now());

END $$;
