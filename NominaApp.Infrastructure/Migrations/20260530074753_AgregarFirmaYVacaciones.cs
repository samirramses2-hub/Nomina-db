using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NominaApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarFirmaYVacaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaFirma",
                table: "CFDIs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IPFirma",
                table: "CFDIs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SolicitudesVacaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpleadoId = table.Column<int>(type: "int", nullable: false),
                    FechaSolicitud = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DiasSolicitados = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    ComentariosEmpleado = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ComentariosRRHH = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudesVacaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolicitudesVacaciones_Empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "Empleados",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesVacaciones_EmpleadoId",
                table: "SolicitudesVacaciones",
                column: "EmpleadoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SolicitudesVacaciones");

            migrationBuilder.DropColumn(
                name: "FechaFirma",
                table: "CFDIs");

            migrationBuilder.DropColumn(
                name: "IPFirma",
                table: "CFDIs");
        }
    }
}
