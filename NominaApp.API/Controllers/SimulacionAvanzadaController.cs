using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Calculos;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SimulacionAvanzadaController : ControllerBase
{
    private readonly NominaDbContext _context;

    public SimulacionAvanzadaController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpPost("nomina")]
    public async Task<ActionResult<object>> SimularNomina([FromBody] SimulacionRequest req)
    {
        var empresa = await _context.Empresas.FindAsync(req.EmpresaId);
        if (empresa is null) return NotFound("Empresa no encontrada.");

        var empleados = await _context.Empleados
            .Where(e => e.EmpresaId == req.EmpresaId && e.Activo)
            .ToListAsync();

        if (!empleados.Any())
            return BadRequest("No hay empleados activos en esta empresa.");

        var diasPeriodo = req.TipoPeriodo switch { 1 => 7, 3 => 30, _ => 15 };
        var periodosAnio = req.TipoPeriodo switch { 1 => 52, 3 => 12, _ => 24 };

        var resultadosActual  = new List<ResultadoSimEmpleado>();
        var resultadosNuevo   = new List<ResultadoSimEmpleado>();

        foreach (var emp in empleados)
        {
            // Salario actual
            var salActual = emp.SalarioDiario;
            var resActual = CalcularEmpleado(emp, salActual, diasPeriodo, req.EjercicioFiscal);
            resultadosActual.Add(resActual);

            // Salario nuevo según tipo de simulación
            decimal salNuevo = req.TipoSimulacion switch
            {
                "porcentaje"  => Math.Round(salActual * (1 + req.Valor / 100), 2),
                "monto"       => Math.Round(salActual + req.Valor, 2),
                "absoluto"    => req.Valor,
                "empleado"    => emp.Id == req.EmpleadoId ? req.Valor : salActual,
                _             => salActual
            };

            if (salNuevo < 248.93m) salNuevo = 248.93m;
            var resNuevo = CalcularEmpleado(emp, salNuevo, diasPeriodo, req.EjercicioFiscal);
            resultadosNuevo.Add(resNuevo);
        }

        // Totales por periodo
        var totalNetoActual    = Math.Round(resultadosActual.Sum(r => r.Neto), 2);
        var totalNetoNuevo     = Math.Round(resultadosNuevo.Sum(r => r.Neto), 2);
        var totalCostoActual   = Math.Round(resultadosActual.Sum(r => r.CostoEmpresa), 2);
        var totalCostoNuevo    = Math.Round(resultadosNuevo.Sum(r => r.CostoEmpresa), 2);
        var totalISRActual     = Math.Round(resultadosActual.Sum(r => r.ISR), 2);
        var totalISRNuevo      = Math.Round(resultadosNuevo.Sum(r => r.ISR), 2);
        var totalIMSSActual    = Math.Round(resultadosActual.Sum(r => r.IMSSPatronal), 2);
        var totalIMSSNuevo     = Math.Round(resultadosNuevo.Sum(r => r.IMSSPatronal), 2);

        return Ok(new
        {
            empresa        = empresa.RazonSocial,
            tipoSimulacion = req.TipoSimulacion,
            valor          = req.Valor,
            tipoPeriodo    = diasPeriodo,
            periodosAnio,
            porPeriodo = new
            {
                netoActual    = totalNetoActual,
                netoNuevo     = totalNetoNuevo,
                difNeto       = Math.Round(totalNetoNuevo - totalNetoActual, 2),
                costoActual   = totalCostoActual,
                costoNuevo    = totalCostoNuevo,
                difCosto      = Math.Round(totalCostoNuevo - totalCostoActual, 2),
                isrActual     = totalISRActual,
                isrNuevo      = totalISRNuevo,
                difISR        = Math.Round(totalISRNuevo - totalISRActual, 2),
                imssActual    = totalIMSSActual,
                imssNuevo     = totalIMSSNuevo,
                difIMSS       = Math.Round(totalIMSSNuevo - totalIMSSActual, 2),
            },
            anual = new
            {
                costoActual   = Math.Round(totalCostoActual * periodosAnio, 2),
                costoNuevo    = Math.Round(totalCostoNuevo  * periodosAnio, 2),
                difCosto      = Math.Round((totalCostoNuevo - totalCostoActual) * periodosAnio, 2),
                netoActual    = Math.Round(totalNetoActual  * periodosAnio, 2),
                netoNuevo     = Math.Round(totalNetoNuevo   * periodosAnio, 2),
                difNeto       = Math.Round((totalNetoNuevo  - totalNetoActual)  * periodosAnio, 2),
                isrActual     = Math.Round(totalISRActual   * periodosAnio, 2),
                isrNuevo      = Math.Round(totalISRNuevo    * periodosAnio, 2),
                difISR        = Math.Round((totalISRNuevo   - totalISRActual)   * periodosAnio, 2),
            },
            empleados = empleados.Select((emp, idx) => new
            {
                empleadoId     = emp.Id,
                nombre         = $"{emp.Nombre} {emp.ApellidoPaterno}".Trim(),
                salarioActual  = resultadosActual[idx].SalarioDiario,
                salarioNuevo   = resultadosNuevo[idx].SalarioDiario,
                difSalario     = Math.Round(resultadosNuevo[idx].SalarioDiario - resultadosActual[idx].SalarioDiario, 2),
                netoActual     = resultadosActual[idx].Neto,
                netoNuevo      = resultadosNuevo[idx].Neto,
                difNeto        = Math.Round(resultadosNuevo[idx].Neto - resultadosActual[idx].Neto, 2),
                costoActual    = resultadosActual[idx].CostoEmpresa,
                costoNuevo     = resultadosNuevo[idx].CostoEmpresa,
                difCosto       = Math.Round(resultadosNuevo[idx].CostoEmpresa - resultadosActual[idx].CostoEmpresa, 2),
                isrActual      = resultadosActual[idx].ISR,
                isrNuevo       = resultadosNuevo[idx].ISR,
                difISR         = Math.Round(resultadosNuevo[idx].ISR - resultadosActual[idx].ISR, 2),
            }).ToList()
        });
    }

    private ResultadoSimEmpleado CalcularEmpleado(Empleado emp, decimal salario, int diasPeriodo, int ejercicio)
    {
        var parametros = new ParametrosCalculo
        {
            SalarioDiario   = salario,
            DiasPeriodo     = diasPeriodo,
            EjercicioFiscal = ejercicio,
        };
        var calculo = MotorCalculo.Calcular(parametros);
        var imss    = MotorCalculo.CalcularCuotasIMSS(salario, diasPeriodo);

        return new ResultadoSimEmpleado
        {
            SalarioDiario = salario,
            Neto          = calculo.NetoPagar,
            ISR           = calculo.DetalleISR.ISRRetenido,
            IMSSObrero    = imss.TotalObrero,
            IMSSPatronal  = imss.TotalPatronal,
            CostoEmpresa  = Math.Round(calculo.TotalPercepciones + imss.TotalPatronal, 2)
        };
    }
}

public class SimulacionRequest
{
    public int     EmpresaId      { get; set; }
    public int     EjercicioFiscal { get; set; } = 2025;
    public int     TipoPeriodo    { get; set; } = 2;
    public string  TipoSimulacion { get; set; } = "porcentaje";
    public decimal Valor          { get; set; }
    public int?    EmpleadoId     { get; set; }
}

public class ResultadoSimEmpleado
{
    public decimal SalarioDiario { get; set; }
    public decimal Neto          { get; set; }
    public decimal ISR           { get; set; }
    public decimal IMSSObrero    { get; set; }
    public decimal IMSSPatronal  { get; set; }
    public decimal CostoEmpresa  { get; set; }
}