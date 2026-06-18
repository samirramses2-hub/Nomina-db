using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Infrastructure.Data;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BackupsController : ControllerBase
{
    private readonly NominaDbContext _context;

    public BackupsController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("descargar")]
    public async Task<IActionResult> DescargarBackup()
    {
        var backupPath = Path.Combine(Path.GetTempPath(), $"NominaApp_{DateTime.Now:yyyyMMdd_HHmmss}.bak");
        
        try
        {
#pragma warning disable EF1002
            await _context.Database.ExecuteSqlRawAsync($"BACKUP DATABASE [NominaApp] TO DISK = '{backupPath}' WITH FORMAT, INIT, MEDIANAME = 'SQLServerBackups', NAME = 'Full Backup of NominaApp'");
#pragma warning restore EF1002

            if (!System.IO.File.Exists(backupPath))
                return StatusCode(500, "No se pudo generar el archivo de respaldo.");

            var stream = new FileStream(backupPath, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.DeleteOnClose);
            return File(stream, "application/octet-stream", Path.GetFileName(backupPath));
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error al realizar el respaldo: {ex.Message}");
        }
    }
}
