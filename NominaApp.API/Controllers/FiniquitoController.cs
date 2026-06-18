using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FiniquitoController : ControllerBase
{
    private readonly NominaDbContext _context;
    private const decimal UMA_2025 = 108.57m;
    private const decimal SALARIO_MINIMO_2025 = 248.93m;

    public FiniquitoController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("calcular/{empleadoId}")]
    public async Task<ActionResult<object>> Calcular(
        int empleadoId,
        [FromQuery] DateTime fechaBaja,
        [FromQuery] string tipoSeparacion,
        [FromQuery] decimal? salarioDiarioIntegrado = null)
    {
        var empleado = await _context.Empleados
            .Include(e => e.Empresa)
            .FirstOrDefaultAsync(e => e.Id == empleadoId);
        if (empleado is null) return NotFound("Empleado no encontrado.");

        var fechaIngreso  = empleado.FechaIngreso;
        var sdi           = salarioDiarioIntegrado ?? empleado.SalarioDiario;
        var antiguedadDias = (fechaBaja - fechaIngreso).Days;
        var anosServicio  = antiguedadDias / 365.0m;
        var anosCompletos = (int)anosServicio;
        var mesesFraccion = (int)((anosServicio - anosCompletos) * 12);

        // Días de vacaciones según LFT
        var diasVacaciones = CalcularDiasVacaciones(anosCompletos);
        var diasVacProp    = Math.Round(diasVacaciones * (mesesFraccion / 12.0m), 2);

        // Días trabajados en el último periodo (desde inicio del mes hasta fecha de baja)
        var inicioUltimoPeriodo = new DateTime(fechaBaja.Year, fechaBaja.Month, fechaBaja.Day <= 15 ? 1 : 16);
        var diasUltimoPeriodo   = (fechaBaja - inicioUltimoPeriodo).Days + 1;

        var conceptos = new List<ConceptoFiniquito>();

        // ── FINIQUITO (aplica siempre) ────────────────────────────
        // 1. Días trabajados último periodo
        var montoUltimoPeriodo = Math.Round(empleado.SalarioDiario * diasUltimoPeriodo, 2);
        conceptos.Add(new ConceptoFiniquito
        {
            Tipo        = "finiquito",
            Concepto    = "Días trabajados último periodo",
            Dias        = diasUltimoPeriodo,
            Monto       = montoUltimoPeriodo,
            Gravado     = montoUltimoPeriodo,
            Exento      = 0,
            Explicacion = $"${empleado.SalarioDiario:F2}/día × {diasUltimoPeriodo} días = ${montoUltimoPeriodo:F2}"
        });

        // 2. Vacaciones proporcionales
        var montoVacProp = Math.Round(empleado.SalarioDiario * diasVacProp, 2);
        // Exento: hasta $248.93 × días de vacaciones por ley
        var exentoVac    = Math.Round(Math.Min(montoVacProp, SALARIO_MINIMO_2025 * diasVacaciones), 2);
        conceptos.Add(new ConceptoFiniquito
        {
            Tipo        = "finiquito",
            Concepto    = "Vacaciones proporcionales",
            Dias        = diasVacProp,
            Monto       = montoVacProp,
            Gravado     = Math.Max(0, montoVacProp - exentoVac),
            Exento      = exentoVac,
            Explicacion = $"{diasVacaciones} días/año × {mesesFraccion}/12 meses = {diasVacProp:F2} días × ${empleado.SalarioDiario:F2} = ${montoVacProp:F2}"
        });

        // 3. Prima vacacional proporcional (25%)
        var montoPrimaVac = Math.Round(montoVacProp * 0.25m, 2);
        var exentoPrimaVac = Math.Round(Math.Min(montoPrimaVac, UMA_2025 * 15), 2);
        conceptos.Add(new ConceptoFiniquito
        {
            Tipo        = "finiquito",
            Concepto    = "Prima vacacional proporcional",
            Dias        = diasVacProp,
            Monto       = montoPrimaVac,
            Gravado     = Math.Max(0, montoPrimaVac - exentoPrimaVac),
            Exento      = exentoPrimaVac,
            Explicacion = $"25% × ${montoVacProp:F2} vacaciones = ${montoPrimaVac:F2} (exento hasta 15 UMAs = ${UMA_2025 * 15:F2})"
        });

        // 4. Aguinaldo proporcional
        var diasAguinaldo    = 15;
        var mesesTrabajados  = (int)((fechaBaja - new DateTime(fechaBaja.Year, 1, 1)).TotalDays / 30.4);
        var aguinaldoProp    = Math.Round(empleado.SalarioDiario * diasAguinaldo * mesesTrabajados / 12, 2);
        var exentoAguinaldo  = Math.Round(Math.Min(aguinaldoProp, UMA_2025 * 30), 2);
        conceptos.Add(new ConceptoFiniquito
        {
            Tipo        = "finiquito",
            Concepto    = "Aguinaldo proporcional",
            Dias        = diasAguinaldo,
            Monto       = aguinaldoProp,
            Gravado     = Math.Max(0, aguinaldoProp - exentoAguinaldo),
            Exento      = exentoAguinaldo,
            Explicacion = $"15 días × ${empleado.SalarioDiario:F2} × {mesesTrabajados}/12 meses = ${aguinaldoProp:F2} (exento hasta 30 UMAs = ${UMA_2025 * 30:F2})"
        });

        // ── LIQUIDACIÓN (solo si es despido injustificado) ────────
        if (tipoSeparacion == "despido")
        {
            // 5. Tres meses de salario (LFT Art. 50 fracción II)
            var tresMeses = Math.Round(sdi * 90, 2);
            // Exento: hasta 90 días × SMA × factor (SAT: exento hasta $234,772 aprox 2025)
            var exentoTresMeses = Math.Round(Math.Min(tresMeses, SALARIO_MINIMO_2025 * 90), 2);
            conceptos.Add(new ConceptoFiniquito
            {
                Tipo        = "liquidacion",
                Concepto    = "Tres meses de salario (LFT Art. 50)",
                Dias        = 90,
                Monto       = tresMeses,
                Gravado     = Math.Max(0, tresMeses - exentoTresMeses),
                Exento      = exentoTresMeses,
                Explicacion = $"SDI ${sdi:F2}/día × 90 días = ${tresMeses:F2} (LFT Art. 50 fracción II)"
            });

            // 6. Veinte días por año de servicio (LFT Art. 50 fracción II)
            var veinteXAnio = Math.Round(sdi * 20 * anosServicio, 2);
            // Exento: hasta 20 días de SMA por año
            var exentoVeinteXAnio = Math.Round(Math.Min(veinteXAnio, SALARIO_MINIMO_2025 * 20 * anosServicio), 2);
            conceptos.Add(new ConceptoFiniquito
            {
                Tipo        = "liquidacion",
                Concepto    = "20 días por año trabajado",
                Dias        = (decimal)(20 * anosServicio),
                Monto       = veinteXAnio,
                Gravado     = Math.Max(0, veinteXAnio - exentoVeinteXAnio),
                Exento      = exentoVeinteXAnio,
                Explicacion = $"SDI ${sdi:F2} × 20 días × {anosServicio:F2} años = ${veinteXAnio:F2}"
            });

            // 7. Prima de antigüedad (LFT Art. 162) — 12 días de SMA por año
            // Tope: 2 veces el salario mínimo por día
            var salarioTopePrima = Math.Min(sdi, SALARIO_MINIMO_2025 * 2);
            var primaAntiguedad  = Math.Round(salarioTopePrima * 12 * anosServicio, 2);
            var exentoPrima      = Math.Round(Math.Min(primaAntiguedad, SALARIO_MINIMO_2025 * 12 * anosServicio), 2);
            conceptos.Add(new ConceptoFiniquito
            {
                Tipo        = "liquidacion",
                Concepto    = "Prima de antigüedad (LFT Art. 162)",
                Dias        = (decimal)(12 * anosServicio),
                Monto       = primaAntiguedad,
                Gravado     = Math.Max(0, primaAntiguedad - exentoPrima),
                Exento      = exentoPrima,
                Explicacion = $"12 días × ${salarioTopePrima:F2} (tope 2 SMA) × {anosServicio:F2} años = ${primaAntiguedad:F2}"
            });
        }
        else if (tipoSeparacion == "renuncia" && anosCompletos >= 15)
        {
            // Prima de antigüedad solo aplica en renuncia si tiene 15+ años
            var salarioTopePrima = Math.Min(sdi, SALARIO_MINIMO_2025 * 2);
            var primaAntiguedad  = Math.Round(salarioTopePrima * 12 * anosServicio, 2);
            var exentoPrima      = Math.Round(Math.Min(primaAntiguedad, SALARIO_MINIMO_2025 * 12 * anosServicio), 2);
            conceptos.Add(new ConceptoFiniquito
            {
                Tipo        = "finiquito",
                Concepto    = "Prima de antigüedad (15+ años, LFT Art. 162)",
                Dias        = (decimal)(12 * anosServicio),
                Monto       = primaAntiguedad,
                Gravado     = Math.Max(0, primaAntiguedad - exentoPrima),
                Exento      = exentoPrima,
                Explicacion = $"12 días × ${salarioTopePrima:F2} × {anosServicio:F2} años = ${primaAntiguedad:F2}"
            });
        }

        // Totales y ISR
        var totalBruto   = Math.Round(conceptos.Sum(c => c.Monto), 2);
        var totalGravado = Math.Round(conceptos.Sum(c => c.Gravado), 2);
        var totalExento  = Math.Round(conceptos.Sum(c => c.Exento), 2);

        // ISR sobre parte gravada (tasa progresiva simplificada)
        var isr = CalcularISRFiniquito(totalGravado);

        return Ok(new
        {
            empleado = new
            {
                empleado.Id,
                nombre        = $"{empleado.Nombre} {empleado.ApellidoPaterno} {empleado.ApellidoMaterno}".Trim(),
                empleado.RFC,
                empresa       = empleado.Empresa.RazonSocial,
                fechaIngreso  = fechaIngreso.ToString("dd/MM/yyyy"),
                fechaBaja     = fechaBaja.ToString("dd/MM/yyyy"),
                antiguedadDias,
                anosCompletos,
                mesesFraccion,
                antiguedadTexto = $"{anosCompletos} año(s) y {mesesFraccion} mes(es)",
                salarioDiario = empleado.SalarioDiario,
                sdi
            },
            tipoSeparacion,
            conceptos,
            resumen = new
            {
                totalBruto,
                totalExento,
                totalGravado,
                isr           = Math.Round(isr, 2),
                totalNeto     = Math.Round(totalBruto - isr, 2),
                explicacionISR = $"Base gravada ${totalGravado:F2} → ISR estimado ${isr:F2}"
            }
        });
    }

    private decimal CalcularISRFiniquito(decimal baseGravada)
    {
        if (baseGravada <= 0) return 0;
        // Tabla anual ISR 2025
        var tabla = new[]
        {
            (0.01m,         23512.03m,    0m,          1.92m),
            (23512.04m,     199241.16m,   451.26m,     6.40m),
            (199241.17m,    349993.14m,   11730.06m,   10.88m),
            (349993.15m,    407844.72m,   28112.94m,   16.00m),
            (407844.73m,    488641.08m,   37410.42m,   17.92m),
            (488641.09m,    984360.84m,   51876.66m,   21.36m),
            (984360.85m,    decimal.MaxValue, 157769.76m, 23.52m),
        };
        foreach (var (li, ls, cf, tasa) in tabla)
        {
            if (baseGravada >= li && baseGravada <= ls)
            {
                var excedente = Math.Round(baseGravada - li, 2);
                return Math.Round(excedente * (tasa / 100) + cf, 2);
            }
        }
        return 0;
    }

    private int CalcularDiasVacaciones(int anos) => anos switch
    {
        0 or 1 => 12,
        2      => 14,
        3      => 16,
        4      => 18,
        >= 5 and < 10  => 20,
        >= 10 and < 15 => 22,
        >= 15 and < 20 => 24,
        >= 20 and < 25 => 26,
        _              => 28
    };
}

public class ConceptoFiniquito
{
    public string Tipo { get; set; } = string.Empty;
    public string Concepto { get; set; } = string.Empty;
    public decimal Dias { get; set; }
    public decimal Monto { get; set; }
    public decimal Gravado { get; set; }
    public decimal Exento { get; set; }
    public string Explicacion { get; set; } = string.Empty;
}