-- Migración manual (pgAdmin) — umbrales de stock crítico por material (Almacén).
-- Ejecutar directamente contra la BD PostgreSQL. No usar dotnet ef.
-- Ambas columnas son NULL por defecto: un material sin umbrales configurados
-- simplemente no aparece en el dashboard de "Materiales con Stock Crítico".

ALTER TABLE almacen_materiales ADD COLUMN IF NOT EXISTS punto_reorden  NUMERIC(14, 2);
ALTER TABLE almacen_materiales ADD COLUMN IF NOT EXISTS stock_seguridad NUMERIC(14, 2);
