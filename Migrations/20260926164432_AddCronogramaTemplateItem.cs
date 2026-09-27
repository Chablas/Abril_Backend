using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abril_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddCronogramaTemplateItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cronograma_template_item",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tipo_cronograma = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    nombre = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    nivel = table.Column<int>(type: "integer", nullable: false),
                    es_padre = table.Column<bool>(type: "boolean", nullable: false),
                    parent_codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    predecesora_codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: false),
                    updated_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_user_id = table.Column<int>(type: "integer", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cronograma_template_item", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cronograma_template_item_tipo_cronograma_codigo",
                table: "cronograma_template_item",
                columns: new[] { "tipo_cronograma", "codigo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cronograma_template_item");
        }
    }
}
