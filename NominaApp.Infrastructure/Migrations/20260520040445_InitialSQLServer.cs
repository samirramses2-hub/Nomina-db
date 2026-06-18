using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NominaApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSQLServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Empresas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RazonSocial = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RFC = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    RegimenFiscal = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DomicilioFiscal = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empresas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeclaracionesPTU",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    EjercicioFiscal = table.Column<int>(type: "int", nullable: false),
                    UtilidadFiscal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MontoRepartir = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FechaDeclaracion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaPago = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeclaracionesPTU", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeclaracionesPTU_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Empleados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApellidoPaterno = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApellidoMaterno = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RFC = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    CURP = table.Column<string>(type: "nvarchar(18)", maxLength: 18, nullable: false),
                    NSS = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    Banco = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CuentaBancaria = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CLABE = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SalarioDiario = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TipoContrato = table.Column<int>(type: "int", nullable: false),
                    TipoPeriodo = table.Column<int>(type: "int", nullable: false),
                    FechaIngreso = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CodigoEmpleado = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empleados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Empleados_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PeriodosNomina",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    NumeroPeriodo = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Descripcion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaPago = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    TipoPeriodo = table.Column<int>(type: "int", nullable: false),
                    EjercicioFiscal = table.Column<int>(type: "int", nullable: false),
                    TipoEspecial = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodosNomina", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeriodosNomina_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReglaPersonalizada",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    Prioridad = table.Column<int>(type: "int", nullable: false),
                    Disparador = table.Column<int>(type: "int", nullable: false),
                    CondicionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AccionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VecesEjecutada = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReglaPersonalizada", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReglaPersonalizada_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "HistorialSalarial",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpleadoId = table.Column<int>(type: "int", nullable: false),
                    SalarioDiario = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FechaVigencia = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialSalarial", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialSalarial_Empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "Empleados",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MovimientosIMSS",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpleadoId = table.Column<int>(type: "int", nullable: false),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    TipoMovimiento = table.Column<int>(type: "int", nullable: false),
                    FechaMovimiento = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SalarioDiarioIntegrado = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosIMSS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosIMSS_Empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "Empleados",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MovimientosIMSS_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Rol = table.Column<int>(type: "int", nullable: false),
                    EmpresaId = table.Column<int>(type: "int", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EmpleadoId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Usuarios_Empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "Empleados",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Usuarios_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CFDIs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpleadoId = table.Column<int>(type: "int", nullable: false),
                    PeriodoNominaId = table.Column<int>(type: "int", nullable: false),
                    UUID = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RFCEmisor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RFCReceptor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    MotivoCancelacion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UUIDSustitucion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaTimbrado = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaCancelacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CFDIs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CFDIs_Empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "Empleados",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CFDIs_PeriodosNomina_PeriodoNominaId",
                        column: x => x.PeriodoNominaId,
                        principalTable: "PeriodosNomina",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ColaTimbrado",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpleadoId = table.Column<int>(type: "int", nullable: false),
                    PeriodoNominaId = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    Intentos = table.Column<int>(type: "int", nullable: false),
                    MaxIntentos = table.Column<int>(type: "int", nullable: false),
                    ProximoIntento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimoError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UUID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Diferido = table.Column<bool>(type: "bit", nullable: false),
                    FechaDiferido = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaCompletado = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColaTimbrado", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ColaTimbrado_Empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "Empleados",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ColaTimbrado_PeriodosNomina_PeriodoNominaId",
                        column: x => x.PeriodoNominaId,
                        principalTable: "PeriodosNomina",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Incidencias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpleadoId = table.Column<int>(type: "int", nullable: false),
                    PeriodoNominaId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EmpleadoId1 = table.Column<int>(type: "int", nullable: true),
                    PeriodoNominaId1 = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Incidencias_Empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "Empleados",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Incidencias_Empleados_EmpleadoId1",
                        column: x => x.EmpleadoId1,
                        principalTable: "Empleados",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Incidencias_PeriodosNomina_PeriodoNominaId",
                        column: x => x.PeriodoNominaId,
                        principalTable: "PeriodosNomina",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Incidencias_PeriodosNomina_PeriodoNominaId1",
                        column: x => x.PeriodoNominaId1,
                        principalTable: "PeriodosNomina",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CFDIs_EmpleadoId",
                table: "CFDIs",
                column: "EmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_CFDIs_PeriodoNominaId",
                table: "CFDIs",
                column: "PeriodoNominaId");

            migrationBuilder.CreateIndex(
                name: "IX_ColaTimbrado_EmpleadoId",
                table: "ColaTimbrado",
                column: "EmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_ColaTimbrado_PeriodoNominaId",
                table: "ColaTimbrado",
                column: "PeriodoNominaId");

            migrationBuilder.CreateIndex(
                name: "IX_DeclaracionesPTU_EmpresaId",
                table: "DeclaracionesPTU",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Empleados_EmpresaId",
                table: "Empleados",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Empresas_RFC",
                table: "Empresas",
                column: "RFC",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistorialSalarial_EmpleadoId",
                table: "HistorialSalarial",
                column: "EmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidencias_EmpleadoId",
                table: "Incidencias",
                column: "EmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidencias_EmpleadoId1",
                table: "Incidencias",
                column: "EmpleadoId1");

            migrationBuilder.CreateIndex(
                name: "IX_Incidencias_PeriodoNominaId",
                table: "Incidencias",
                column: "PeriodoNominaId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidencias_PeriodoNominaId1",
                table: "Incidencias",
                column: "PeriodoNominaId1");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosIMSS_EmpleadoId",
                table: "MovimientosIMSS",
                column: "EmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosIMSS_EmpresaId",
                table: "MovimientosIMSS",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodosNomina_EmpresaId",
                table: "PeriodosNomina",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_ReglaPersonalizada_EmpresaId",
                table: "ReglaPersonalizada",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_EmpleadoId",
                table: "Usuarios",
                column: "EmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_EmpresaId",
                table: "Usuarios",
                column: "EmpresaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CFDIs");

            migrationBuilder.DropTable(
                name: "ColaTimbrado");

            migrationBuilder.DropTable(
                name: "DeclaracionesPTU");

            migrationBuilder.DropTable(
                name: "HistorialSalarial");

            migrationBuilder.DropTable(
                name: "Incidencias");

            migrationBuilder.DropTable(
                name: "MovimientosIMSS");

            migrationBuilder.DropTable(
                name: "ReglaPersonalizada");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "PeriodosNomina");

            migrationBuilder.DropTable(
                name: "Empleados");

            migrationBuilder.DropTable(
                name: "Empresas");
        }
    }
}
