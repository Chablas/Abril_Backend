-- ============================================================================
-- PETAR — Reemplaza los checklists genéricos (redactados por IA, sin respaldo
-- documental) por el contenido REAL transcrito de los formatos oficiales de
-- Abril, provistos por el usuario:
--   SSO-FO-039 PETAR Altura Ver.03           -> "Trabajo en altura"
--   SSO-FO-040 PETAR Caliente Ver.03         -> "Trabajo en caliente"
--   SSO-FO-041 PETAR Confinado Ver.02        -> "Espacios confinados"
--   SSO-FO-042 PETAR Excavación Ver.03       -> "Excavaciones"
--   SSO-FO-043 PETAR izaje torre grúa Ver.02 -> "Izaje de cargas"
--   SSO-FO-125 PETAR Control y Aislamiento de Energía -> "Trabajos eléctricos (bloqueo y etiquetado)"
-- y agrega el tipo nuevo detectado como gap real (izaje con equipo no
-- convencional / tecles, que el FO-043 no cubre porque está armado
-- específicamente para grúa): SSO-FO-153.
--
-- NOTA IMPORTANTE (transparencia): el checklist de "Izaje con equipo no
-- convencional / tecles" (SSO-FO-153) NO viene de un Excel oficial — Abril no
-- tiene ese formato todavía. Fue redactado por IA en base a criterio general
-- de SSOMA en construcción (justificación técnica de equipo no certificado,
-- derateo de capacidad, certificación de punto de anclaje, etc.) y debe ser
-- revisado por el Coordinador SSOMA/Prevencionista antes de usarse en campo.
-- Además, el FO-043 (izaje con grúa) tiene campos técnicos de formulario
-- (tipo/modelo de grúa, longitud de pluma, ángulo, capacidad certificada,
-- peso de componentes) que NO son ítems SI/NO/NA y por lo tanto no están
-- representados en `ss_petar_item` — solo se transcribió su checklist de
-- SI/NO/NA. Si se necesita capturar esos campos técnicos, requiere columnas
-- nuevas en `ss_petar`, no cabe en el catálogo genérico de items.
--
-- Idempotente: agrega la columna si falta, borra solo los items existentes de
-- estos tipos (por tipo_id) e inserta los reales. No toca `ss_petar` (los
-- registros ya firmados no se alteran: `ss_petar_item_respuesta` guarda copia
-- propia de `texto` en el momento de la firma, ver AtsRepository/PetarRepository).
-- ============================================================================
BEGIN;

ALTER TABLE ss_petar_tipo ADD COLUMN IF NOT EXISTS codigo varchar(20);

UPDATE ss_petar_tipo SET codigo = 'SSO-FO-039' WHERE nombre = 'Trabajo en altura';
UPDATE ss_petar_tipo SET codigo = 'SSO-FO-040' WHERE nombre = 'Trabajo en caliente';
UPDATE ss_petar_tipo SET codigo = 'SSO-FO-041' WHERE nombre = 'Espacios confinados';
UPDATE ss_petar_tipo SET codigo = 'SSO-FO-042' WHERE nombre = 'Excavaciones';
UPDATE ss_petar_tipo SET codigo = 'SSO-FO-043' WHERE nombre = 'Izaje de cargas';
UPDATE ss_petar_tipo SET codigo = 'SSO-FO-125' WHERE nombre = 'Trabajos eléctricos (bloqueo y etiquetado)';

INSERT INTO ss_petar_tipo (nombre, codigo, orden)
SELECT * FROM (VALUES
    ('Izaje con equipo no convencional / tecles', 'SSO-FO-153', 7)
) AS v(nombre, codigo, orden)
WHERE NOT EXISTS (SELECT 1 FROM ss_petar_tipo t WHERE t.nombre = v.nombre);

-- Borra los items genéricos existentes de los 6 tipos que sí tienen formato
-- oficial (deja intacto cualquier ss_petar_item_respuesta ya firmado, porque
-- esa tabla guarda su propia copia del texto).
DELETE FROM ss_petar_item
WHERE tipo_id IN (
    SELECT id FROM ss_petar_tipo
    WHERE nombre IN (
        'Trabajo en altura', 'Trabajo en caliente', 'Espacios confinados',
        'Excavaciones', 'Izaje de cargas', 'Trabajos eléctricos (bloqueo y etiquetado)'
    )
);

