using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Calculos;
using NominaApp.Core.CFDI;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;
using NominaApp.Infrastructure.Servicios;
using System.Text;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CfdiController : ControllerBase
{
    private readonly NominaDbContext _context;
    private const string FacturamaUsuario  = "ESCUELA1";
    private const string FacturamaPassword = "Srpv180501";

    public CfdiController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("generar-xml/{empleadoId}/{periodoId}")]
    public async Task<IActionResult> GenerarXml(int empleadoId, int periodoId)
    {
        var empleado = await _context.Empleados
            .Include(e => e.Empresa)
            .FirstOrDefaultAsync(e => e.Id == empleadoId);
        if (empleado is null) return NotFound("Empleado no encontrado.");

        var periodo = await _context.PeriodosNomina.FindAsync(periodoId);
        if (periodo is null) return NotFound("Periodo no encontrado.");

        var incidencias = await _context.Incidencias
            .Where(i => i.EmpleadoId == empleadoId && i.PeriodoNominaId == periodoId)
            .ToListAsync();

        var diasPeriodo = (periodo.FechaFin - periodo.FechaInicio).Days + 1;

        var parametros = new ParametrosCalculo
        {
            SalarioDiario        = empleado.SalarioDiario,
            DiasPeriodo          = diasPeriodo,
            EjercicioFiscal      = periodo.EjercicioFiscal,
            FaltasInjustificadas = incidencias.Where(i => i.Tipo == TipoIncidencia.FaltaInjustificada).Sum(i => i.Cantidad),
            FaltasJustificadas   = incidencias.Where(i => i.Tipo == TipoIncidencia.FaltaJustificada).Sum(i => i.Cantidad),
            DiasVacaciones       = incidencias.Where(i => i.Tipo == TipoIncidencia.Vacaciones).Sum(i => i.Cantidad),
            HorasExtraSimples    = incidencias.Where(i => i.Tipo == TipoIncidencia.HoraExtraSimple).Sum(i => i.Cantidad),
            HorasExtraDobles     = incidencias.Where(i => i.Tipo == TipoIncidencia.HoraExtraDoble).Sum(i => i.Cantidad),
            HorasExtraTriples    = incidencias.Where(i => i.Tipo == TipoIncidencia.HoraExtraTriple).Sum(i => i.Cantidad),
            Bonos                = incidencias.Where(i => i.Tipo == TipoIncidencia.Bono).Sum(i => i.Cantidad),
            DiasPrimaDominical   = incidencias.Where(i => i.Tipo == TipoIncidencia.PrimaDominical).Sum(i => i.Cantidad)
        };

        var calculo = MotorCalculo.Calcular(parametros);
        var imss    = MotorCalculo.CalcularCuotasIMSS(empleado.SalarioDiario, diasPeriodo);

        var percepciones = new List<CfdiPercepcion>();
        foreach (var p in calculo.Percepciones)
        {
            percepciones.Add(new CfdiPercepcion
            {
                TipoPercepcion = p.Concepto == "Salario base" ? "001" :
                                 p.Concepto.Contains("extra")  ? "019" : "010",
                Clave          = percepciones.Count == 0 ? "001" : $"00{percepciones.Count + 1}",
                Concepto       = p.Concepto,
                ImporteGravado = p.Monto,
                ImporteExento  = 0
            });
        }

        var deducciones = new List<CfdiDeduccion>();
        foreach (var d in calculo.Deducciones)
        {
            deducciones.Add(new CfdiDeduccion
            {
                TipoDeduccion = d.Concepto == "ISR retenido" ? "002" : "001",
                Clave         = deducciones.Count == 0 ? "001" : $"00{deducciones.Count + 1}",
                Concepto      = d.Concepto,
                Importe       = d.Monto
            });
        }

        var request = new CfdiNominaRequest
        {
            RfcEmisor              = "EKU9003173C9",
            NombreEmisor           = "ESCUELA KEMPER URGATE",
            RegimenFiscalEmisor    = "601",
            RfcReceptor            = empleado.RFC,
            NombreReceptor         = $"{empleado.Nombre} {empleado.ApellidoPaterno} {empleado.ApellidoMaterno}".Trim(),
            CurpReceptor           = empleado.CURP,
            NumSeguridadSocial     = empleado.NSS,
            FechaInicioRelLaboral  = empleado.FechaIngreso,
            PeriodicidadPago       = "04",
            TipoContrato           = "01",
            TipoRegimen            = "02",
            NumEmpleado            = empleado.Id,
            FechaPago              = periodo.FechaFin,
            FechaInicialPago       = periodo.FechaInicio,
            FechaFinalPago         = periodo.FechaFin,
            NumDiasPagados         = diasPeriodo,
            SalarioDiarioIntegrado = imss.SBC,
            SalarioBaseCotApor     = imss.SBC,
            Percepciones           = percepciones,
            Deducciones            = deducciones
        };

        var xml = GeneradorXmlCfdi.GenerarXml(request);
        return Content(xml, "application/xml", Encoding.UTF8);
    }

    [HttpPost("timbrar/{empleadoId}/{periodoId}")]
    public async Task<IActionResult> Timbrar(int empleadoId, int periodoId, [FromBody] ResultadoCalculo? overrideCalculo)
    {
        var empleado = await _context.Empleados
            .Include(e => e.Empresa)
            .FirstOrDefaultAsync(e => e.Id == empleadoId);
        if (empleado is null) return NotFound("Empleado no encontrado.");

        var periodo = await _context.PeriodosNomina.FindAsync(periodoId);
        if (periodo is null) return NotFound("Periodo no encontrado.");

        var incidencias = await _context.Incidencias
            .Where(i => i.EmpleadoId == empleadoId && i.PeriodoNominaId == periodoId)
            .ToListAsync();

        var diasPeriodo = (periodo.FechaFin - periodo.FechaInicio).Days + 1;

        var parametros = new ParametrosCalculo
        {
            SalarioDiario        = empleado.SalarioDiario,
            DiasPeriodo          = diasPeriodo,
            EjercicioFiscal      = periodo.EjercicioFiscal,
            FaltasInjustificadas = incidencias.Where(i => i.Tipo == TipoIncidencia.FaltaInjustificada).Sum(i => i.Cantidad),
            FaltasJustificadas   = incidencias.Where(i => i.Tipo == TipoIncidencia.FaltaJustificada).Sum(i => i.Cantidad),
            DiasVacaciones       = incidencias.Where(i => i.Tipo == TipoIncidencia.Vacaciones).Sum(i => i.Cantidad),
            HorasExtraSimples    = incidencias.Where(i => i.Tipo == TipoIncidencia.HoraExtraSimple).Sum(i => i.Cantidad),
            HorasExtraDobles     = incidencias.Where(i => i.Tipo == TipoIncidencia.HoraExtraDoble).Sum(i => i.Cantidad),
            HorasExtraTriples    = incidencias.Where(i => i.Tipo == TipoIncidencia.HoraExtraTriple).Sum(i => i.Cantidad),
            Bonos                = incidencias.Where(i => i.Tipo == TipoIncidencia.Bono).Sum(i => i.Cantidad),
            DiasPrimaDominical   = incidencias.Where(i => i.Tipo == TipoIncidencia.PrimaDominical).Sum(i => i.Cantidad)
        };

        var calculo = overrideCalculo != null && overrideCalculo.Percepciones != null && overrideCalculo.Percepciones.Count > 0 
                      ? overrideCalculo 
                      : MotorCalculo.Calcular(parametros);
        var imss    = MotorCalculo.CalcularCuotasIMSS(empleado.SalarioDiario, diasPeriodo);

        // Percepciones
        var percepciones = new List<FacturamaPerceptionDetail>();
        int pIdx = 1;
        foreach (var p in calculo.Percepciones)
        {
            var tipo = p.Concepto == "Salario base" ? "001" :
                       p.Concepto.Contains("extra")  ? "019" : "010";

            var percepcion = new FacturamaPerceptionDetail
            {
                PerceptionType = tipo,
                Code           = $"00{pIdx:D3}",
                Description    = p.Concepto,
                TaxedAmount    = Math.Round(p.Monto, 2),
                ExemptAmount   = 0
            };

            if (tipo == "019")
            {
                percepcion.ExtraHours = new List<FacturamaExtraHours>
                {
                    new FacturamaExtraHours
                    {
                        Days      = "1",
                        HoursType = "01",
                        Amount    = Math.Round(p.Monto, 2).ToString("F2")
                    }
                };
            }

            percepciones.Add(percepcion);
            pIdx++;
        }

        // Deducciones
        var deducciones = new List<FacturamaDeductionDetail>();
        int dIdx = 1;
        foreach (var d in calculo.Deducciones)
        {
            deducciones.Add(new FacturamaDeductionDetail
            {
                DeduccionType = d.Concepto == "ISR retenido" ? "002" : "001",
                Code          = $"00{dIdx:D3}",
                Description   = d.Concepto,
                Amount        = Math.Round(d.Monto, 2)
            });
            dIdx++;
        }

        // Subsidio al empleo — obligatorio para régimen 02
        var subsidio = Math.Round(calculo.DetalleISR.SubsidioEmpleo > 0
            ? calculo.DetalleISR.SubsidioEmpleo
            : 0.01m, 2);

        var otrosPagos = new List<FacturamaOtherPayment>
        {
            new FacturamaOtherPayment
            {
                OtherPaymentType  = "002",
                Code              = "001",
                Description       = "Subsidio al empleo",
                Amount            = subsidio,
                EmploymentSubsidy = new FacturamaEmploymentSubsidy { Amount = subsidio }
            }
        };

        var cfdi = new FacturamaCfdiNomina
        {
            NameId          = "16",
            ExpeditionPlace = "42501",
            CfdiType        = "N",
            PaymentMethod   = "PPD",
            Currency        = "MXN",
            Folio           = $"{empleadoId}-{periodoId}",
            Receiver = new FacturamaReceiver
            {
                Rfc          = empleado.RFC,
                Name         = $"{empleado.Nombre} {empleado.ApellidoPaterno} {empleado.ApellidoMaterno}".Trim().ToUpper(),
                CfdiUse      = "CN01",
                FiscalRegime = "605",
                TaxZipCode   = "36257"
            },
            Complemento = new FacturamaComplemento
            {
                Payroll = new FacturamaPayroll
                {
                    Type               = "O",
                    PaymentDate        = periodo.FechaFin.ToString("yyyy-MM-ddTHH:mm:ss"),
                    InitialPaymentDate = periodo.FechaInicio.ToString("yyyy-MM-ddTHH:mm:ss"),
                    FinalPaymentDate   = periodo.FechaFin.ToString("yyyy-MM-ddTHH:mm:ss"),
                    DaysPaid           = diasPeriodo,
                    Issuer = new FacturamaPayrollIssuer
                    {
                        EmployerRegistration = "A1234567890",
                        
                    },
                    Employee = new FacturamaPayrollEmployee
                    {
                        Curp                    = empleado.CURP,
                        SocialSecurityNumber    = empleado.NSS,
                        StartDateLaborRelations = empleado.FechaIngreso.ToString("yyyy-MM-ddTHH:mm:ss"),
                        ContractType            = "01",
                        RegimeType              = "02",
                        Unionized               = false,
                        TypeOfJourney           = "01",
                        EmployeeNumber          = empleado.Id.ToString(),
                        Department              = "General",
                        Position                = "Empleado",
                        PositionRisk            = "1",
                        FrequencyPayment        = "04",
                        Bank                    = "BANAMEX",
                        BankAccount             = "1234567890123456",
                        BaseSalary              = Math.Round(empleado.SalarioDiario, 2),
                        DailySalary             = Math.Round(imss.SBC, 2),
                        FederalEntityKey        = "JAL"
                    },
                    Perceptions   = new FacturamaPerceptions { Details = percepciones },
                    Deductions    = new FacturamaDeductions  { Details = deducciones },
                    OtherPayments = otrosPagos
                }
            }
        };

        var facturama = new FacturamaService(FacturamaUsuario, FacturamaPassword);
        var respuesta = await facturama.TimbrarNominaAsync(cfdi);

        if (!respuesta.Exito)
            return BadRequest(new { error = respuesta.Error });

        // Guardar registro del CFDI
var registro = new CFDIRegistro
{
    EmpleadoId      = empleadoId,
    PeriodoNominaId = periodoId,
    UUID            = respuesta.UUID,
    RFCEmisor       = "EKU9003173C9",
    RFCReceptor     = empleado.RFC,
    Total           = calculo.NetoPagar,
    Estado          = EstadoCFDI.Vigente,
    FechaTimbrado   = DateTime.UtcNow
};
_context.CFDIs.Add(registro);
await _context.SaveChangesAsync();

return Ok(new
{
    uuid    = respuesta.UUID,
    mensaje = "CFDI de nómina timbrado correctamente"
});
    }
    [HttpGet("sucursales")]
public async Task<IActionResult> GetSucursales()
{
    var facturama = new FacturamaService(FacturamaUsuario, FacturamaPassword);
    var resultado = await facturama.GetSucursalesAsync();
    return Ok(resultado);
}
}