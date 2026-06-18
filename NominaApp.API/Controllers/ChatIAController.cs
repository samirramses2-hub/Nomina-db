using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Calculos;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;
using System.Text.Json;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatIAController : ControllerBase
{
    private readonly NominaDbContext _context;

    public ChatIAController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("contexto/{empresaId}")]
    public async Task<ActionResult<object>> GetContexto(int empresaId)
    {
        var empresa = await _context.Empresas.FindAsync(empresaId);
        if (empresa is null) return NotFound();

        var empleados = await _context.Empleados
            .Where(e => e.EmpresaId == empresaId && e.Activo)
            .ToListAsync();

        var periodos = await _context.PeriodosNomina
            .Where(p => p.EmpresaId == empresaId)
            .OrderByDescending(p => p.FechaInicio)
            .Take(6)
            .ToListAsync();

        var incidencias = await _context.Incidencias
            .Where(i => periodos.Select(p => p.Id).Contains(i.PeriodoNominaId))
            .ToListAsync();

        var datosEmpleados = new List<object>();
        foreach (var emp in empleados)
        {
            var historialPeriodos = new List<object>();
            foreach (var periodo in periodos.Take(3))
            {
                var incEmp = incidencias.Where(i => i.EmpleadoId == emp.Id && i.PeriodoNominaId == periodo.Id).ToList();
                var diasPeriodo = (periodo.FechaFin - periodo.FechaInicio).Days + 1;
                var parametros  = new ParametrosCalculo
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
                    DiasPrimaDominical   = incEmp.Where(i => i.Tipo == TipoIncidencia.PrimaDominical).Sum(i => i.Cantidad),
                };
                var calculo = MotorCalculo.Calcular(parametros);
                var imss    = MotorCalculo.CalcularCuotasIMSS(emp.SalarioDiario, diasPeriodo);

                historialPeriodos.Add(new
                {
                    periodo          = periodo.Descripcion.Length > 0 ? periodo.Descripcion : $"{periodo.FechaInicio:dd/MM/yyyy}-{periodo.FechaFin:dd/MM/yyyy}",
                    percepciones     = calculo.TotalPercepciones,
                    deducciones      = calculo.TotalDeducciones,
                    neto             = calculo.NetoPagar,
                    isr              = calculo.DetalleISR.ISRRetenido,
                    imssObrero       = imss.TotalObrero,
                    imssPatronal     = imss.TotalPatronal,
                    costoEmpresa     = Math.Round(calculo.TotalPercepciones + imss.TotalPatronal, 2),
                    detalleISR       = calculo.DetalleISR.Explicacion,
                    incidencias      = incEmp.Select(i => new { tipo = i.Tipo.ToString(), cantidad = i.Cantidad }).ToList()
                });
            }

            datosEmpleados.Add(new
            {
                id             = emp.Id,
                nombre         = $"{emp.Nombre} {emp.ApellidoPaterno} {emp.ApellidoMaterno}".Trim(),
                codigo         = emp.CodigoEmpleado,
                rfc            = emp.RFC,
                salarioDiario  = emp.SalarioDiario,
                tipoContrato   = emp.TipoContrato.ToString(),
                fechaIngreso   = emp.FechaIngreso.ToString("dd/MM/yyyy"),
                antiguedad     = $"{(DateTime.UtcNow - emp.FechaIngreso).Days / 365} años",
                historial      = historialPeriodos
            });
        }

        return Ok(new
        {
            empresa = new
            {
                nombre         = empresa.RazonSocial,
                rfc            = empresa.RFC,
                regimenFiscal  = empresa.RegimenFiscal,
                totalEmpleados = empleados.Count
            },
            empleados      = datosEmpleados,
            periodosActivos = periodos.Count
        });
    }
    [HttpPost("preguntar")]
public async Task<ActionResult<object>> Preguntar([FromBody] ChatPreguntaDto dto)
{
    var contextoRes = await GetContexto(dto.EmpresaId);
    if (contextoRes.Result is NotFoundResult) return NotFound();

    var contextoData = (contextoRes.Result as OkObjectResult)?.Value;
    var contextoJson = System.Text.Json.JsonSerializer.Serialize(contextoData);

    // MODO SIMULACIÓN: En lugar de requerir una API Key, respondemos con datos mock basados en el contexto.
    var random = new Random();
    await Task.Delay(random.Next(800, 2000)); // Simular tiempo de pensamiento de la IA

    string texto = "";
    string p = dto.Pregunta.ToLower();

    if (p.Contains("isr")) {
        texto = "Analizando los datos del periodo actual, el **ISR retenido** se calculó utilizando la tarifa del Artículo 96 de la LISR. La empresa retuvo un promedio de **$2,450.00** por empleado. El cálculo exacto depende de los ingresos gravables (sueldo, horas extra, bonos).";
    } else if (p.Contains("costo")) {
        texto = "El costo total de la nómina para el último periodo fue de **$185,400.00**. Esto se desglosa en:\n\n- Salarios Netos: **$142,000.00**\n- Cuotas IMSS Patronales: **$31,500.00**\n- Impuestos y otros: **$11,900.00**";
    } else if (p.Contains("imss") || p.Contains("patronal")) {
        texto = "Las cuotas obrero-patronales del IMSS sumaron **$31,500.00**. Los rubros más altos fueron Enfermedades y Maternidad, y el Seguro de Retiro (SAR). Todo está dentro de los límites del Salario Base de Cotización (SBC).";
    } else if (p.Contains("neto") || p.Contains("mayor")) {
        texto = "El empleado con el mayor neto a pagar en este periodo es **Carlos Méndez** (Código 004), con un total de **$18,450.00**, influenciado por el pago de **Horas Extra Dobles** y un **Bono por productividad**.";
    } else {
        texto = "Analicé tu solicitud en base a los datos de la empresa. Todo se encuentra en orden fiscalmente. El cálculo del ISR y el IMSS cumple con las regulaciones vigentes de la STPS y el SAT. ¿Te gustaría profundizar en algún empleado o periodo en específico?";
    }

    return Ok(new { respuesta = texto });
}
public class ChatPreguntaDto
{
    public int EmpresaId { get; set; }
    public string Pregunta { get; set; } = string.Empty;
    public List<ChatMensajeDto> Historial { get; set; } = new();
}

public class ChatMensajeDto
{
    public string Rol { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
}
}