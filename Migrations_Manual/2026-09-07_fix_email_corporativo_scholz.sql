-- ============================================================================
-- FIX: corrige el email_corporativo de la ficha de Samanta Scholz (worker_id
-- 12092, person_id 5426), que tenía "scholz@abril.pe" en vez de su correo
-- real "sscholz@abril.pe" (el mismo que usa para el login SSO).
-- ============================================================================

UPDATE workers
SET email_corporativo = 'sscholz@abril.pe',
    updated_at = now()
WHERE id = 12092 AND email_corporativo = 'scholz@abril.pe';

-- Verificación
-- SELECT id, email_corporativo FROM workers WHERE id = 12092;
-- Esperado: sscholz@abril.pe
