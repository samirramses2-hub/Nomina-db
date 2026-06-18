using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Calculos;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DirectivoController : ControllerBase
{
    private readonly NominaDbContext _context;

    public DirectivoController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("{empresaId}")]
    public async Task<ActionResult<object>> GetDirectivo(int empresaId, [FromQuery] int ejercicio = 2025)
    {
        var empresa = await _context.Empresas.FindAsync(empresaId);
        if (empresa is null) return NotFound();

        var empleados = await _context.Empleados
            .Where(e => e.EmpresaId == empresaId && e.Activo)
            .ToListAsync();

        var periodos = await _context.PeriodosNomina
            .Where(p => p.EmpresaId == empresaId && p.EjercicioFiscal == ejercicio)
            .OrderBy(p => p.FechaInicio)
            .ToListAsync();

        var incidencias = await _context.Incidencias
            .Where(i => periodos.Select(p => p.Id).Contains(i.PeriodoNominaId))
            .ToListAsync();

        var datosPeriodos = new List<object>();
        decimal costoAcumulado = 0;

        foreach (var periodo in periodos)
        {
            decimal netoTotal        = 0;
            decimal isrTotal         = 0;
            decimal imssObreroTotal  = 0;
            decimal imssPatronalTotal = 0;
            decimal costoEmpresaTotal = 0;
            int     totalFaltas      = 0;
            int     totalHorasExtra  = 0;

            var diasPeriodo = (periodo.FechaFin - periodo.FechaInicio).Days + 1;

            foreach (var emp in empleados)
            {
                var incEmp = incidencias
                    .Where(i => i.EmpleadoId == emp.Id && i.PeriodoNominaId == periodo.Id)
                    .ToList();

                var parametros = new ParametrosCalculo
                {
                    SalarioDiario        = emp.SalarioDiario,
                    DiasPeriodo          = diasPeriodo,
                    EjercicioFiscal      = ejercicio,
                    FaltasInjustificadas = incEmp.Where(i => i.Tipo == TipoIncidencia.FaltaInjustificada).Sum(i => i.Cantidad),
                    FaltasJustificadas   = incEmp.Where(i => i.Tipo == TipoIncidencia.FaltaJustificada).Sum(i => i.Cantidad),
                    DiasVacaciones       = incEmp.Where(i => i.Tipo == TipoIncidencia.Vacaciones).Sum(i => i.Cantidad),
                    HorasExtraSimples    = incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraSimple).Sum(i => i.Cantidad),
                    HorasExtraDobles     = incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraDoble).Sum(i => i.Cantidad),
                    HorasExtraTriples    = incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraTriple).Sum(i => i.Cantidad),
                    Bonos                = incEmp.Where(i => i.Tipo == TipoIncidencia.Bono).Sum(i => i.Cantidad),
                    DiasPrimaDominical   = incEmp.Where(i => i.Tipo == TipoIncidencia.PrimaDominical).Sum(i => i.Cantidad),
                };

                var calculo = MotorCalculo.Calcular(parametros);
                var imss    = MotorCalculo.CalcularCuotasIMSS(emp.SalarioDiario, diasPeriodo);

                netoTotal         += calculo.NetoPagar;
                isrTotal          += calculo.DetalleISR.ISRRetenido;
                imssObreroTotal   += imss.TotalObrero;
                imssPatronalTotal += imss.TotalPatronal;
                costoEmpresaTotal += calculo.TotalPercepciones + imss.TotalPatronal;
                totalFaltas       += (int)incEmp.Where(i => i.Tipo == TipoIncidencia.FaltaInjustificada).Sum(i => i.Cantidad);
                totalHorasExtra   += (int)incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraSimple ||
                                                              i.Tipo == TipoIncidencia.HoraExtraDoble).Sum(i => i.Cantidad);
            }

            costoAcumulado += costoEmpresaTotal;

            datosPeriodos.Add(new
            {
                periodoId        = periodo.Id,
                descripcion      = periodo.Descripcion.Length > 0
                    ? periodo.Descripcion
                    : $"{periodo.FechaInicio:dd/MM}",
                fechaInicio      = periodo.FechaInicio.ToString("dd/MM/yyyy"),
                fechaFin         = periodo.FechaFin.ToString("dd/MM/yyyy"),
                fechaPago        = periodo.FechaPago.ToString("dd/MM/yyyy"),
                estado           = periodo.Estado.ToString(),
                neto             = Math.Round(netoTotal, 2),
                isr              = Math.Round(isrTotal, 2),
                imssObrero       = Math.Round(imssObreroTotal, 2),
                imssPatronal     = Math.Round(imssPatronalTotal, 2),
                costoEmpresa     = Math.Round(costoEmpresaTotal, 2),
                costoAcumulado   = Math.Round(costoAcumulado, 2),
                faltas           = totalFaltas,
                horasExtra       = totalHorasExtra,
                empleados        = empleados.Count
            });
        }

        // Predicción para periodos futuros (regresión lineal simple)
        var periodosConDatos = datosPeriodos
            .Select(p => (decimal)p.GetType().GetProperty("costoEmpresa")!.GetValue(p)!)
            .Where(c => c > 0).ToList();

        decimal prediccionProximoPeriodo = 0;
        decimal tendencia = 0;
        if (periodosConDatos.Count >= 2)
        {
            var n     = periodosConDatos.Count;
            var sumX  = (decimal)(n * (n - 1)) / 2;
            var sumX2 = (decimal)(n * (n - 1) * (2 * n - 1)) / 6;
            var sumY  = periodosConDatos.Sum();
            var sumXY = periodosConDatos.Select((v, i) => v * i).Sum();

            tendencia = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            prediccionProximoPeriodo = Math.Round(periodosConDatos.Last() + tendencia, 2);
        }

        // Promedio del ejercicio
        var costoPromedioPeriodo = periodosConDatos.Any()
            ? Math.Round(periodosConDatos.Average(), 2) : 0;

        var costoTotal = Math.Round(costoAcumulado, 2);
        var periodosTotales = periodos.Count;
        var periodosAnio    = periodos.FirstOrDefault()?.TipoPeriodo switch
        {
            TipoPeriodo.Semanal  => 52,
            TipoPeriodo.Mensual  => 12,
            _ => 24
        };

        var proyeccionAnual = costoPromedioPeriodo > 0
            ? Math.Round(costoPromedioPeriodo * periodosAnio, 2) : 0;

        // Costo por empleado
        var costoPromedioEmpleado = empleados.Count > 0 && costoPromedioPeriodo > 0
            ? Math.Round(costoPromedioPeriodo / empleados.Count, 2) : 0;

        // Eficiencia (neto / costo empresa)
        var netoAcumulado = datosPeriodos
            .Sum(p => (decimal)p.GetType().GetProperty("neto")!.GetValue(p)!);
        var eficiencia = costoAcumulado > 0
            ? Math.Round(netoAcumulado / costoAcumulado * 100, 1) : 0;

        return Ok(new
        {
            empresa            = empresa.RazonSocial,
            rfc                = empresa.RFC,
            ejercicio,
            totalEmpleados     = empleados.Count,
            periodosCalculados = periodosConDatos.Count,
            periodosTotal      = periodosTotales,

            financiero = new
            {
                costoTotal,
                costoPromedioPeriodo,
                costoPromedioEmpleado,
                proyeccionAnual,
                prediccionProximoPeriodo,
                tendencia            = Math.Round(tendencia, 2),
                eficiencia,
                netoAcumulado        = Math.Round(netoAcumulado, 2),
                isrAcumulado         = Math.Round(datosPeriodos.Sum(p => (decimal)p.GetType().GetProperty("isr")!.GetValue(p)!), 2),
                imssPatronalAcumulado = Math.Round(datosPeriodos.Sum(p => (decimal)p.GetType().GetProperty("imssPatronal")!.GetValue(p)!), 2),
            },

            periodos = datosPeriodos,

            alertasDirectivo = GenerarAlertasDirectivo(
                datosPeriodos, tendencia, costoPromedioPeriodo, eficiencia)
        });
    }

    private List<object> GenerarAlertasDirectivo(
        List<object> periodos, decimal tendencia,
        decimal promedio, decimal eficiencia)
    {
        var alertas = new List<object>();

        if (tendencia > promedio * 0.05m)
            alertas.Add(new
            {
                nivel   = "warning",
                mensaje = $"El costo de nómina muestra tendencia alcista de ${tendencia:F2} por periodo.",
                accion  = "Revisar incrementos salariales y horas extra."
            });

        if (eficiencia < 70)
            alertas.Add(new
            {
                nivel   = "danger",
                mensaje = $"Solo el {eficiencia}% del gasto en nómina llega como neto al empleado.",
                accion  = "Revisar cargas fiscales y optimizar estructura salarial."
            });

        if (periodos.Count >= 2)
        {
            var ultimos = periodos.TakeLast(2).ToList();
            var ant     = (decimal)ultimos[0].GetType().GetProperty("costoEmpresa")!.GetValue(ultimos[0])!;
            var act     = (decimal)ultimos[1].GetType().GetProperty("costoEmpresa")!.GetValue(ultimos[1])!;
            if (ant > 0 && act > ant * 1.10m)
                alertas.Add(new
                {
                    nivel   = "danger",
                    mensaje = $"El costo del último periodo subió {((act - ant) / ant * 100):F1}% respecto al anterior.",
                    accion  = "Identificar empleados con incidencias inusuales o bonos extraordinarios."
                });
        }

        return alertas;
    }
}