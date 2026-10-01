using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abril_Backend.Migrations
{
    /// <inheritdoc />
    public partial class RenameContractIdToContributorId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTA: recortada a mano — arrastraba el backlog de ATS "grupo"/capataz (ya aplicado a
            // producción vía Migrations_Manual/2026-09-30_ats_*.sql). Mismo patrón de drift ya
            // documentado en migraciones anteriores de esta sesión. Up()/Down() de ESTA migración
            // solo renombran project_contract.contractor_id -> contributor_id (tabla vacía, sin
            // datos que perder).
            migrationBuilder.RenameColumn(
                name: "contractor_id",
                table: "project_contract",
                newName: "contributor_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "contributor_id",
                table: "project_contract",
                newName: "contractor_id");
        }
    }
}
