-- Programación de Inspecciones Cruzadas SSOMA: anillos de proyectos que se inspeccionan entre
-- sí, rotando un mes a la vez (nunca se repite pareja hasta agotar el anillo completo). Ver
-- Features/SsomaModule/InspeccionCruzadaProgramacionFeature/. Ejecutar manualmente en pgAdmin.

BEGIN;

-- Un grupo de proyectos que se inspeccionan solo entre ellos (dos anillos nunca se mezclan).
CREATE TABLE IF NOT EXISTS ss_inspeccion_cruzada_anillo (
    id           serial PRIMARY KEY,
    nombre       varchar(200) NOT NULL,
    created_at   timestamp NOT NULL DEFAULT now()
);

-- Proyectos dentro de un anillo, con su posición en la cola circular.
CREATE TABLE IF NOT EXISTS ss_inspeccion_cruzada_rotacion (
    id           serial PRIMARY KEY,
    anillo_id    integer NOT NULL REFERENCES ss_inspeccion_cruzada_anillo (id),
    proyecto_id  integer NOT NULL REFERENCES project (project_id),
    orden        integer NOT NULL,
    activo       boolean NOT NULL DEFAULT true,
    created_at   timestamp NOT NULL DEFAULT now(),
    updated_at   timestamp NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_inspeccion_cruzada_rotacion_proyecto
    ON ss_inspeccion_cruzada_rotacion (proyecto_id);

-- Una fila por anillo: desplazamiento circular vigente y último mes ya generado, para que
-- agregar/quitar un proyecto no reinicie la continuidad de los demás meses.
CREATE TABLE IF NOT EXISTS ss_inspeccion_cruzada_cursor (
    anillo_id     integer PRIMARY KEY REFERENCES ss_inspeccion_cruzada_anillo (id),
    offset_valor  integer NOT NULL DEFAULT 1,
    ultimo_anio   integer NULL,
    ultimo_mes    integer NULL,
    updated_at    timestamp NULL
);

-- La pareja de proyectos que le toca inspeccionarse en un mes concreto. Generada
-- automáticamente por la rotación; reasignable a mano para un mes puntual.
CREATE TABLE IF NOT EXISTS ss_inspeccion_cruzada_programacion (
    id                          serial PRIMARY KEY,
    anio                        integer NOT NULL,
    mes                         integer NOT NULL,
    anillo_id                   integer NOT NULL REFERENCES ss_inspeccion_cruzada_anillo (id),
    proyecto_inspector_id       integer NOT NULL REFERENCES project (project_id),
    proyecto_inspeccionado_id   integer NOT NULL REFERENCES project (project_id),
    es_manual                   boolean NOT NULL DEFAULT false,
    motivo_cambio               varchar(500) NULL,
    created_at                  timestamp NOT NULL DEFAULT now(),
    updated_at                  timestamp NULL
);

CREATE INDEX IF NOT EXISTS ix_inspeccion_cruzada_programacion_periodo
    ON ss_inspeccion_cruzada_programacion (anio, mes);

COMMIT;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- Siembra: cronograma de Septiembre 2026 ya definido (ver imagen "CRONOGRAMA DE INSPECCIONES
-- CRUZADAS - SEPTIEMBRE"). Dos anillos:
--   Anillo A (6): CEDRO 33 → GRAN MANZANO → BOSQUE REAL → KAURI → SAUCE ZEN → MAXIMO → CEDRO 33
--   Anillo B (2): BUGAMBILIAS ⇄ 9 NOGALES
-- Ajustar los nombres del WHERE si no calzan exactos con project.project_description.
-- Ejecutar manualmente en pgAdmin, DESPUÉS del bloque anterior, revisando el SELECT de
-- verificación al final antes de confirmar que los proyectos matchearon todos.
-- ─────────────────────────────────────────────────────────────────────────────────────────────

BEGIN;

DO $$
DECLARE
    v_anillo_a integer;
    v_anillo_b integer;
BEGIN
    INSERT INTO ss_inspeccion_cruzada_anillo (nombre) VALUES ('Anillo A') RETURNING id INTO v_anillo_a;
    INSERT INTO ss_inspeccion_cruzada_anillo (nombre) VALUES ('Anillo B') RETURNING id INTO v_anillo_b;

    INSERT INTO ss_inspeccion_cruzada_rotacion (anillo_id, proyecto_id, orden)
    SELECT v_anillo_a, p.project_id, o.orden
    FROM (VALUES
        ('CEDRO 33', 0),
        ('GRAN MANZANO', 1),
        ('BOSQUE REAL', 2),
        ('KAURI', 3),
        ('SAUCE ZEN', 4),
        ('MAXIMO', 5)
    ) AS o(nombre, orden)
    JOIN project p ON p.project_description ILIKE o.nombre;

    INSERT INTO ss_inspeccion_cruzada_rotacion (anillo_id, proyecto_id, orden)
    SELECT v_anillo_b, p.project_id, o.orden
    FROM (VALUES
        ('BUGAMBILIAS', 0),
        ('9 NOGALES', 1)
    ) AS o(nombre, orden)
    JOIN project p ON p.project_description ILIKE o.nombre;

    -- Setiembre 2026 ya se considera generado (offset 1 = el orden tal cual quedó arriba);
    -- Octubre en adelante lo genera solo el sistema al abrir el calendario.
    INSERT INTO ss_inspeccion_cruzada_cursor (anillo_id, offset_valor, ultimo_anio, ultimo_mes)
    VALUES (v_anillo_a, 1, 2026, 9), (v_anillo_b, 1, 2026, 9);

    INSERT INTO ss_inspeccion_cruzada_programacion (anio, mes, anillo_id, proyecto_inspector_id, proyecto_inspeccionado_id)
    SELECT 2026, 9, v_anillo_a, r1.proyecto_id, r2.proyecto_id
    FROM ss_inspeccion_cruzada_rotacion r1
    JOIN ss_inspeccion_cruzada_rotacion r2
      ON r2.anillo_id = v_anillo_a AND r2.orden = (r1.orden + 1) % 6
    WHERE r1.anillo_id = v_anillo_a;

    INSERT INTO ss_inspeccion_cruzada_programacion (anio, mes, anillo_id, proyecto_inspector_id, proyecto_inspeccionado_id)
    SELECT 2026, 9, v_anillo_b, r1.proyecto_id, r2.proyecto_id
    FROM ss_inspeccion_cruzada_rotacion r1
    JOIN ss_inspeccion_cruzada_rotacion r2
      ON r2.anillo_id = v_anillo_b AND r2.orden = (r1.orden + 1) % 2
    WHERE r1.anillo_id = v_anillo_b;
END $$;

-- Verificación: debe mostrar 6 filas para Anillo A y 2 para Anillo B, sin proyectos NULL.
SELECT a.nombre AS anillo, pi.project_description AS inspector, pd.project_description AS inspeccionado
FROM ss_inspeccion_cruzada_programacion prog
JOIN ss_inspeccion_cruzada_anillo a ON a.id = prog.anillo_id
JOIN project pi ON pi.project_id = prog.proyecto_inspector_id
JOIN project pd ON pd.project_id = prog.proyecto_inspeccionado_id
WHERE prog.anio = 2026 AND prog.mes = 9
ORDER BY a.nombre, pi.project_description;

COMMIT;
