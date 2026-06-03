using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abril_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddEvAsignacionSupervisorAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "ev_asignacion_supervisor",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "updated_by_user_id",
                table: "ev_asignacion_supervisor",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "ev_asignacion_supervisor");

            migrationBuilder.DropColumn(
                name: "updated_by_user_id",
                table: "ev_asignacion_supervisor");
        }
    }
}
