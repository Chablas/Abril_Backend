using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abril_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddContractorUserCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_ss_sctr_vidaley_project_proyecto_id",
                table: "ss_sctr_vidaley");

            migrationBuilder.DropForeignKey(
                name: "fk_ss_sctr_vidaley_worker_ss_sctr_vidaley_sctr_vida_ley_id",
                table: "ss_sctr_vidaley_worker");

            migrationBuilder.RenameColumn(
                name: "sctr_vida_ley_id",
                table: "ss_sctr_vidaley_worker",
                newName: "sctr_vidaley_id");

            migrationBuilder.RenameIndex(
                name: "ix_ss_sctr_vidaley_worker_sctr_vida_ley_id",
                table: "ss_sctr_vidaley_worker",
                newName: "ix_ss_sctr_vidaley_worker_sctr_vidaley_id");

            migrationBuilder.AddColumn<DateTime>(
                name: "fecha_inicio_cobertura",
                table: "ss_sctr_vidaley_worker",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "proyecto_id",
                table: "ss_sctr_vidaley",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<DateTime>(
                name: "fecha_inicio",
                table: "ss_sctr_vidaley",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tipo_poliza",
                table: "ss_sctr_vidaley",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "equipo_electrico",
                table: "ss_induccion",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "sharepoint_item_id",
                table: "project_sub_contractor_summary_sheet",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sharepoint_item_id",
                table: "project_sub_contractor_service_order",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sharepoint_item_id",
                table: "project_sub_contractor_schedule",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sharepoint_item_id",
                table: "project_sub_contractor_scanned_doc",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sharepoint_item_id",
                table: "project_sub_contractor_quotation_file",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sharepoint_item_id",
                table: "project_sub_contractor_promissory_note",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sharepoint_item_id",
                table: "project_sub_contractor_contract",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sharepoint_item_id",
                table: "project_sub_contractor_comparative_file",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sharepoint_item_id",
                table: "project_sub_contractor_budget",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sharepoint_item_id",
                table: "project_sub_contractor_attached_quotation",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "project_sub_contractor_package_id",
                table: "project_sub_contractor",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "es_abril",
                table: "contributor",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "project_sub_contractor_package",
                columns: table => new
                {
                    project_sub_contractor_package_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    file_url = table.Column<string>(type: "text", nullable: true),
                    original_file_name = table.Column<string>(type: "text", nullable: true),
                    sharepoint_item_id = table.Column<string>(type: "text", nullable: true),
                    created_datetime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: false),
                    updated_datetime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_user_id = table.Column<int>(type: "integer", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_sub_contractor_package", x => x.project_sub_contractor_package_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_project_sub_contractor_project_sub_contractor_package_id",
                table: "project_sub_contractor",
                column: "project_sub_contractor_package_id");

            migrationBuilder.AddForeignKey(
                name: "fk_project_sub_contractor_project_sub_contractor_package_proje",
                table: "project_sub_contractor",
                column: "project_sub_contractor_package_id",
                principalTable: "project_sub_contractor_package",
                principalColumn: "project_sub_contractor_package_id");

            migrationBuilder.AddForeignKey(
                name: "fk_ss_sctr_vidaley_project_proyecto_id",
                table: "ss_sctr_vidaley",
                column: "proyecto_id",
                principalTable: "project",
                principalColumn: "project_id");

            migrationBuilder.AddForeignKey(
                name: "fk_ss_sctr_vidaley_worker_ss_sctr_vidaley_sctr_vidaley_id",
                table: "ss_sctr_vidaley_worker",
                column: "sctr_vidaley_id",
                principalTable: "ss_sctr_vidaley",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_project_sub_contractor_project_sub_contractor_package_proje",
                table: "project_sub_contractor");

            migrationBuilder.DropForeignKey(
                name: "fk_ss_sctr_vidaley_project_proyecto_id",
                table: "ss_sctr_vidaley");

            migrationBuilder.DropForeignKey(
                name: "fk_ss_sctr_vidaley_worker_ss_sctr_vidaley_sctr_vidaley_id",
                table: "ss_sctr_vidaley_worker");

            migrationBuilder.DropTable(
                name: "project_sub_contractor_package");

            migrationBuilder.DropIndex(
                name: "ix_project_sub_contractor_project_sub_contractor_package_id",
                table: "project_sub_contractor");

            migrationBuilder.DropColumn(
                name: "fecha_inicio_cobertura",
                table: "ss_sctr_vidaley_worker");

            migrationBuilder.DropColumn(
                name: "fecha_inicio",
                table: "ss_sctr_vidaley");

            migrationBuilder.DropColumn(
                name: "tipo_poliza",
                table: "ss_sctr_vidaley");

            migrationBuilder.DropColumn(
                name: "equipo_electrico",
                table: "ss_induccion");

            migrationBuilder.DropColumn(
                name: "sharepoint_item_id",
                table: "project_sub_contractor_summary_sheet");

            migrationBuilder.DropColumn(
                name: "sharepoint_item_id",
                table: "project_sub_contractor_service_order");

            migrationBuilder.DropColumn(
                name: "sharepoint_item_id",
                table: "project_sub_contractor_schedule");

            migrationBuilder.DropColumn(
                name: "sharepoint_item_id",
                table: "project_sub_contractor_scanned_doc");

            migrationBuilder.DropColumn(
                name: "sharepoint_item_id",
                table: "project_sub_contractor_quotation_file");

            migrationBuilder.DropColumn(
                name: "sharepoint_item_id",
                table: "project_sub_contractor_promissory_note");

            migrationBuilder.DropColumn(
                name: "sharepoint_item_id",
                table: "project_sub_contractor_contract");

            migrationBuilder.DropColumn(
                name: "sharepoint_item_id",
                table: "project_sub_contractor_comparative_file");

            migrationBuilder.DropColumn(
                name: "sharepoint_item_id",
                table: "project_sub_contractor_budget");

            migrationBuilder.DropColumn(
                name: "sharepoint_item_id",
                table: "project_sub_contractor_attached_quotation");

            migrationBuilder.DropColumn(
                name: "project_sub_contractor_package_id",
                table: "project_sub_contractor");

            migrationBuilder.DropColumn(
                name: "es_abril",
                table: "contributor");

            migrationBuilder.RenameColumn(
                name: "sctr_vidaley_id",
                table: "ss_sctr_vidaley_worker",
                newName: "sctr_vida_ley_id");

            migrationBuilder.RenameIndex(
                name: "ix_ss_sctr_vidaley_worker_sctr_vidaley_id",
                table: "ss_sctr_vidaley_worker",
                newName: "ix_ss_sctr_vidaley_worker_sctr_vida_ley_id");

            migrationBuilder.AlterColumn<int>(
                name: "proyecto_id",
                table: "ss_sctr_vidaley",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_ss_sctr_vidaley_project_proyecto_id",
                table: "ss_sctr_vidaley",
                column: "proyecto_id",
                principalTable: "project",
                principalColumn: "project_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_ss_sctr_vidaley_worker_ss_sctr_vidaley_sctr_vida_ley_id",
                table: "ss_sctr_vidaley_worker",
                column: "sctr_vida_ley_id",
                principalTable: "ss_sctr_vidaley",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
