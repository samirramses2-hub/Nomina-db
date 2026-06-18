using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NominaApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPrestamos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Asistencias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpleadoId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HoraEntrada = table.Column<TimeSpan>(type: "time", nullable: true),
                    HoraSalida = table.Column<TimeSpan>(type: "time", nullable: true),
                    HoraEntradaEsperada = table.Column<TimeSpan>(type: "time", nullable: true),
                    HoraSalidaEsperada = table.Column<TimeSpan>(type: "time", nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    MinutosRetardo = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HorasTrabajadas = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HorasExtra = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetodoRegistro = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CodigoQR = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Asistencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Asistencias_Empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "Empleados",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "HorariosEmpleados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpleadoId = table.Column<int>(type: "int", nullable: false),
                    DiaSemana = table.Column<int>(type: "int", nullable: false),
                    HoraEntrada = table.Column<TimeSpan>(type: "time", nullable: false),
                    HoraSalida = table.Column<TimeSpan>(type: "time", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HorariosEmpleados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HorariosEmpleados_Empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "Empleados",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Prestamos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpleadoId = table.Column<int>(type: "int", nullable: false),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    MontoTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MontoRestante = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PagoQuincenal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NumeroPagos = table.Column<int>(type: "int", nullable: false),
                    PagosRealizados = table.Column<int>(type: "int", nullable: false),
                    TasaInteres = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FechaOtorgamiento = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaLiquidacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    Concepto = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Autorizador = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prestamos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Prestamos_Empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "Empleados",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Prestamos_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PagosPrestamo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrestamoId = table.Column<int>(type: "int", nullable: false),
                    PeriodoNominaId = table.Column<int>(type: "int", nullable: true),
                    MontoPago = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MontoInteres = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MontoCapital = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SaldoRestante = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NumeroPago = table.Column<int>(type: "int", nullable: false),
                    FechaPago = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PagosPrestamo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PagosPrestamo_Prestamos_PrestamoId",
                        column: x => x.PrestamoId,
                        principalTable: "Prestamos",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Asistencias_EmpleadoId",
                table: "Asistencias",
                column: "EmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_HorariosEmpleados_EmpleadoId",
                table: "HorariosEmpleados",
                column: "EmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_PagosPrestamo_PrestamoId",
                table: "PagosPrestamo",
                column: "PrestamoId");

            migrationBuilder.CreateIndex(
                name: "IX_Prestamos_EmpleadoId",
                table: "Prestamos",
                column: "EmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_Prestamos_EmpresaId",
                table: "Prestamos",
                column: "EmpresaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Asistencias");

            migrationBuilder.DropTable(
                name: "HorariosEmpleados");

            migrationBuilder.DropTable(
                name: "PagosPrestamo");

            migrationBuilder.DropTable(
                name: "Prestamos");
        }
    }
}
