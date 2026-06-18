using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Calculos;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;
using NominaApp.API.DTOs;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly NominaDbContext _context;

    public DashboardController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("{empresaId}")]
    public async Task<ActionResult<DashboardDto>> GetDashboard(int empresaId)
    {
        var empresa = await _context.Empresas.FindAsync(empresaId);
        if (empresa is null) return NotFound("Empresa no encontrada.");

        var empleados = await _context.Empleados
            .Include(e => e.Departamento)
            .Where(e => e.EmpresaId == empresaId && e.Activo)
            .ToListAsync();

        var periodos = await _context.PeriodosNomina
            .Where(p => p.EmpresaId == empresaId)
            .OrderByDescending(p => p.FechaInicio)
            .Take(6)
            .ToListAsync();

        var dashboard = new DashboardDto
        {
            TotalEmpleados = empleados.Count,
            TotalPeriodos  = periodos.Count,
            CfdisTimbrados = 0
        };

        // Costos por periodo
        foreach (var periodo in periodos.OrderBy(p => p.FechaInicio))
        {
            var incidencias = await _context.Incidencias
                .Where(i => i.PeriodoNominaId == periodo.Id)
                .ToListAsync();

            var diasPeriodo = (periodo.FechaFin - periodo.FechaInicio).Days + 1;
            decimal totalPercepciones = 0;
            decimal totalNeto         = 0;
            decimal totalIMSSPatronal = 0;

            foreach (var empleado in empleados)
            {
                var incEmp = incidencias.Where(i => i.EmpleadoId == empleado.Id).ToList();
                var parametros = new ParametrosCalculo
                {
                    SalarioDiario        = empleado.SalarioDiario,
                    DiasPeriodo          = diasPeriodo,
                    EjercicioFiscal      = periodo.EjercicioFiscal,
                    FaltasInjustificadas = incEmp.Where(i => i.Tipo == TipoIncidencia.FaltaInjustificada).Sum(i => i.Cantidad),
                    FaltasJustificadas   = incEmp.Where(i => i.Tipo == TipoIncidencia.FaltaJustificada).Sum(i => i.Cantidad),
                    DiasVacaciones       = incEmp.Where(i => i.Tipo == TipoIncidencia.Vacaciones).Sum(i => i.Cantidad),
                    HorasExtraSimples    = incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraSimple).Sum(i => i.Cantidad),
                    HorasExtraDobles     = incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraDoble).Sum(i => i.Cantidad),
                    HorasExtraTriples    = incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraTriple).Sum(i => i.Cantidad),
                    Bonos                = incEmp.Where(i => i.Tipo == TipoIncidencia.Bono).Sum(i => i.Cantidad),
                    DiasPrimaDominical   = incEmp.Where(i => i.Tipo == TipoIncidencia.PrimaDominical).Sum(i => i.Cantidad),
                    IncapacidadIMSS       = incidencias.Where(i => i.Tipo == TipoIncidencia.IncapacidadIMSS).Sum(i => i.Cantidad),
IncapacidadRiesgo     = incidencias.Where(i => i.Tipo == TipoIncidencia.IncapacidadRiesgo).Sum(i => i.Cantidad),
IncapacidadMaternidad = incidencias.Where(i => i.Tipo == TipoIncidencia.IncapacidadMaternidad).Sum(i => i.Cantidad),
LicenciaSinGoce       = incidencias.Where(i => i.Tipo == TipoIncidencia.LicenciaSinGoce).Sum(i => i.Cantidad),
PrimaVacacional       = incidencias.Where(i => i.Tipo == TipoIncidencia.PrimaVacacional).Sum(i => i.Cantidad),
Aguinaldo             = incidencias.Where(i => i.Tipo == TipoIncidencia.Aguinaldo).Sum(i => i.Cantidad),
DescuentoInfonavit    = incidencias.Where(i => i.Tipo == TipoIncidencia.DescuentoInfonavit).Sum(i => i.Cantidad),
                };

                var calculo = MotorCalculo.Calcular(parametros);
                var imss    = MotorCalculo.CalcularCuotasIMSS(empleado.SalarioDiario, diasPeriodo);

                totalPercepciones += calculo.TotalPercepciones;
                totalNeto         += calculo.NetoPagar;
                totalIMSSPatronal += imss.TotalPatronal;
            }

            dashboard.CostosPorPeriodo.Add(new CostoPorPeriodoDto
            {
                Periodo           = $"{periodo.FechaInicio:dd/MM} - {periodo.FechaFin:dd/MM/yy}",
                TotalPercepciones = Math.Round(totalPercepciones, 2),
                TotalNeto         = Math.Round(totalNeto, 2),
                CostoEmpresa      = Math.Round(totalPercepciones + totalIMSSPatronal, 2),
                NumEmpleados      = empleados.Count
            });
        }

        // KPIs del último periodo
        if (dashboard.CostosPorPeriodo.Any())
        {
            var ultimo = dashboard.CostosPorPeriodo.Last();
            var ultimoPeriodo = periodos.OrderBy(p => p.FechaInicio).Last();
            
            dashboard.CostoTotalUltimoPeriodo   = ultimo.CostoEmpresa;
            dashboard.NetoTotalUltimoPeriodo     = ultimo.TotalNeto;
            dashboard.IMSSPatronalUltimoPeriodo  = Math.Round(ultimo.CostoEmpresa - ultimo.TotalPercepciones, 2);

            // Calcular Costos por Departamento
            var incidenciasUltimo = await _context.Incidencias
                .Where(i => i.PeriodoNominaId == ultimoPeriodo.Id)
                .ToListAsync();
                
            var diasPeriodo = (ultimoPeriodo.FechaFin - ultimoPeriodo.FechaInicio).Days + 1;
            
            var deptosAgrupados = empleados.GroupBy(e => e.Departamento?.Nombre ?? "Sin Departamento");
            
            foreach (var grupo in deptosAgrupados)
            {
                decimal costoDepto = 0;
                foreach (var emp in grupo)
                {
                    var incEmp = incidenciasUltimo.Where(i => i.EmpleadoId == emp.Id).ToList();
                    var param = new ParametrosCalculo
                    {
                        SalarioDiario = emp.SalarioDiario,
                        DiasPeriodo = diasPeriodo,
                        EjercicioFiscal = ultimoPeriodo.EjercicioFiscal,
                        FaltasInjustificadas = incEmp.Where(i => i.Tipo == TipoIncidencia.FaltaInjustificada).Sum(i => i.Cantidad),
                        FaltasJustificadas = incEmp.Where(i => i.Tipo == TipoIncidencia.FaltaJustificada).Sum(i => i.Cantidad),
                        DiasVacaciones = incEmp.Where(i => i.Tipo == TipoIncidencia.Vacaciones).Sum(i => i.Cantidad),
                        HorasExtraSimples = incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraSimple).Sum(i => i.Cantidad),
                        HorasExtraDobles = incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraDoble).Sum(i => i.Cantidad),
                        HorasExtraTriples = incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraTriple).Sum(i => i.Cantidad),
                        Bonos = incEmp.Where(i => i.Tipo == TipoIncidencia.Bono).Sum(i => i.Cantidad),
                        DiasPrimaDominical = incEmp.Where(i => i.Tipo == TipoIncidencia.PrimaDominical).Sum(i => i.Cantidad),
                        IncapacidadIMSS = incEmp.Where(i => i.Tipo == TipoIncidencia.IncapacidadIMSS).Sum(i => i.Cantidad),
                        IncapacidadRiesgo = incEmp.Where(i => i.Tipo == TipoIncidencia.IncapacidadRiesgo).Sum(i => i.Cantidad),
                        IncapacidadMaternidad = incEmp.Where(i => i.Tipo == TipoIncidencia.IncapacidadMaternidad).Sum(i => i.Cantidad),
                        LicenciaSinGoce = incEmp.Where(i => i.Tipo == TipoIncidencia.LicenciaSinGoce).Sum(i => i.Cantidad),
                        PrimaVacacional = incEmp.Where(i => i.Tipo == TipoIncidencia.PrimaVacacional).Sum(i => i.Cantidad),
                        Aguinaldo = incEmp.Where(i => i.Tipo == TipoIncidencia.Aguinaldo).Sum(i => i.Cantidad),
                        DescuentoInfonavit = incEmp.Where(i => i.Tipo == TipoIncidencia.DescuentoInfonavit).Sum(i => i.Cantidad),
                    };
                    
                    var calc = MotorCalculo.Calcular(param);
                    var imss = MotorCalculo.CalcularCuotasIMSS(emp.SalarioDiario, diasPeriodo);
                    costoDepto += calc.TotalPercepciones + imss.TotalPatronal;
                }
                
                dashboard.CostosPorDepartamento.Add(new CostoPorDepartamentoDto
                {
                    Departamento = grupo.Key,
                    CostoTotal = Math.Round(costoDepto, 2),
                    NumEmpleados = grupo.Count(),
                    PorcentajeDelTotal = ultimo.CostoEmpresa > 0 ? Math.Round((costoDepto / ultimo.CostoEmpresa) * 100, 1) : 0
                });
            }
            
            dashboard.CostosPorDepartamento = dashboard.CostosPorDepartamento.OrderByDescending(c => c.CostoTotal).ToList();

            // Calcular Índice de Ausentismo y Rotación
            var totalInasistencias = incidenciasUltimo.Where(i => i.Tipo == TipoIncidencia.FaltaInjustificada || i.Tipo == TipoIncidencia.FaltaJustificada).Sum(i => i.Cantidad);
            var diasEsperados = empleados.Count * diasPeriodo;
            dashboard.IndiceAusentismo = diasEsperados > 0 ? Math.Round((decimal)totalInasistencias / diasEsperados * 100, 2) : 0;

            var hace30Dias = DateTime.UtcNow.AddDays(-30);
            var altasRecientes = empleados.Count(e => e.FechaIngreso >= hace30Dias);
            dashboard.RotacionPersonal = empleados.Count > 0 ? Math.Round((decimal)altasRecientes / empleados.Count * 100, 2) : 0;
        }

        // Alertas
