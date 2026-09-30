using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abril_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddContratosFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTA: esta migración fue generada por `dotnet ef migrations add` y arrastraba de
            // arrastre el backlog del merge reciente de origin/master (tablas ga_actor*/
            // area_actor_asignacion/project_tipo/project_ciclo_vida/project_torre/etc. y los drops
            // de area_consolidadores/area_revisores/workers_revisores) — todo eso ya se aplicó a
            // producción a mano vía Migrations/Manual/*.sql (20260925_GaActoresUnificados.sql,
            // 20260928_ProyectosTipoYCicloVida.sql, etc.), el snapshot de EF nunca tuvo su propia
            // migración para ese trabajo. Mismo patrón de drift ya documentado en sesiones
            // anteriores (AddCronogramaTemplateItem, AddOwnerMilestone). Se recortó a mano para que
            // el Up()/Down() de ESTA migración solo cree/elimine las 3 tablas de Contratos.
            migrationBuilder.CreateTable(
                name: "project_contract",
                columns: table => new
                {
                    project_contract_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    contractor_id = table.Column<int>(type: "integer", nullable: false),
                    work_specialty_id = table.Column<int>(type: "integer", nullable: false),
                    project_contract_status_id = table.Column<int>(type: "integer", nullable: false),
                    contract_number = table.Column<int>(type: "integer", nullable: true),
                    service_description = table.Column<string>(type: "text", nullable: true),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    currency_id = table.Column<int>(type: "integer", nullable: false),
                    contractor_email = table.Column<string>(type: "text", nullable: true),
                    signing_date = table.Column<DateOnly>(type: "date", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    term_days = table.Column<int>(type: "integer", nullable: true),
                    detalle_servicios = table.Column<string>(type: "text", nullable: true),
                    folder_name = table.Column<string>(type: "text", nullable: true),
                    created_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: false),
                    updated_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_user_id = table.Column<int>(type: "integer", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_contract", x => x.project_contract_id);
                });

            migrationBuilder.CreateTable(
                name: "project_contract_milestone",
                columns: table => new
                {
                    project_contract_milestone_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_contract_id = table.Column<int>(type: "integer", nullable: false),
                    project_contract_milestone_order = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    percentage = table.Column<decimal>(type: "numeric", nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: true),
                    paid_date = table.Column<DateOnly>(type: "date", nullable: true),
                    cheque_recibo = table.Column<string>(type: "text", nullable: true),
                    observation = table.Column<string>(type: "text", nullable: true),
                    created_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: false),
                    updated_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_user_id = table.Column<int>(type: "integer", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_contract_milestone", x => x.project_contract_milestone_id);
                });

            migrationBuilder.CreateTable(
                name: "project_contract_status",
                columns: table => new
                {
                    project_contract_status_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_contract_status_description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_contract_status", x => x.project_contract_status_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_contract_milestone");

            migrationBuilder.DropTable(
                name: "project_contract");

            migrationBuilder.DropTable(
                name: "project_contract_status");
        }
    }
}
