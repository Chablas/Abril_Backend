-- ============================================================================
-- Gestión Administrativa — lo personalizado que sobra (SOLO PROD)
-- DESPUÉS de 20260929_GaRolesPorFuncion.sql. Fecha: 2026-09-29
--
-- Generado con el auditor de solo lectura sobre prod (mismo IActoresResolver de la app, con la regla
-- nueva de obra: solo el tipo PROYECTO). Los ids son de prod: en dev o demo la guarda aborta.
--
--   1) 89 celdas de fichas (workers_actor_asignacion) IGUALES a lo que daría el sistema: quitarlas no
--      cambia a ningún aprobador, consolidador ni firmante; desde ahora manda el algoritmo y se mueve
--      solo si cambia el puesto o la obra. Son jefes cuyo gerente ya es el del algoritmo, gente de
--      oficina cuyo jefe es el de su área, y staff con el residente de su obra.
--   2) Finanzas (Revisores de Áreas): el aprobador de la salida y el jefe notificado eran Carlos
--      López Vallejos, que ya no está (retirado). Sin eso aprueba Romina Vera (gerente) y, cuando GTH
--      cargue el correo corporativo de Gonzalo Risco (el nuevo sub gerente), pasa solo a él.
--
-- NO se tocan las otras 238 celdas de fichas: 201 sostienen la aprobación de 67 trabajadores (sin
-- ellas caería en el buzón de GTH porque el algoritmo no encuentra a nadie) y 37 dicen algo
-- distinto a propósito o por la migración (ver el reporte del auditor).
--
-- Baja lógica (state = false). Cada bloque aborta si alguna fila ya no es la que se auditó. Al
-- final recalcula CONSOLIDADOR (misma regla que el backend).
-- ============================================================================

BEGIN;

-- ── 1) Fichas: iguales al algoritmo ────────────────────────────────────────
DO $$
BEGIN
    IF (SELECT count(*)
          FROM workers_actor_asignacion r
          JOIN (VALUES
                (7, 11966, 1, 12753),
                (8, 11966, 3, 12753),
                (9, 11966, 5, 12753),
                (37, 12166, 1, 11836),
                (38, 12166, 3, 11836),
                (39, 12166, 5, 11836),
                (22, 12305, 1, 12753),
                (23, 12305, 3, 12753),
                (24, 12305, 5, 12753),
                (31, 12432, 1, 11836),
                (32, 12432, 3, 11836),
                (33, 12432, 5, 11836),
                (40, 12435, 1, 11836),
                (41, 12435, 3, 11836),
                (42, 12435, 5, 11836),
                (47, 12465, 3, 12753),
                (48, 12465, 5, 12753),
                (49, 12663, 1, 11836),
                (50, 12663, 3, 11836),
                (51, 12663, 5, 11836),
                (100, 12677, 1, 12745),
                (101, 12677, 3, 12745),
                (102, 12677, 5, 12745),
                (34, 12745, 1, 11836),
                (35, 12745, 3, 11836),
                (36, 12745, 5, 11836),
                (19, 12771, 1, 12753),
                (20, 12771, 3, 12753),
                (21, 12771, 5, 12753),
                (10, 12835, 1, 12753),
                (11, 12835, 3, 12753),
                (12, 12835, 5, 12753),
                (97, 12839, 1, 11836),
                (98, 12839, 3, 11836),
                (99, 12839, 5, 11836),
                (88, 12869, 1, 11955),
                (181, 12944, 1, 12867),
                (182, 12944, 3, 12867),
                (183, 12944, 5, 12867),
                (76, 13013, 1, 11931),
                (4, 13022, 1, 12753),
                (5, 13022, 3, 12753),
                (6, 13022, 5, 12753),
                (28, 13140, 1, 11836),
                (29, 13140, 3, 11836),
                (30, 13140, 5, 11836),
                (13, 13255, 1, 12753),
                (14, 13255, 3, 12753),
                (15, 13255, 5, 12753),
                (16, 13376, 1, 12753),
                (17, 13376, 3, 12753),
                (18, 13376, 5, 12753),
                (94, 13555, 1, 12435),
                (95, 13555, 3, 12435),
                (96, 13555, 5, 12435),
                (82, 13591, 1, 11955),
                (322, 13602, 1, 13575),
                (61, 13616, 1, 12263),
                (55, 13632, 1, 12263),
                (43, 13664, 1, 11836),
                (44, 13664, 3, 11836),
                (45, 13664, 5, 11836),
                (85, 13707, 1, 11955),
                (91, 13717, 1, 14006),
                (92, 13717, 3, 14006),
                (93, 13717, 5, 14006),
                (67, 13774, 1, 12167),
                (103, 13853, 1, 11836),
                (104, 13853, 3, 11836),
                (105, 13853, 5, 11836),
                (73, 13948, 1, 13575),
                (148, 13994, 1, 13595),
                (149, 13994, 3, 13595),
                (150, 13994, 5, 13595),
                (25, 14006, 1, 12867),
                (26, 14006, 3, 12867),
                (27, 14006, 5, 12867),
                (287, 14074, 3, 11836),
                (288, 14074, 5, 11836),
                (106, 14228, 1, 13255),
                (107, 14228, 3, 13255),
                (108, 14228, 5, 13255),
                (1, 14453, 1, 12305),
                (2, 14453, 3, 12305),
                (3, 14453, 5, 12305),
                (298, 15386, 1, 12857),
                (199, 15428, 1, 11836),
                (200, 15428, 3, 11836),
                (201, 15428, 5, 11836)
               ) AS e(id, worker_id, actor_id, asignado_id)
            ON e.id = r.workers_actor_asignacion_id
           AND e.worker_id = r.worker_id AND e.actor_id = r.ga_actor_id AND e.asignado_id = r.asignado_id
         WHERE r.state AND r.active) <> 89 THEN
        RAISE EXCEPTION 'workers_actor_asignacion cambió desde la auditoría. Volver a correr el auditor. Abortado.';
    END IF;