INSERT INTO ss_petar_item (tipo_id, texto, orden)
SELECT t.id, v.texto, v.orden
FROM (VALUES
    -- SSO-FO-039 Trabajo en altura
    ('Trabajo en altura', 'Todo el personal participante del trabajo ha recibido la capacitación en Trabajos de Altura', 1),
    ('Trabajo en altura', 'El trabajo requiere la presencia de un Observador de trabajo en Altura', 2),
    ('Trabajo en altura', 'Se realizó la charla de seguridad previa al inicio de la actividad y se elaboró el ATS grupalmente en el punto de trabajo', 3),
    ('Trabajo en altura', 'Se verificó el área de influencia de la actividad para planificar los controles preventivos', 4),
    ('Trabajo en altura', 'Se difundió entre el personal los riesgos presentes en la actividad', 5),
    ('Trabajo en altura', 'Se difundió al personal las medidas tomadas para controlar los riesgos detectados', 6),
    ('Trabajo en altura', 'Existen accesos adecuados y despejados al lugar de trabajo en altura', 7),
    ('Trabajo en altura', 'Se ha bloqueado y señalizado para restricción de ingreso de personas en todos los niveles necesarios', 8),
    ('Trabajo en altura', 'El área de trabajo está alejada más de 3 metros de sistemas eléctricos aéreos', 9),
    ('Trabajo en altura', 'El área de trabajo no se ve afectada por vientos superiores a 45 km/h', 10),
    ('Trabajo en altura', 'El área de trabajo cuenta con la luminosidad necesaria (natural o artificial)', 11),
    ('Trabajo en altura', 'Las superficies de trabajo tienen el piso completo y en buen estado', 12),
    ('Trabajo en altura', 'Los bordes de losa o desniveles superiores a 1.80 m cuentan con barandas', 13),
    ('Trabajo en altura', 'Las barandas superior e intermedia están en buenas condiciones y correctamente instaladas', 14),
    ('Trabajo en altura', 'Es necesario utilizar arnés tipo paracaídas, líneas de vida y absorbedor de impacto (Norma ANSI Z359)', 15),
    ('Trabajo en altura', 'Se realizó la inspección del equipo contra caídas (adjuntar/revisar registro)', 16),
    ('Trabajo en altura', 'Si hubiera elementos en mal estado, se procedió a su retiro de servicio', 17),
    ('Trabajo en altura', 'Los cascos del personal cuentan con barbiquejo', 18),
    ('Trabajo en altura', 'Existen puntos de anclaje adecuados, ubicados sobre el trabajador en la actividad', 19),
    ('Trabajo en altura', 'Se verificó que el punto de anclaje esté en buenas condiciones y sea resistente según estándar', 20),
    ('Trabajo en altura', 'Se verificó la disponibilidad de suficientes puntos de anclaje por persona o para varias personas', 21),
    ('Trabajo en altura', 'Es necesario utilizar conectores de anclaje debido a la falta de puntos de anclaje adecuados', 22),
    ('Trabajo en altura', 'Es necesario instalar línea de anclaje vertical adecuada y resistente según estándar', 23),
    ('Trabajo en altura', 'Es necesario instalar línea de anclaje horizontal adecuada y resistente según estándar', 24),
    ('Trabajo en altura', 'Es necesario hacer uso de ROPE GRAB (freno de línea de anclaje)', 25),
    ('Trabajo en altura', 'Se realizó la inspección de estado y uso de escaleras, cumplen con estándares de seguridad', 26),
    ('Trabajo en altura', 'Se realizó la inspección de estado y uso de andamios y plataformas, cumplen con estándares de seguridad', 27),
    ('Trabajo en altura', 'Se realizó la inspección del estado y uso de los equipos de izaje y elevación, cumplen con estándares de seguridad', 28),
    ('Trabajo en altura', 'Se realizó la inspección del estado y uso de las herramientas y equipos, cumplen con estándares de seguridad', 29),
    ('Trabajo en altura', 'Todas las herramientas manuales y equipos portátiles se encuentran asegurados contra caída', 30),

    -- SSO-FO-040 Trabajo en caliente
    ('Trabajo en caliente', 'Todo el personal participante del trabajo ha recibido la capacitación en Trabajos en Caliente', 1),
    ('Trabajo en caliente', 'Todo el personal cuenta con los EPP adecuados para la actividad', 2),
    ('Trabajo en caliente', 'Se realizó la charla de seguridad previa al inicio de la actividad y se elaboró el ATS grupalmente en el punto de trabajo', 3),
    ('Trabajo en caliente', 'Se verificó el área de influencia de la actividad para planificar los controles preventivos', 4),
    ('Trabajo en caliente', 'Se difundió entre el personal los riesgos presentes en la actividad', 5),
    ('Trabajo en caliente', 'Se difundió al personal las medidas tomadas para controlar los riesgos detectados', 6),
    ('Trabajo en caliente', 'Existen accesos adecuados y despejados al lugar de trabajo en caliente', 7),
    ('Trabajo en caliente', 'Se ha bloqueado y señalizado para restricción de ingreso de personas en todos los niveles necesarios', 8),
    ('Trabajo en caliente', 'El área de trabajo cuenta con la luminosidad necesaria (natural o artificial)', 9),
    ('Trabajo en caliente', 'El área de trabajo cuenta con la ventilación adecuada', 10),
    ('Trabajo en caliente', 'Se analizó la dirección del viento', 11),
    ('Trabajo en caliente', 'Se monitoreó la atmósfera y no existen gases ni vapores inflamables', 12),
    ('Trabajo en caliente', 'Se retiraron al menos 20 metros los materiales inflamables, trapos y polvo, haciendo segura el área', 13),
    ('Trabajo en caliente', 'Se verificó que los suelos no tengan derrames de algún tipo de combustible', 14),
    ('Trabajo en caliente', 'Se cubrieron o aislaron materiales, infraestructura o equipos no removibles', 15),
    ('Trabajo en caliente', 'Las conexiones a tierra están correctamente instaladas', 16),
    ('Trabajo en caliente', 'Se revisó la disponibilidad y operatividad de los medios de extinción de fuego (manguera, extintores, mantas, etc.)', 17),
    ('Trabajo en caliente', 'Los equipos/maquinarias se limpiaron de todo tipo de material combustible', 18),
    ('Trabajo en caliente', 'Los equipos/maquinarias están eléctricamente bloqueados y rotulados', 19),
    ('Trabajo en caliente', 'Motores y válvulas bloqueados y señalizados', 20),
    ('Trabajo en caliente', 'Tanques/depósitos se limpiaron de todo tipo de material combustible', 21),
    ('Trabajo en caliente', 'Tanques/depósitos han sido purgados de líquidos/vapores inflamables', 22),
    ('Trabajo en caliente', 'Tanques, tubería y equipo han sido bloqueados y ventilados al menos 24 horas', 23),
    ('Trabajo en caliente', 'El vigía cuenta con equipo contraincendio (extintor y/o manguera) y sabe utilizarlo', 24),
    ('Trabajo en caliente', 'El vigía conoce los lineamientos de protección en caso de alguna emergencia', 25),
    ('Trabajo en caliente', 'Se requiere vigilancia del fuego hasta 1 hora después de concluido el trabajo', 26),

    -- SSO-FO-041 Espacios confinados
    ('Espacios confinados', 'Se identificó y controló el riesgo de deficiencia de oxígeno', 1),
    ('Espacios confinados', 'Se identificó y controló el riesgo de gases o vapores tóxicos', 2),
    ('Espacios confinados', 'Se identificó y controló el riesgo de gases inflamables', 3),
    ('Espacios confinados', 'Se identificó y controló el riesgo de residuos inflamables', 4),
    ('Espacios confinados', 'Se identificaron y controlaron peligros eléctricos', 5),
    ('Espacios confinados', 'Se identificaron y controlaron peligros mecánicos', 6),
    ('Espacios confinados', 'Se identificó y controló el riesgo de inundación', 7),
    ('Espacios confinados', 'Área libre de combustibles/inflamables', 8),
    ('Espacios confinados', 'Delimitación del área de trabajo (letreros, avisos de seguridad, etc.)', 9),
    ('Espacios confinados', 'Accesos libres y expeditos', 10),
    ('Espacios confinados', 'Apertura de ventanas de ventilación y/o equipos de ventilación', 11),
    ('Espacios confinados', 'Iluminación adecuada', 12),
    ('Espacios confinados', 'Procedimiento de bloqueo, cerrado de válvulas y bridas de alimentación', 13),
    ('Espacios confinados', 'Vaciado total del contenido', 14),
    ('Espacios confinados', 'Monitoreo del área (medición de gases si es necesario)', 15),
    ('Espacios confinados', 'Equipos de comunicación disponibles', 16),
    ('Espacios confinados', 'EPP básicos y complementarios completos (uso obligatorio)', 17),
    ('Espacios confinados', 'Uso de herramientas no metálicas', 18),
    ('Espacios confinados', 'Se requiere equipo de protección autónoma', 19),
    ('Espacios confinados', 'Eliminación de fuentes de ignición', 20),
    ('Espacios confinados', 'Permisos para trabajos específicos (excavación, etc.) si aplica', 21),
    ('Espacios confinados', 'Supervisor de área notificado', 22),
    ('Espacios confinados', 'Equipo contra incendio disponible (extintor ABC 9 kg)', 23),

    -- SSO-FO-042 Excavaciones
    ('Excavaciones', 'Se cuenta con los EPP básicos completos y complementarios si fuera el caso (arnés, línea de vida)', 1),
    ('Excavaciones', 'La máquina excavadora se encuentra ubicada a 1.0 veces la profundidad de la excavación', 2),
    ('Excavaciones', 'Se cuenta con entibaciones o apuntalamientos verificados por un ingeniero civil o responsable de la obra', 3),
    ('Excavaciones', 'Se cuenta con barandas en todo el perímetro de la excavación o zanja', 4),
    ('Excavaciones', 'Se ha colocado señalización en el perímetro de la excavación (cintas, mallas de peligro, letreros, etc.)', 5),
    ('Excavaciones', 'La iluminación dentro de la zanja es suficiente para realizar los trabajos', 6),
    ('Excavaciones', 'La concentración de oxígeno es adecuada para el personal que ingrese a la zanja o excavación', 7),
    ('Excavaciones', 'Equipos contra emergencias disponibles (camillas, botiquín, grupo de emergencia, etc.)', 8),
    ('Excavaciones', 'Se cuenta con escaleras o rampas de acceso y salida aseguradas y libres de obstáculos', 9),
    ('Excavaciones', 'Se cuenta con señalero o vigía para dirigir al operador de la máquina y a los conductores de los volquetes', 10),
    ('Excavaciones', 'Las vías de circulación peatonal y vehicular se encuentran a una distancia de 2 y 3 metros', 11),
    ('Excavaciones', 'El material extraído se acopia a una distancia mínima de 2 metros de la excavación', 12),
    ('Excavaciones', 'Se ha limpiado el área y se han cortado los servicios básicos (luz, gas, etc.) antes de iniciar la excavación', 13),
    ('Excavaciones', 'Se ha aplicado lechada de concreto puro mezclada con agua a las paredes de la excavación', 14),

    -- SSO-FO-043 Izaje de cargas (con grúa) — solo el checklist SI/NO/NA;
    -- los campos técnicos de grúa (tipo, pluma, capacidad certificada, peso
    -- de componentes) no son ítems y no se transcriben aquí, ver nota arriba.
    ('Izaje de cargas', 'Se requiere el uso de vientos/cabos guía en la carga a izar', 1),
    ('Izaje de cargas', 'Existen riesgos eléctricos cerca del área', 2),
    ('Izaje de cargas', 'Existen peligros subterráneos (hundimiento)', 3),
    ('Izaje de cargas', 'Se realizó reunión de coordinación antes de la maniobra', 4),
    ('Izaje de cargas', 'La condición del suelo es estable', 5),
    ('Izaje de cargas', 'La condición meteorológica es óptima', 6),
    ('Izaje de cargas', 'Se inspeccionaron los accesorios de izaje', 7),
    ('Izaje de cargas', 'El personal está capacitado en maniobras de izaje', 8),
    ('Izaje de cargas', 'Se ha señalizado el área de maniobras', 9),
    ('Izaje de cargas', 'Se cuenta con equipos de comunicación', 10),
    ('Izaje de cargas', 'Se cuenta con rigger, señalero o vigía', 11),
    ('Izaje de cargas', 'Se identificaron otros peligros existentes', 12),

    -- SSO-FO-125 Trabajos eléctricos (bloqueo y etiquetado / LOTO)
    ('Trabajos eléctricos (bloqueo y etiquetado)', 'Se identificó con precisión el equipo que será intervenido (nombre, código, ubicación)', 1),
    ('Trabajos eléctricos (bloqueo y etiquetado)', 'Se notificó al personal afectado por la parada del equipo', 2),
    ('Trabajos eléctricos (bloqueo y etiquetado)', 'Todos los trabajadores involucrados conocen el procedimiento específico para esta tarea', 3),
    ('Trabajos eléctricos (bloqueo y etiquetado)', 'Se encuentra restringido el acceso a la zona de trabajo para personal no autorizado', 4),
    ('Trabajos eléctricos (bloqueo y etiquetado)', 'Hay un responsable designado en el sitio durante toda la intervención', 5),
    ('Trabajos eléctricos (bloqueo y etiquetado)', 'Se inspeccionó el equipo para asegurarse de que todos los elementos estén nuevamente instalados y seguros', 6),
    ('Trabajos eléctricos (bloqueo y etiquetado)', 'El procedimiento específico de bloqueo y etiquetado está disponible en el sitio de trabajo', 7)
) AS v(tipo_nombre, texto, orden)
JOIN ss_petar_tipo t ON t.nombre = v.tipo_nombre
WHERE NOT EXISTS (SELECT 1 FROM ss_petar_item i WHERE i.tipo_id = t.id AND i.texto = v.texto);

