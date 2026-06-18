using Microsoft.AspNetCore.Mvc;
using UglyToad.PdfPig;
using System.Text.RegularExpressions;
using System.Text;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OnboardingController : ControllerBase
{
    [HttpPost("procesar-csf")]
    public IActionResult ProcesarCSF(IFormFile archivo)
    {
        if (archivo == null || archivo.Length == 0)
            return BadRequest("No se proporcionó ningún archivo.");

        if (archivo.ContentType != "application/pdf")
            return BadRequest("El documento debe ser un archivo PDF.");

        try
        {
            using var ms = new MemoryStream();
            archivo.CopyTo(ms);
            var fileBytes = ms.ToArray();

            var extractedText = new StringBuilder();

            using (PdfDocument document = PdfDocument.Open(fileBytes))
            {
                foreach (var page in document.GetPages())
                {
                    extractedText.Append(page.Text);
                }
            }

            string text = extractedText.ToString();

            // Lógica de Regex para extracción
            string rfc = ExtractRegex(text, @"([A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3})");
            
            // Buscar Código Postal (5 dígitos cerca de "Código Postal:" o solos, usualmente el primero que aparece después de cierta info, o simplemente buscar 5 números)
            // Es un poco frágil, buscaremos explícitamente algo como C.P. o 5 dígitos consecutivos
            string cp = ExtractRegex(text, @"\b(\d{5})\b");

            // Buscar Nombre (Usualmente está después del RFC o cerca) - Muy simplificado
            string nombre = ExtractRegex(text, @"Nombre\s*,\s*denominación\s*o\s*razón\s*social:\s*(.*?)(idC|CURP|RFC|$)") ?? "";
            if (string.IsNullOrWhiteSpace(nombre)) 
            {
                // Alternativa
                nombre = ExtractRegex(text, @"Nombre \(s\)\s*(.*?)\s*Primer Apellido");
            }

            // Limpieza básica
            nombre = nombre.Trim();

            return Ok(new
            {
                rfc = rfc,
                codigoPostal = cp,
                nombreBruto = nombre,
                rawTextPreview = text.Length > 200 ? text.Substring(0, 200) + "..." : text
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error procesando el PDF: {ex.Message}");
        }
    }

    private string ExtractRegex(string input, string pattern)
    {
        var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);
        if (match.Success && match.Groups.Count > 1)
        {
            return match.Groups[1].Value.Trim();
        }
        else if (match.Success)
        {
             return match.Value.Trim();
        }
        return null;
    }
}
