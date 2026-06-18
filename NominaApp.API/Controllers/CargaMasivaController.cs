using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;
using System.Data;
using ExcelDataReader;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CargaMasivaController : ControllerBase
{
    private readonly NominaDbContext _context;

    public CargaMasivaController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("plantilla/{periodoId}")]
    public async Task<IActionResult> DescargarPlantilla(int periodoId)
    {
        var periodo = await _context.PeriodosNomina
            .Include(p => p.Empresa)
            .FirstOrDefaultAsync(p => p.Id == periodoId);
        if (periodo is null) return NotFound();

        var empleados = await _context.Empleados
            .Where(e => e.EmpresaId == periodo.EmpresaId && e.Activo)
            .OrderBy(e => e.CodigoEmpleado)
            .ToListAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("CodigoEmpleado,RFC,NombreEmpleado,FaltasJustificadas,FaltasInjustificadas,Vacaciones,HorasExtraSimples,HorasExtraDobles,Bonos,PrimaDominical,IncapacidadIMSS,LicenciaSinGoce,DescuentoInfonavit,Observaciones");

        foreach (var emp in empleados)
        {
            sb.AppendLine($"{emp.CodigoEmpleado},{emp.RFC},{emp.Nombre} {emp.ApellidoPaterno} {emp.ApellidoMaterno},0,0,0,0,0,0,0,0,0,0,");
        }

        var bytes    = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"plantilla_incidencias_{periodo.FechaInicio:yyyyMMdd}_{periodo.FechaFin:yyyyMMdd}.csv";
        return File(bytes, "text/csv", fileName);
    }

    [HttpPost("incidencias/{periodoId}")]
    public async Task<ActionResult<object>> CargarIncidencias(int periodoId, IFormFile archivo)
    {
        if (archivo is null || archivo.Length == 0)
            return BadRequest("No se recibió ningún archivo.");

        var periodo = await _context.PeriodosNomina.FindAsync(periodoId);
        if (periodo is null) return NotFound("Periodo no encontrado.");
        if (periodo.Estado == EstadoPeriodo.Cerrado)
            return BadRequest("No se pueden cargar incidencias en un periodo cerrado.");

        var empleados = await _context.Empleados
            .Where(e => e.EmpresaId == periodo.EmpresaId && e.Activo)
            .ToListAsync();

        var resultados  = new List<object>();
        int procesados  = 0;
        int errores     = 0;
        int omitidos    = 0;

        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        using var stream = archivo.OpenReadStream();

        var extension = Path.GetExtension(archivo.FileName).ToLower();
        if (extension == ".csv")
        {
            using var streamReader = new System.IO.StreamReader(stream);
            var contenido = await streamReader.ReadToEndAsync();
            var lineas    = contenido.Split('\n').Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

            foreach (var linea in lineas)
            {
                var cols = linea.Split(',');
                if (cols.Length < 13) continue;

                var resultado = await ProcesarFila(
                    cols[0].Trim(), // CodigoEmpleado
                    cols[1].Trim(), // RFC
                    cols[3].Trim(), // FaltasJustificadas
                    cols[4].Trim(), // FaltasInjustificadas
                    cols[5].Trim(), // Vacaciones
                    cols[6].Trim(), // HorasExtraSimples
                    cols[7].Trim(), // HorasExtraDobles
                    cols[8].Trim(), // Bonos
                    cols[9].Trim(), // PrimaDominical
                    cols[10].Trim(), // IncapacidadIMSS
                    cols[11].Trim(), // LicenciaSinGoce
                    cols[12].Trim(), // DescuentoInfonavit
                    cols.Length > 13 ? cols[13].Trim() : "",
                    periodoId, empleados
                );

                resultados.Add(resultado);
                if ((bool)resultado.GetType().GetProperty("procesado")!.GetValue(resultado)!)
                    procesados++;
                else if ((bool)resultado.GetType().GetProperty("omitido")!.GetValue(resultado)!)
                    omitidos++;
                else
                    errores++;
            }
        }
        else
        {
            using var excelReader = ExcelReaderFactory.CreateReader(stream);
            var dataset = excelReader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
            });

            var tabla = dataset.Tables[0];
            foreach (DataRow row in tabla.Rows)
            {
                var resultado = await ProcesarFila(
                    row[0]?.ToString()?.Trim() ?? "",
                    row[1]?.ToString()?.Trim() ?? "",
                    row[3]?.ToString()?.Trim() ?? "0",
                    row[4]?.ToString()?.Trim() ?? "0",
                    row[5]?.ToString()?.Trim() ?? "0",
                    row[6]?.ToString()?.Trim() ?? "0",
                    row[7]?.ToString()?.Trim() ?? "0",
                    row[8]?.ToString()?.Trim() ?? "0",
                    row[9]?.ToString()?.Trim() ?? "0",
                    row[10]?.ToString()?.Trim() ?? "0",
                    row[11]?.ToString()?.Trim() ?? "0",
                    row[12]?.ToString()?.Trim() ?? "0",
                    row.Table.Columns.Count > 13 ? row[13]?.ToString()?.Trim() ?? "" : "",
                    periodoId, empleados
                );

                resultados.Add(resultado);
                if ((bool)resultado.GetType().GetProperty("procesado")!.GetValue(resultado)!)
                    procesados++;
                else if ((bool)resultado.GetType().GetProperty("omitido")!.GetValue(resultado)!)
                    omitidos++;
                else
                    errores++;
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            procesados,
            errores,
            omitidos,
            resultados
        });
    }

    private async Task<object> ProcesarFila(
        string codigo, string rfc,
        string faltasJust, string faltasInjust, string vacaciones,
        string horasSimples, string horasDobles, string bonos,
        string primaDom, string incapacidad, string licencia,
        string infonavit, string observaciones,
        int periodoId, List<NominaApp.Core.Entities.Empleado> empleados)
    {
        var empleado = empleados.FirstOrDefault(e =>
            e.CodigoEmpleado == codigo || e.RFC == rfc);

        if (empleado is null)
            return new { codigo, rfc, procesado = false, omitido = false, error = "Empleado no encontrado" };

        var incidenciasAgregar = new List<(TipoIncidencia tipo, decimal cantidad)>
        {
            (TipoIncidencia.FaltaJustificada,   ParseDecimal(faltasJust)),
            (TipoIncidencia.FaltaInjustificada, ParseDecimal(faltasInjust)),
            (TipoIncidencia.Vacaciones,         ParseDecimal(vacaciones)),
            (TipoIncidencia.HoraExtraSimple,    ParseDecimal(horasSimples)),
            (TipoIncidencia.HoraExtraDoble,     ParseDecimal(horasDobles)),
            (TipoIncidencia.Bono,               ParseDecimal(bonos)),
            (TipoIncidencia.PrimaDominical,     ParseDecimal(primaDom)),
            (TipoIncidencia.IncapacidadIMSS,    ParseDecimal(incapacidad)),
            (TipoIncidencia.LicenciaSinGoce,    ParseDecimal(licencia)),
            (TipoIncidencia.DescuentoInfonavit, ParseDecimal(infonavit)),
        }.Where(x => x.cantidad > 0).ToList();

        if (!incidenciasAgregar.Any())
            return new { codigo, rfc, nombre = $"{empleado.Nombre} {empleado.ApellidoPaterno}", procesado = false, omitido = true, error = "Sin incidencias que registrar" };

        foreach (var (tipo, cantidad) in incidenciasAgregar)
        {
            _context.Incidencias.Add(new NominaApp.Core.Entities.Incidencia
            {
                EmpleadoId      = empleado.Id,
                PeriodoNominaId = periodoId,
                Tipo            = tipo,
                Cantidad        = cantidad,
                Observaciones   = observaciones,
                FechaRegistro   = DateTime.UtcNow
            });
        }

        return new
        {
            codigo,
            rfc,
            nombre      = $"{empleado.Nombre} {empleado.ApellidoPaterno}",
            procesado   = true,
            omitido     = false,
            incidencias = incidenciasAgregar.Count,
            error       = (string?)null
        };
    }

    private decimal ParseDecimal(string valor)
    {
        if (decimal.TryParse(valor, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var result))
            return result;
        return 0;
    }
}