-- SSO-FO-153 Izaje con equipo no convencional / tecles (redactado por IA,
-- pendiente de revisión SSOMA — ver nota al inicio del archivo)
INSERT INTO ss_petar_item (tipo_id, texto, orden)
SELECT t.id, v.texto, v.orden
FROM (VALUES
    ('Izaje con equipo no convencional / tecles', 'Justificación técnica documentada del uso de equipo no certificado para izaje (debe ser excepción, no rutina)', 1),
    ('Izaje con equipo no convencional / tecles', 'Capacidad de izaje derateada respecto a la tabla de carga del fabricante (o límite conservador si el equipo no tiene tabla en modo izaje)', 2),
    ('Izaje con equipo no convencional / tecles', 'Punto de enganche del brazo/cucharón (excavadora) inspeccionado y en buen estado', 3),
    ('Izaje con equipo no convencional / tecles', 'Certificado vigente del tecle (manual o eléctrico) verificado', 4),
    ('Izaje con equipo no convencional / tecles', 'Capacidad del tecle verificada contra el peso real de la carga', 5),
    ('Izaje con equipo no convencional / tecles', 'Punto de anclaje / estructura donde cuelga el tecle certificado para soportar la carga', 6),
    ('Izaje con equipo no convencional / tecles', 'Eslingas, grilletes y ganchos inspeccionados, sin daños visibles', 7),
    ('Izaje con equipo no convencional / tecles', 'Prohibición total de personal bajo la carga, señalizada y verificada', 8),
    ('Izaje con equipo no convencional / tecles', 'Uso de tag lines (cabos guía) para controlar el balanceo de la carga', 9)
) AS v(tipo_nombre, texto, orden)
JOIN ss_petar_tipo t ON t.nombre = v.tipo_nombre
WHERE NOT EXISTS (SELECT 1 FROM ss_petar_item i WHERE i.tipo_id = t.id AND i.texto = v.texto);

COMMIT;

-- ============================================================================
-- Verificación (correr después; no modifica nada)
-- ============================================================================
-- SELECT nombre, codigo, orden FROM ss_petar_tipo ORDER BY orden;
-- SELECT t.nombre, count(*) FROM ss_petar_item i JOIN ss_petar_tipo t ON t.id = i.tipo_id GROUP BY t.nombre ORDER BY t.nombre;