var alertas = new List<AlertaDto>();

// 1. Sin periodo abierto
var periodoAbierto = await _context.PeriodosNomina
    .FirstOrDefaultAsync(p => p.EmpresaId == empresaId && p.Estado == EstadoPeriodo.Abierto);

if (periodoAbierto is null)
    alertas.Add(new AlertaDto
    {
        Tipo    = "Periodo",
        Mensaje = "No hay un periodo abierto. Crea uno para procesar la nómina.",
        Nivel   = "warning"
    });

// 2. Periodo próximo a vencer
if (periodoAbierto != null)
{
    var diasParaCierre = (periodoAbierto.FechaFin.Date - DateTime.UtcNow.Date).Days;
    if (diasParaCierre <= 2 && diasParaCierre >= 0)
        alertas.Add(new AlertaDto
        {
            Tipo    = "Cierre",
            Mensaje = $"El periodo cierra en {diasParaCierre} día(s). Verifica que todas las incidencias estén capturadas.",
            Nivel   = "warning"
        });

    if (diasParaCierre < 0)
        alertas.Add(new AlertaDto
        {
            Tipo    = "Cierre",
            Mensaje = $"El periodo venció hace {Math.Abs(diasParaCierre)} día(s) y sigue abierto. Ciérralo o timbra la nómina.",
            Nivel   = "danger"
        });
}

