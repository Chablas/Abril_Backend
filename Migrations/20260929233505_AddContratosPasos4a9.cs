using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abril_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddContratosPasos4a9 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "arrival_observation",
                table: "project_contract",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "arrived_with_observations",
                table: "project_contract",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "contractor_notification_skipped",
                table: "project_contract",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "step6signed_gerente_general",
                table: "project_contract",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "step6signed_gerente_inmobiliario",
                table: "project_contract",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "step6signed_jefe_proyectos",
                table: "project_contract",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "project_contract_scanned_doc",
                columns: table => new
                {
                    project_contract_scanned_doc_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_contract_id = table.Column<int>(type: "integer", nullable: false),
                    slot = table.Column<int>(type: "integer", nullable: false),
                    file_url = table.Column<string>(type: "text", nullable: true),
                    original_file_name = table.Column<string>(type: "text", nullable: true),
                    storage_item_id = table.Column<string>(type: "text", nullable: true),
                    created_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: false),
                    updated_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_user_id = table.Column<int>(type: "integer", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_contract_scanned_doc", x => x.project_contract_scanned_doc_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_contract_scanned_doc");

            migrationBuilder.DropColumn(
                name: "arrival_observation",
                table: "project_contract");

            migrationBuilder.DropColumn(
                name: "arrived_with_observations",
                table: "project_contract");

            migrationBuilder.DropColumn(
                name: "contractor_notification_skipped",
                table: "project_contract");

            migrationBuilder.DropColumn(
                name: "step6signed_gerente_general",
                table: "project_contract");

            migrationBuilder.DropColumn(
                name: "step6signed_gerente_inmobiliario",
                table: "project_contract");

            migrationBuilder.DropColumn(
                name: "step6signed_jefe_proyectos",
                table: "project_contract");
        }
    }
}
