using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abril_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerMilestone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTA: esta migración fue generada por `dotnet ef migrations add` y arrastraba de
            // arrastre todo el backlog de cambios que ya se aplicaron a mano en producción vía
            // Migrations_Manual/ (columnas de "curso" en curso_kit_marca_y_plantillas.sql, y las
            // tablas ss_ats_*/ss_petar_* en 2026-09-26_ats_digital.sql, 2026-09-27_ats_digital_iperc.sql
            // y los *_petar*.sql) — el snapshot de EF nunca tuvo una migración para ese trabajo.
            // Se recortó a mano para que el Up()/Down() de ESTA migración solo cree/elimine
            // "owner_milestone" (lo único realmente nuevo); el resto del modelo se deja reflejado
            // en el snapshot (ya coincide con la BD real) sin volver a ejecutar su SQL acá.
            migrationBuilder.CreateTable(
                name: "owner_milestone",
                columns: table => new
                {
                    owner_milestone_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    description = table.Column<string>(type: "text", nullable: false),
                    milestone_id = table.Column<int>(type: "integer", nullable: false),
                    owner_milestone_order = table.Column<int>(type: "integer", nullable: false),
                    created_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: false),
                    updated_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_user_id = table.Column<int>(type: "integer", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_owner_milestone", x => x.owner_milestone_id);
                });

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "owner_milestone");
        }
    }
}