END $$;

UPDATE workers_actor_asignacion SET state = false, updated_at = now()
 WHERE state AND workers_actor_asignacion_id IN (7, 8, 9, 37, 38, 39, 22, 23, 24, 31, 32, 33, 40, 41, 42, 47, 48, 49, 50, 51, 100, 101, 102, 34, 35, 36, 19, 20, 21, 10, 11, 12, 97, 98, 99, 88, 181, 182, 183, 76, 4, 5, 6, 28, 29, 30, 13, 14, 15, 16, 17, 18, 94, 95, 96, 82, 322, 61, 55, 43, 44, 45, 85, 91, 92, 93, 67, 103, 104, 105, 73, 148, 149, 150, 25, 26, 27, 287, 288, 106, 107, 108, 1, 2, 3, 298, 199, 200, 201);

-- ── 2) Finanzas: el personalizado apunta a alguien retirado ─────────────────
DO $$
BEGIN
    IF (SELECT count(*)
          FROM area_actor_asignacion a
          JOIN area_scope s ON s.area_scope_id = a.area_scope_id
          JOIN area_item ai ON ai.area_item_id = s.area_item_id
          JOIN workers w    ON w.id = a.worker_id
         WHERE a.area_actor_asignacion_id IN (2, 8)
           AND a.state
           AND ai.area_item_name = 'Finanzas'
           AND a.project_id IS NULL
           AND w.workers_estado_id NOT IN (1, 3)) <> 2 THEN
        RAISE EXCEPTION 'Las filas 2 y 8 de area_actor_asignacion ya no son el personalizado de Finanzas a un retirado. Abortado.';
    END IF;
END $$;

UPDATE area_actor_asignacion SET state = false, updated_at = now()
 WHERE state AND area_actor_asignacion_id IN (2, 8);

-- ── 3) CONSOLIDADOR exacto ─────────────────────────────────────────────────
WITH designados AS (
    SELECT p.user_id
    FROM area_actor_asignacion a
    JOIN workers w ON w.id = a.worker_id
    JOIN person p  ON p.person_id = w.person_id
    WHERE a.state AND a.active AND a.ga_actor_id = 4
      AND p.user_id IS NOT NULL
      AND lower(trim(w.email_corporativo)) LIKE '%@abril.pe'
    UNION
    SELECT p.user_id
    FROM workers_actor_asignacion r
    JOIN workers w ON w.id = r.asignado_id
    JOIN person p  ON p.person_id = w.person_id
    WHERE r.state AND r.active AND r.ga_actor_id = 4
      AND p.user_id IS NOT NULL
      AND lower(trim(w.email_corporativo)) LIKE '%@abril.pe'
),
cubiertos AS (
    SELECT ur.user_id
    FROM user_role ur
    JOIN role ro         ON ro.role_id   = ur.role_id AND ro.state
    JOIN role_feature rf ON rf.role_id   = ur.role_id
    JOIN feature f       ON f.feature_id = rf.feature_id
    WHERE ur.state AND ur.role_id <> 95
      AND f.feature_key IN ('gestion-administrativa.gestion-rendiciones', 'gestion-administrativa.consolidados')
    GROUP BY ur.user_id
    HAVING count(DISTINCT f.feature_key) = 2
),
objetivo AS (
    SELECT DISTINCT d.user_id
    FROM designados d
    JOIN app_user u ON u.user_id = d.user_id AND u.state
    WHERE NOT EXISTS (SELECT 1 FROM cubiertos c WHERE c.user_id = d.user_id)
),
alta AS (
    INSERT INTO user_role (user_id, role_id, created_user_id)
    SELECT o.user_id, 95, 1
    FROM objetivo o
    ON CONFLICT (user_id, role_id) DO UPDATE
        SET state = true, active = true, updated_date_time = now(), updated_user_id = 1
        WHERE NOT user_role.state
    RETURNING user_id
)
DELETE FROM user_role ur
WHERE ur.role_id = 95
  AND NOT EXISTS (SELECT 1 FROM objetivo o WHERE o.user_id = ur.user_id);

COMMIT;
