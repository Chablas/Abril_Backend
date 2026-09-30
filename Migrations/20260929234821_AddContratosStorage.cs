using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abril_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddContratosStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "contract_file_url",
                table: "project_contract",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contract_original_file_name",
                table: "project_contract",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contract_storage_item_id",
                table: "project_contract",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "project_contract_folder",
                columns: table => new
                {
                    project_contract_folder_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    link_url = table.Column<string>(type: "text", nullable: false),
                    drive_id = table.Column<string>(type: "text", nullable: false),
                    folder_id = table.Column<string>(type: "text", nullable: false),
                    folder_name = table.Column<string>(type: "text", nullable: true),
                    web_url = table.Column<string>(type: "text", nullable: true),
                    created_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: false),
                    updated_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_user_id = table.Column<int>(type: "integer", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_contract_folder", x => x.project_contract_folder_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_contract_folder");

            migrationBuilder.DropColumn(
                name: "contract_file_url",
                table: "project_contract");

            migrationBuilder.DropColumn(
                name: "contract_original_file_name",
                table: "project_contract");

            migrationBuilder.DropColumn(
                name: "contract_storage_item_id",
                table: "project_contract");
        }
    }
}
