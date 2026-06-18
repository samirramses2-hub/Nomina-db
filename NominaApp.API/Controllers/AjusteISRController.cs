using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Calculos;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AjusteISRController : ControllerBase
{
    private readonly NominaDbContext _context;

    public AjusteISRController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("{empresaId}/{ejercicio}")]
    public async Task<ActionResult<object>> GetAjuste(int empresaId, int ejercicio)
    {
        var empresa = await _context.Empresas.FindAsync(empresaId);
        if (empresa is null) return NotFound("Empresa no encontrada.");

        var empleados = await _context.Empleados
            .Where(e => e.EmpresaId == empresaId && e.Activo)
            .ToListAsync();

        var periodos = await _context.PeriodosNomina
            .Where(p => p.EmpresaId == empresaId && p.EjercicioFiscal == ejercicio)
            .OrderBy(p => p.FechaInicio)
            .ToListAsync();

        if (!periodos.Any())
            return BadRequest($"No hay periodos registrados para el ejercicio {ejercicio}.");

        var resultados = new List<object>();

        // Tabla anual ISR 2025
        var tablaAnual = new List<(decimal LI, decimal LS, decimal CF, decimal Tasa)>
        {
            (0.01m,         23512.03m,    0m,          1.92m),
            (23512.04m,     199241.16m,   451.26m,     6.40m),
            (199241.17m,    349993.14m,   11730.06m,   10.88m),
            (349993.15m,    407844.72m,   28112.94m,   16.00m),
            (407844.73m,    488641.08m,   37410.42m,   17.92m),
            (488641.09m,    984360.84m,   51876.66m,   21.36m),
            (984360.85m,    1553864.52m,  157769.76m,  23.52m),
            (1553864.53m,   2966924.88m,  291897.24m,  30.00m),
            (2966924.89m,   3954499.20m,  715649.40m,  32.00m),
            (3954499.21m,   11868000.00m, 1031007.72m, 34.00m),
            (11868000.01m,  decimal.MaxValue, 3722654.52m, 35.00m)
        };

        var tablaSubsidioAnual = new List<(decimal LI, decimal LS, decimal Subsidio)>
        {
            (0.01m,       29839.26m,    9768.48m),
            (29839.27m,   44939.28m,    9768.48m),
            (44939.29m,   58477.20m,    9763.92m),
            (58477.21m,   63155.52m,    8636.16m),
            (63155.53m,   72229.20m,    8246.40m),
            (72229.21m,   92613.66m,    7401.12m),
            (92613.67m,   95688.72m,    0m),
            (95688.73m,   200161.20m,   0m),
            (200161.21m,  decimal.MaxValue, 0m)
        };

        foreach (var empleado in empleados)
        {
            decimal isrRetenidoTotal   = 0;
            decimal ingresosAnuales    = 0;
            decimal subsidioEntregado  = 0;
            int     periodosContados   = 0;

            foreach (var periodo in periodos)
            {
                var incidencias = await _context.Incidencias
                    .Where(i => i.EmpleadoId == empleado.Id && i.PeriodoNominaId == periodo.Id)
                    .ToListAsync();

                var diasPeriodo = (periodo.FechaFin - periodo.FechaInicio).Days + 1;

                var parametros = new ParametrosCalculo
                {
                    SalarioDiario        = empleado.SalarioDiario,
                    DiasPeriodo          = diasPeriodo,
                    EjercicioFiscal      = ejercicio,
                    FaltasInjustificadas = incidencias.Where(i => i.Tipo == TipoIncidencia.FaltaInjustificada).Sum(i => i.Cantidad),
                    FaltasJustificadas   = incidencias.Where(i => i.Tipo == TipoIncidencia.FaltaJustificada).Sum(i => i.Cantidad),
                    DiasVacaciones       = incidencias.Where(i => i.Tipo == TipoIncidencia.Vacaciones).Sum(i => i.Cantidad),
                    HorasExtraSimples    = incidencias.Where(i => i.Tipo == TipoIncidencia.HoraExtraSimple).Sum(i => i.Cantidad),
                    HorasExtraDobles     = incidencias.Where(i => i.Tipo == TipoIncidencia.HoraExtraDoble).Sum(i => i.Cantidad),
                    HorasExtraTriples    = incidencias.Where(i => i.Tipo == TipoIncidencia.HoraExtraTriple).Sum(i => i.Cantidad),
                    Bonos                = incidencias.Where(i => i.Tipo == TipoIncidencia.Bono).Sum(i => i.Cantidad),
                    DiasPrimaDominical   = incidencias.Where(i => i.Tipo == TipoIncidencia.PrimaDominical).Sum(i => i.Cantidad),
                    IncapacidadIMSS       = incidencias.Where(i => i.Tipo == TipoIncidencia.IncapacidadIMSS).Sum(i => i.Cantidad),
IncapacidadRiesgo     = incidencias.Where(i => i.Tipo == TipoIncidencia.IncapacidadRiesgo).Sum(i => i.Cantidad),
IncapacidadMaternidad = incidencias.Where(i => i.Tipo == TipoIncidencia.IncapacidadMaternidad).Sum(i => i.Cantidad),
LicenciaSinGoce       = incidencias.Where(i => i.Tipo == TipoIncidencia.LicenciaSinGoce).Sum(i => i.Cantidad),
PrimaVacacional       = incidencias.Where(i => i.Tipo == TipoIncidencia.PrimaVacacional).Sum(i => i.Cantidad),
Aguinaldo             = incidencias.Where(i => i.Tipo == TipoIncidencia.Aguinaldo).Sum(i => i.Cantidad),
DescuentoInfonavit    = incidencias.Where(i => i.Tipo == TipoIncidencia.DescuentoInfonavit).Sum(i => i.Cantidad),
                };

                var calculo = MotorCalculo.Calcular(parametros);
                isrRetenidoTotal  += calculo.DetalleISR.ISRRetenido;
                ingresosAnuales   += calculo.TotalPercepciones;
                subsidioEntregado += calculo.DetalleISR.SubsidioEmpleo;
                periodosContados++;
            }

            // Calcular ISR anual con tabla anual
            var renglon = tablaAnual.FirstOrDefault(r =>
                ingresosAnuales >= r.LI && ingresosAnuales <= r.LS);

            decimal isrAnual = 0;
            string explicacionISR = "";

            if (renglon != default)
            {
                var excedente      = Math.Round(ingresosAnuales - renglon.LI, 2);
                var impuestoPrevio = Math.Round(excedente * (renglon.Tasa / 100), 2);
                isrAnual           = Math.Round(impuestoPrevio + renglon.CF, 2);
                explicacionISR     = $"Ingresos anuales ${ingresosAnuales:F2} × tasa {renglon.Tasa}% + cuota fija ${renglon.CF:F2} = ISR anual ${isrAnual:F2}";
            }

            // Subsidio anual
            var renglonSub = tablaSubsidioAnual.FirstOrDefault(r =>
                ingresosAnuales >= r.LI && ingresosAnuales <= r.LS);
            var subsidioAnual = renglonSub.Subsidio;

            var isrAnualNeto   = Math.Round(isrAnual - subsidioAnual, 2);
            if (isrAnualNeto < 0) isrAnualNeto = 0;

            var diferencia     = Math.Round(isrRetenidoTotal - isrAnualNeto, 2);
            var resultado      = diferencia > 0 ? "devolucion" : diferencia < 0 ? "cargo" : "correcto";

            resultados.Add(new
            {
                empleadoId         = empleado.Id,
                nombre             = $"{empleado.Nombre} {empleado.ApellidoPaterno} {empleado.ApellidoMaterno}".Trim(),
                rfc                = empleado.RFC,
                periodosContados,
                ingresosAnuales    = Math.Round(ingresosAnuales, 2),
                isrRetenidoTotal   = Math.Round(isrRetenidoTotal, 2),
                subsidioEntregado  = Math.Round(subsidioEntregado, 2),
                isrAnual           = isrAnual,
                subsidioAnual      = subsidioAnual,
                isrAnualNeto,
                diferencia         = Math.Abs(diferencia),
                resultado,
                explicacion        = resultado == "devolucion"
                    ? $"Se retuvo ${isrRetenidoTotal:F2} pero el ISR anual es ${isrAnualNeto:F2}. El empleado tiene una devolución de ${Math.Abs(diferencia):F2}."
                    : resultado == "cargo"
                    ? $"Se retuvo ${isrRetenidoTotal:F2} pero el ISR anual es ${isrAnualNeto:F2}. El empleado tiene un cargo de ${Math.Abs(diferencia):F2}."
                    : "El ISR retenido coincide exactamente con el ISR anual.",
                explicacionISR
            });
        }

        return Ok(new
        {
            empresa    = empresa.RazonSocial,
            ejercicio,
            periodos   = periodos.Count,
            resultados
        });
    }
}