using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Calculos;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PTUController : ControllerBase
{
    private readonly NominaDbContext _context;

    public PTUController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpPost("calcular")]
    public async Task<ActionResult<object>> Calcular([FromBody] CalcularPTURequest req)
    {
        var empresa = await _context.Empresas.FindAsync(req.EmpresaId);
        if (empresa is null) return NotFound("Empresa no encontrada.");

        var empleados = await _context.Empleados
            .Where(e => e.EmpresaId == req.EmpresaId && e.Activo)
            .ToListAsync();

        var periodos = await _context.PeriodosNomina
            .Where(p => p.EmpresaId == req.EmpresaId && p.EjercicioFiscal == req.EjercicioFiscal)
            .ToListAsync();

        if (!periodos.Any())
            return BadRequest($"No hay periodos registrados para el ejercicio {req.EjercicioFiscal}.");

        var incidencias = await _context.Incidencias
            .Where(i => periodos.Select(p => p.Id).Contains(i.PeriodoNominaId))
            .ToListAsync();

        var montoPTU      = Math.Round(req.UtilidadFiscal * 0.10m, 2);
        var montoByDias   = Math.Round(montoPTU / 2, 2);
        var montoBySlario = Math.Round(montoPTU / 2, 2);

        // Calcular días trabajados y salarios por empleado
        var datosEmpleados = new List<DatoEmpleadoPTU>();

        foreach (var emp in empleados)
        {
            var incEmp       = incidencias.Where(i => i.EmpleadoId == emp.Id).ToList();
            int diasTrabajados = 0;
            decimal salarioTotal = 0;

            foreach (var periodo in periodos)
            {
                var diasPeriodo = (periodo.FechaFin - periodo.FechaInicio).Days + 1;
                var faltasInjust = incEmp.Where(i => i.PeriodoNominaId == periodo.Id && i.Tipo == TipoIncidencia.FaltaInjustificada).Sum(i => i.Cantidad);
                var incapacidad  = incEmp.Where(i => i.PeriodoNominaId == periodo.Id && i.Tipo == TipoIncidencia.IncapacidadIMSS).Sum(i => i.Cantidad);
                var licSinGoce   = incEmp.Where(i => i.PeriodoNominaId == periodo.Id && i.Tipo == TipoIncidencia.LicenciaSinGoce).Sum(i => i.Cantidad);

                // Días efectivos = días del periodo - faltas injustificadas - licencias sin goce
                var diasEfectivos = diasPeriodo - (int)faltasInjust - (int)licSinGoce;
                if (diasEfectivos < 0) diasEfectivos = 0;

                diasTrabajados += diasEfectivos;

                var parametros = new ParametrosCalculo
                {
                    SalarioDiario        = emp.SalarioDiario,
                    DiasPeriodo          = diasPeriodo,
                    EjercicioFiscal      = periodo.EjercicioFiscal,
                    FaltasInjustificadas = faltasInjust,
                    FaltasJustificadas   = incEmp.Where(i => i.PeriodoNominaId == periodo.Id && i.Tipo == TipoIncidencia.FaltaJustificada).Sum(i => i.Cantidad),
                    DiasVacaciones       = incEmp.Where(i => i.PeriodoNominaId == periodo.Id && i.Tipo == TipoIncidencia.Vacaciones).Sum(i => i.Cantidad),
                    HorasExtraSimples    = incEmp.Where(i => i.PeriodoNominaId == periodo.Id && i.Tipo == TipoIncidencia.HoraExtraSimple).Sum(i => i.Cantidad),
                    HorasExtraDobles     = incEmp.Where(i => i.PeriodoNominaId == periodo.Id && i.Tipo == TipoIncidencia.HoraExtraDoble).Sum(i => i.Cantidad),
                    HorasExtraTriples    = 0,
                    Bonos                = incEmp.Where(i => i.PeriodoNominaId == periodo.Id && i.Tipo == TipoIncidencia.Bono).Sum(i => i.Cantidad),
                    DiasPrimaDominical   = incEmp.Where(i => i.PeriodoNominaId == periodo.Id && i.Tipo == TipoIncidencia.PrimaDominical).Sum(i => i.Cantidad)
                };

                var calculo = MotorCalculo.Calcular(parametros);
                salarioTotal += calculo.TotalPercepciones;
            }

            datosEmpleados.Add(new DatoEmpleadoPTU
            {
                EmpleadoId     = emp.Id,
                Nombre         = $"{emp.Nombre} {emp.ApellidoPaterno} {emp.ApellidoMaterno}".Trim(),
                RFC            = emp.RFC,
                SalarioDiario  = emp.SalarioDiario,
                DiasTrabajados = diasTrabajados,
                SalarioTotal   = Math.Round(salarioTotal, 2)
            });
        }

        // Totales para calcular proporciones
        var totalDias    = datosEmpleados.Sum(e => e.DiasTrabajados);
        var totalSalario = datosEmpleados.Sum(e => e.SalarioTotal);

        // PTU por empleado
        var resultados = new List<object>();
        decimal totalRepartido = 0;

        foreach (var dato in datosEmpleados)
        {
            var ptuPorDias    = totalDias > 0    ? Math.Round(montoByDias   * dato.DiasTrabajados / totalDias, 2)    : 0;
            var ptuPorSalario = totalSalario > 0 ? Math.Round(montoBySlario * dato.SalarioTotal   / totalSalario, 2) : 0;
            var ptuTotal      = Math.Round(ptuPorDias + ptuPorSalario, 2);

            // Exención ISR: 15 días de salario mínimo (2025: $248.93 × 15 = $3,733.95)
            var exencion      = Math.Round(248.93m * 15, 2);
            var ptuGravada    = Math.Max(0, Math.Round(ptuTotal - exencion, 2));
            var ptuExenta     = Math.Min(ptuTotal, exencion);

            // ISR sobre PTU gravada (tasa promedio simplificada)
            var isrPTU = 0m;
            if (ptuGravada > 0)
            {
                // Usar tabla ISR quincenal como aproximación
                isrPTU = Math.Round(ptuGravada * 0.1m, 2); // Aproximación
            }

            var ptuNeta = Math.Round(ptuTotal - isrPTU, 2);
            totalRepartido += ptuTotal;

            resultados.Add(new
            {
                empleadoId     = dato.EmpleadoId,
                nombre         = dato.Nombre,
                rfc            = dato.RFC,
                diasTrabajados = dato.DiasTrabajados,
                salarioTotal   = dato.SalarioTotal,
                ptuPorDias,
                ptuPorSalario,
                ptuTotal,
                ptuExenta,
                ptuGravada,
                isrPTU,
                ptuNeta,
                explicacion    = $"Días: {dato.DiasTrabajados}/{totalDias} × ${montoByDias:F2} = ${ptuPorDias:F2} | " +
                                 $"Salario: ${dato.SalarioTotal:F2}/{totalSalario:F2} × ${montoBySlario:F2} = ${ptuPorSalario:F2} | " +
                                 $"Total: ${ptuTotal:F2} | Exento: ${ptuExenta:F2} | Gravado: ${ptuGravada:F2}"
            });
        }

        return Ok(new
        {
            empresa          = empresa.RazonSocial,
            ejercicio        = req.EjercicioFiscal,
            utilidadFiscal   = req.UtilidadFiscal,
            montoPTU,
            montoByDias,
            montoBySlario,
            totalEmpleados   = empleados.Count,
            totalDias,
            totalSalario     = Math.Round(totalSalario, 2),
            totalRepartido   = Math.Round(totalRepartido, 2),
            diferencia       = Math.Round(montoPTU - totalRepartido, 2),
            resultados
        });
    }
}

public class CalcularPTURequest
{
    public int EmpresaId { get; set; }
    public int EjercicioFiscal { get; set; }
    public decimal UtilidadFiscal { get; set; }
}

public class DatoEmpleadoPTU
{
    public int EmpleadoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string RFC { get; set; } = string.Empty;
    public decimal SalarioDiario { get; set; }
    public int DiasTrabajados { get; set; }
    public decimal SalarioTotal { get; set; }
}