using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Calculos;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;
using NominaApp.Infrastructure.Servicios;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CierreController : ControllerBase
{
    private readonly NominaDbContext _context;
    private const string FacturamaUsuario  = "Samir0501";
    private const string FacturamaPassword = "Srpv180501";

    public CierreController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("verificar/{periodoId}")]
    public async Task<ActionResult<object>> Verificar(int periodoId)
    {
        var periodo = await _context.PeriodosNomina
            .Include(p => p.Empresa)
            .FirstOrDefaultAsync(p => p.Id == periodoId);
        if (periodo is null) return NotFound("Periodo no encontrado.");

        var empleados = await _context.Empleados
            .Where(e => e.EmpresaId == periodo.EmpresaId && e.Activo)
            .ToListAsync();

        var incidencias = await _context.Incidencias
            .Where(i => i.PeriodoNominaId == periodoId)
            .ToListAsync();

        var alertas = new List<object>();
        var empleadosSinCLABE = empleados.Where(e => string.IsNullOrEmpty(e.CLABE)).ToList();
        var empleadosSinIncidencias = empleados
            .Where(e => !incidencias.Any(i => i.EmpleadoId == e.Id))
            .ToList();

        if (periodo.Estado == EstadoPeriodo.Cerrado)
            alertas.Add(new { tipo = "danger", mensaje = "El periodo ya está cerrado." });

        if (empleadosSinCLABE.Any())
            alertas.Add(new { tipo = "warning", mensaje = $"{empleadosSinCLABE.Count} empleado(s) sin CLABE: {string.Join(", ", empleadosSinCLABE.Select(e => e.Nombre + " " + e.ApellidoPaterno))}" });

        if (empleadosSinIncidencias.Any())
            alertas.Add(new { tipo = "info", mensaje = $"{empleadosSinIncidencias.Count} empleado(s) sin incidencias capturadas este periodo." });

        var diasPeriodo = (periodo.FechaFin - periodo.FechaInicio).Days + 1;
        decimal totalNeto = 0;
        decimal totalCosto = 0;

        foreach (var emp in empleados)
        {
            var incEmp = incidencias.Where(i => i.EmpleadoId == emp.Id).ToList();
            var parametros = new ParametrosCalculo
            {
                SalarioDiario        = emp.SalarioDiario,
                DiasPeriodo          = diasPeriodo,
                EjercicioFiscal      = periodo.EjercicioFiscal,
                FaltasInjustificadas = incEmp.Where(i => i.Tipo == TipoIncidencia.FaltaInjustificada).Sum(i => i.Cantidad),
                FaltasJustificadas   = incEmp.Where(i => i.Tipo == TipoIncidencia.FaltaJustificada).Sum(i => i.Cantidad),
                DiasVacaciones       = incEmp.Where(i => i.Tipo == TipoIncidencia.Vacaciones).Sum(i => i.Cantidad),
                HorasExtraSimples    = incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraSimple).Sum(i => i.Cantidad),
                HorasExtraDobles     = incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraDoble).Sum(i => i.Cantidad),
                HorasExtraTriples    = incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraTriple).Sum(i => i.Cantidad),
                Bonos                = incEmp.Where(i => i.Tipo == TipoIncidencia.Bono).Sum(i => i.Cantidad),
                DiasPrimaDominical   = incEmp.Where(i => i.Tipo == TipoIncidencia.PrimaDominical).Sum(i => i.Cantidad)
            };
            var calculo = MotorCalculo.Calcular(parametros);
            var imss    = MotorCalculo.CalcularCuotasIMSS(emp.SalarioDiario, diasPeriodo);
            totalNeto  += calculo.NetoPagar;
            totalCosto += calculo.TotalPercepciones + imss.TotalPatronal;
        }

        return Ok(new
        {
            periodoId,
            empresa          = periodo.Empresa.RazonSocial,
            periodo          = $"{periodo.FechaInicio:dd/MM/yyyy} — {periodo.FechaFin:dd/MM/yyyy}",
            fechaPago        = periodo.FechaPago.ToString("dd/MM/yyyy"),
            estado           = periodo.Estado.ToString(),
            totalEmpleados   = empleados.Count,
            totalNeto        = Math.Round(totalNeto, 2),
            totalCosto       = Math.Round(totalCosto, 2),
            listo            = !alertas.Any(a => (string)a.GetType().GetProperty("tipo")!.GetValue(a)! == "danger"),
            alertas
        });
    }

    [HttpPost("ejecutar/{periodoId}")]
    public async Task<ActionResult<object>> Ejecutar(int periodoId)
    {
        var periodo = await _context.PeriodosNomina
            .Include(p => p.Empresa)
            .FirstOrDefaultAsync(p => p.Id == periodoId);
        if (periodo is null) return NotFound("Periodo no encontrado.");
        if (periodo.Estado == EstadoPeriodo.Cerrado)
            return BadRequest("El periodo ya está cerrado.");

        var empleados = await _context.Empleados
            .Where(e => e.EmpresaId == periodo.EmpresaId && e.Activo)
            .ToListAsync();

        var incidencias = await _context.Incidencias
            .Where(i => i.PeriodoNominaId == periodoId)
            .ToListAsync();

        var diasPeriodo = (periodo.FechaFin - periodo.FechaInicio).Days + 1;
        var resultados  = new List<object>();
        int timbrados   = 0;
        int errores     = 0;

        // Construir el servicio de CFDI
        var facturama = new FacturamaService(FacturamaUsuario, FacturamaPassword);

        foreach (var emp in empleados)
        {
            var incEmp = incidencias.Where(i => i.EmpleadoId == emp.Id).ToList();
            var parametros = new ParametrosCalculo
            {
                SalarioDiario        = emp.SalarioDiario,
                DiasPeriodo          = diasPeriodo,
                EjercicioFiscal      = periodo.EjercicioFiscal,
                FaltasInjustificadas = incEmp.Where(i => i.Tipo == TipoIncidencia.FaltaInjustificada).Sum(i => i.Cantidad),
                FaltasJustificadas   = incEmp.Where(i => i.Tipo == TipoIncidencia.FaltaJustificada).Sum(i => i.Cantidad),
                DiasVacaciones       = incEmp.Where(i => i.Tipo == TipoIncidencia.Vacaciones).Sum(i => i.Cantidad),
                HorasExtraSimples    = incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraSimple).Sum(i => i.Cantidad),
                HorasExtraDobles     = incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraDoble).Sum(i => i.Cantidad),
                HorasExtraTriples    = incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraTriple).Sum(i => i.Cantidad),
                Bonos                = incEmp.Where(i => i.Tipo == TipoIncidencia.Bono).Sum(i => i.Cantidad),
                DiasPrimaDominical   = incEmp.Where(i => i.Tipo == TipoIncidencia.PrimaDominical).Sum(i => i.Cantidad)
            };

            var calculo = MotorCalculo.Calcular(parametros);
            var imss    = MotorCalculo.CalcularCuotasIMSS(emp.SalarioDiario, diasPeriodo);

            // Construir CFDI
            var percepciones = calculo.Percepciones.Select((p, idx) => new FacturamaPerceptionDetail
            {
                PerceptionType = p.Concepto == "Salario base" ? "001" : p.Concepto.Contains("extra") ? "019" : "010",
                Code           = $"00{idx + 1:D3}",
                Description    = p.Concepto,
                TaxedAmount    = Math.Round(p.Monto, 2),
                ExemptAmount   = 0
            }).ToList();

            var deducciones = calculo.Deducciones.Select((d, idx) => new FacturamaDeductionDetail
            {
                DeduccionType = d.Concepto == "ISR retenido" ? "002" : "001",
                Code          = $"00{idx + 1:D3}",
                Description   = d.Concepto,
                Amount        = Math.Round(d.Monto, 2)
            }).ToList();

            var subsidio = Math.Round(calculo.DetalleISR.SubsidioEmpleo > 0 ? calculo.DetalleISR.SubsidioEmpleo : 0.01m, 2);

            var cfdi = new FacturamaCfdiNomina
            {
                NameId          = "16",
                ExpeditionPlace = "22820",
                CfdiType        = "N",
                PaymentMethod   = "PPD",
                Currency        = "MXN",
                Folio           = $"{emp.Id}-{periodoId}-{DateTime.UtcNow:yyyyMMddHHmmss}",
                Receiver = new FacturamaReceiver
                {
                    Rfc          = emp.RFC,
                    Name         = $"{emp.Nombre} {emp.ApellidoPaterno} {emp.ApellidoMaterno}".Trim().ToUpper(),
                    CfdiUse      = "CN01",
                    FiscalRegime = "605",
                    TaxZipCode   = "36257"
                },
                Complemento = new FacturamaComplemento
                {
                    Payroll = new FacturamaPayroll
                    {
                        Type               = "O",
                        PaymentDate        = periodo.FechaPago.ToString("yyyy-MM-ddTHH:mm:ss"),
                        InitialPaymentDate = periodo.FechaInicio.ToString("yyyy-MM-ddTHH:mm:ss"),
                        FinalPaymentDate   = periodo.FechaFin.ToString("yyyy-MM-ddTHH:mm:ss"),
                        DaysPaid           = diasPeriodo,
                        Issuer = new FacturamaPayrollIssuer { EmployerRegistration = "A1234567890" },
                        Employee = new FacturamaPayrollEmployee
                        {
                            Curp                    = emp.CURP,
                            SocialSecurityNumber    = emp.NSS,
                            StartDateLaborRelations = emp.FechaIngreso.ToString("yyyy-MM-ddTHH:mm:ss"),
                            ContractType            = "01",
                            RegimeType              = "02",
                            Unionized               = false,
                            TypeOfJourney           = "01",
                            EmployeeNumber          = emp.Id.ToString(),
                            Department              = "General",
                            Position                = "Empleado",
                            PositionRisk            = "1",
                            FrequencyPayment        = "04",
                            Bank                    = "BANAMEX",
                            BankAccount             = "1234567890123456",
                            BaseSalary              = Math.Round(emp.SalarioDiario, 2),
                            DailySalary             = Math.Round(imss.SBC, 2),
                            FederalEntityKey        = "JAL",
                            
                        },
                        Perceptions   = new FacturamaPerceptions { Details = percepciones },
                        Deductions    = new FacturamaDeductions  { Details = deducciones },
                        OtherPayments = new List<FacturamaOtherPayment>
                        {
                            new FacturamaOtherPayment
                            {
                                OtherPaymentType  = "002",
                                Code              = "001",
                                Description       = "Subsidio al empleo",
                                Amount            = subsidio,
                                EmploymentSubsidy = new FacturamaEmploymentSubsidy { Amount = subsidio }
                            }
                        }
                    }
                }
            };

            try
            {
                var respuesta = await facturama.TimbrarNominaAsync(cfdi);
                if (respuesta.Exito)
                {
                    timbrados++;
                    resultados.Add(new
                    {
                        empleadoId = emp.Id,
                        nombre     = $"{emp.Nombre} {emp.ApellidoPaterno}",
                        neto       = calculo.NetoPagar,
                        uuid       = respuesta.UUID,
                        estado     = "timbrado"
                    });
                }
                else
                {
                    errores++;
                    resultados.Add(new
                    {
                        empleadoId = emp.Id,
                        nombre     = $"{emp.Nombre} {emp.ApellidoPaterno}",
                        neto       = calculo.NetoPagar,
                        uuid       = (string?)null,
                        estado     = "error",
                        error      = respuesta.Error
                    });
                }
            }
            catch (Exception ex)
            {
                errores++;
                resultados.Add(new
                {
                    empleadoId = emp.Id,
                    nombre     = $"{emp.Nombre} {emp.ApellidoPaterno}",
                    neto       = calculo.NetoPagar,
                    uuid       = (string?)null,
                    estado     = "error",
                    error      = ex.Message
                });
            }
        }

        // Cerrar el periodo si todos timbrados
        if (errores == 0)
        {
            periodo.Estado = EstadoPeriodo.Cerrado;
            await _context.SaveChangesAsync();
        }

        return Ok(new
        {
            periodoId,
            timbrados,
            errores,
            cerrado    = errores == 0,
            resultados
        });
    }
}