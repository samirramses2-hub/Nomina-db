using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NominaApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarDepartamentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DepartamentoId",
                table: "Empleados",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Puesto",
                table: "Empleados",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Departamentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CodigoDepartamento = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    JefeId = table.Column<int>(type: "int", nullable: true),
                    DepartamentoPadreId = table.Column<int>(type: "int", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departamentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Departamentos_Departamentos_DepartamentoPadreId",
                        column: x => x.DepartamentoPadreId,
                        principalTable: "Departamentos",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Departamentos_Empleados_JefeId",
                        column: x => x.JefeId,
                        principalTable: "Empleados",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Departamentos_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Empleados_DepartamentoId",
                table: "Empleados",
                column: "DepartamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Departamentos_DepartamentoPadreId",
                table: "Departamentos",
                column: "DepartamentoPadreId");

            migrationBuilder.CreateIndex(
                name: "IX_Departamentos_EmpresaId",
                table: "Departamentos",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Departamentos_JefeId",
                table: "Departamentos",
                column: "JefeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Empleados_Departamentos_DepartamentoId",
                table: "Empleados",
                column: "DepartamentoId",
                principalTable: "Departamentos",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Empleados_Departamentos_DepartamentoId",
                table: "Empleados");

            migrationBuilder.DropTable(
                name: "Departamentos");

            migrationBuilder.DropIndex(
                name: "IX_Empleados_DepartamentoId",
                table: "Empleados");

            migrationBuilder.DropColumn(
                name: "DepartamentoId",
                table: "Empleados");

            migrationBuilder.DropColumn(
                name: "Puesto",
                table: "Empleados");
        }
    }
}
