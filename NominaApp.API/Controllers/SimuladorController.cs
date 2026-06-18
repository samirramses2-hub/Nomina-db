using Microsoft.AspNetCore.Mvc;
using NominaApp.Core.Calculos;
using NominaApp.API.DTOs;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SimuladorController : ControllerBase
{
    [HttpPost]
    public ActionResult<object> Simular(SimuladorRequestDto dto)
    {
        if (dto.SalarioDiario <= 0)
            return BadRequest("El salario diario debe ser mayor a cero.");

        if (dto.DiasPeriodo <= 0 || dto.DiasPeriodo > 31)
            return BadRequest("Los días del periodo deben estar entre 1 y 31.");

        var parametros = new ParametrosCalculo
        {
            SalarioDiario        = dto.SalarioDiario,
            DiasPeriodo          = dto.DiasPeriodo,
            EjercicioFiscal      = dto.EjercicioFiscal,
            FaltasInjustificadas = dto.FaltasInjustificadas,
            FaltasJustificadas   = dto.FaltasJustificadas,
            DiasVacaciones       = dto.DiasVacaciones,
            HorasExtraSimples    = dto.HorasExtraSimples,
            HorasExtraDobles     = dto.HorasExtraDobles,
            HorasExtraTriples    = 0,
            Bonos                = dto.Bonos,
            DiasPrimaDominical   = dto.DiasPrimaDominical
        };

        try
        {
            var calculo = MotorCalculo.Calcular(parametros);
            var imss    = MotorCalculo.CalcularCuotasIMSS(dto.SalarioDiario, dto.DiasPeriodo);

            return Ok(new
            {
                calculo,
                imss,
                costoTotalEmpresa = Math.Round(calculo.TotalPercepciones + imss.TotalPatronal, 2),
                salarioMensualEstimado = Math.Round(calculo.NetoPagar * (30m / dto.DiasPeriodo), 2)
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("inverso")]
    public ActionResult<object> SimularInverso(SimuladorInversoRequestDto dto)
    {
        if (dto.SueldoNetoDeseado <= 0)
            return BadRequest("El sueldo neto deseado debe ser mayor a cero.");

        if (dto.DiasPeriodo <= 0 || dto.DiasPeriodo > 31)
            return BadRequest("Los días del periodo deben estar entre 1 y 31.");

        // Búsqueda binaria para encontrar el Salario Diario Bruto
        decimal targetMensual = dto.SueldoNetoDeseado;
        decimal targetPeriodo = targetMensual; // Si es mensual, es lo mismo
        
        // Ajustamos la meta del periodo si los días no son 30 (ej. quincenal = meta/2)
        if (dto.DiasPeriodo != 30)
        {
            targetPeriodo = (targetMensual / 30m) * dto.DiasPeriodo;
        }

        decimal minDiario = (targetPeriodo / dto.DiasPeriodo); // Límite inferior asumiendo 0 retenciones
        decimal maxDiario = (targetPeriodo / dto.DiasPeriodo) * 2; // Límite superior holgado
        decimal currentDiario = (minDiario + maxDiario) / 2;
        
        var parametros = new ParametrosCalculo
        {
            DiasPeriodo = dto.DiasPeriodo,
            EjercicioFiscal = dto.EjercicioFiscal
        };

        NominaApp.Core.Calculos.ResultadoCalculo calculoActual = null;
        NominaApp.Core.Calculos.MotorCalculo.CuotasIMSS imssActual = null;

        // Búsqueda binaria (máximo 40 iteraciones nos dan precisión de centavos)
        for (int i = 0; i < 40; i++)
        {
            parametros.SalarioDiario = currentDiario;
            calculoActual = MotorCalculo.Calcular(parametros);
            
            if (calculoActual.NetoPagar < targetPeriodo)
            {
                minDiario = currentDiario;
            }
            else
            {
                maxDiario = currentDiario;
            }
            
            currentDiario = (minDiario + maxDiario) / 2;
        }

        // Una vez encontrado, preparamos el resultado final
        imssActual = MotorCalculo.CalcularCuotasIMSS(currentDiario, dto.DiasPeriodo);

        return Ok(new
        {
            salarioDiarioBrutoRequerido = Math.Round(currentDiario, 2),
            salarioMensualBrutoRequerido = Math.Round(currentDiario * 30m, 2),
            calculo = calculoActual,
            imss = imssActual,
            costoTotalEmpresaPeriodo = Math.Round(calculoActual.TotalPercepciones + imssActual.TotalPatronal, 2),
            costoTotalEmpresaMensual = Math.Round((calculoActual.TotalPercepciones + imssActual.TotalPatronal) * (30m / dto.DiasPeriodo), 2)
        });
    }
}