// 3. Salario menor al mínimo
foreach (var emp in empleados.Where(e => e.SalarioDiario < 248.93m))
    alertas.Add(new AlertaDto
    {
        Tipo    = "Salario",
        Mensaje = $"{emp.Nombre} {emp.ApellidoPaterno} tiene salario de ${emp.SalarioDiario}/día, menor al mínimo vigente ($248.93/día).",
        Nivel   = "danger"
    });

// 4. Sin CLABE registrada
foreach (var emp in empleados.Where(e => string.IsNullOrEmpty(e.CLABE)))
    alertas.Add(new AlertaDto
    {
        Tipo    = "Banco",
        Mensaje = $"{emp.Nombre} {emp.ApellidoPaterno} no tiene CLABE registrada. No se podrá generar el layout de pago.",
        Nivel   = "warning"
    });

// 5. Empleados sin incidencias en el periodo abierto
if (periodoAbierto != null && empleados.Count > 0)
{
    var incPeriodo = await _context.Incidencias
        .Where(i => i.PeriodoNominaId == periodoAbierto.Id)
        .Select(i => i.EmpleadoId)
        .Distinct()
        .ToListAsync();

    var sinIncidencias = empleados.Where(e => !incPeriodo.Contains(e.Id)).ToList();
    if (sinIncidencias.Count == empleados.Count)
        alertas.Add(new AlertaDto
        {
            Tipo    = "Incidencias",
            Mensaje = "Ningún empleado tiene incidencias capturadas en el periodo actual. ¿Ya registraste faltas, vacaciones o horas extra?",
            Nivel   = "info"
        });
}

// 6. Más de 3 faltas injustificadas en el periodo
if (periodoAbierto != null)
{
    var incidenciasFaltas = await _context.Incidencias
        .Include(i => i.Empleado)
        .Where(i => i.PeriodoNominaId == periodoAbierto.Id
                 && i.Tipo == TipoIncidencia.FaltaInjustificada)
        .ToListAsync();

    var conMuchasFaltas = incidenciasFaltas
        .GroupBy(i => i.EmpleadoId)
        .Where(g => g.Sum(i => i.Cantidad) >= 3)
        .ToList();

    foreach (var grupo in conMuchasFaltas)
    {
        var emp = grupo.First().Empleado;
        alertas.Add(new AlertaDto
        {
            Tipo    = "Faltas",
            Mensaje = $"{emp.Nombre} {emp.ApellidoPaterno} tiene {grupo.Sum(i => i.Cantidad)} faltas injustificadas este periodo.",
            Nivel   = "warning"
        });
    }
}

// 7. Sin empleados
if (empleados.Count == 0)
    alertas.Add(new AlertaDto
    {
        Tipo    = "Empleados",
        Mensaje = "No tienes empleados registrados. Agrega al menos uno para calcular nómina.",
        Nivel   = "info"
    });

// 8. Antigüedad — empleados con más de 1 año sin revisión de salario
var unAnoAtras = DateTime.UtcNow.AddYears(-1);
foreach (var emp in empleados.Where(e => e.FechaIngreso <= unAnoAtras && e.SalarioDiario <= 350))
    alertas.Add(new AlertaDto
    {
        Tipo    = "Salario",
        Mensaje = $"{emp.Nombre} {emp.ApellidoPaterno} tiene más de 1 año en la empresa con salario de ${emp.SalarioDiario}/día. Considera una revisión salarial.",
        Nivel   = "info"
    });

    dashboard.Alertas = alertas;
        return Ok(dashboard);
    }
}