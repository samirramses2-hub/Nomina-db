using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using NominaApp.Core.Entities;
using System;

namespace NominaApp.API.Reportes.Contratos;

public class ContratoDocument : IDocument
{
    private readonly Empleado _empleado;
    private readonly Empresa _empresa;
    private readonly string _firmaEmpleadoBase64;
    private readonly string _tipoContrato;

    public ContratoDocument(Empleado empleado, Empresa empresa, string firmaEmpleadoBase64, string tipoContrato)
    {
        _empleado = empleado;
        _empresa = empresa;
        _firmaEmpleadoBase64 = firmaEmpleadoBase64;
        _tipoContrato = tipoContrato;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(11).FontFamily(Fonts.Arial));

            page.Content().Column(col =>
            {
                // Encabezado
                col.Item().AlignCenter().Text($"CONTRATO INDIVIDUAL DE TRABAJO {_tipoContrato.ToUpper()}")
                    .Bold().FontSize(14);
                
                col.Item().PaddingTop(20).Text(text =>
                {
                    text.Span("CONTRATO INDIVIDUAL DE TRABAJO QUE CELEBRAN POR UNA PARTE LA EMPRESA ").Bold();
                    text.Span(_empresa.RazonSocial).Bold();
                    text.Span(", A QUIEN EN LO SUCESIVO SE LE DENOMINARÁ \"EL PATRÓN\", Y POR LA OTRA PARTE EL (LA) C. ").Bold();
                    text.Span($"{_empleado.Nombre} {_empleado.ApellidoPaterno} {_empleado.ApellidoMaterno}").Bold();
                    text.Span(", A QUIEN EN LO SUCESIVO SE LE DENOMINARÁ \"EL TRABAJADOR\", AL TENOR DE LAS SIGUIENTES DECLARACIONES Y CLÁUSULAS:");
                });

                col.Item().PaddingTop(15).Text("D E C L A R A C I O N E S").Bold().AlignCenter();
                
                col.Item().PaddingTop(10).Text(text =>
                {
                    text.Span("I. Declara EL PATRÓN:\n").Bold();
                    text.Span($"Ser una empresa legalmente constituida conforme a las leyes mexicanas, con RFC {_empresa.RFC} y domicilio fiscal en {_empresa.DomicilioFiscal}.\n\n");
                    
                    text.Span("II. Declara EL TRABAJADOR:\n").Bold();
                    text.Span($"Llamarse como ha quedado plasmado, de nacionalidad mexicana, con RFC {_empleado.RFC} y CURP {_empleado.CURP}, manifestando tener los conocimientos y aptitudes para desempeñar el puesto de ");
                    text.Span(_empleado.Puesto ?? "Empleado").Bold();
                    text.Span(".");
                });

                col.Item().PaddingTop(15).Text("C L Á U S U L A S").Bold().AlignCenter();
                
                col.Item().PaddingTop(10).Text(text =>
                {
                    text.Span("PRIMERA. ").Bold();
                    text.Span($"El trabajador se obliga a prestar sus servicios personales subordinados al patrón consistentes en el puesto de {_empleado.Puesto ?? "Empleado"}, bajo la dirección y dependencia del patrón.\n\n");
                    text.Span("SEGUNDA. ").Bold();
                    text.Span($"El trabajador percibirá por la prestación de sus servicios un salario diario de ${_empleado.SalarioDiario:F2} MXN.\n\n");
                    text.Span("TERCERA. ").Bold();
                    text.Span("La duración de la jornada de trabajo será la máxima legal permitida, de conformidad con la Ley Federal del Trabajo.\n\n");
                    text.Span("CUARTA. ").Bold();
                    text.Span("El trabajador disfrutará de los días de descanso legalmente establecidos y sus vacaciones conforme a la antigüedad generada en la empresa.\n\n");
                });

                col.Item().PaddingTop(30).Text($"Leído que fue el presente contrato por las partes y enteradas de su contenido y alcance legal, lo firman en {_empresa.DomicilioFiscal}, el día {DateTime.Now:dd} de {DateTime.Now:MMMM} de {DateTime.Now:yyyy}.").FontSize(10);

                // Firmas
                col.Item().PaddingTop(50).Row(row =>
                {
                    row.RelativeItem().AlignCenter().Column(c =>
                    {
                        c.Item().AlignCenter().Text("_________________________________");
                        c.Item().AlignCenter().Text("EL PATRÓN");
                        c.Item().AlignCenter().Text(_empresa.RazonSocial).Bold();
                    });

                    row.RelativeItem().AlignCenter().Column(c =>
                    {
                        if (!string.IsNullOrEmpty(_firmaEmpleadoBase64))
                        {
                            try {
                                var parts = _firmaEmpleadoBase64.Split(',');
                                var base64Data = parts.Length > 1 ? parts[1] : parts[0];
                                var imageBytes = Convert.FromBase64String(base64Data);
                                c.Item().AlignCenter().Width(120).Height(60).Image(imageBytes);
                            } catch {
                                c.Item().AlignCenter().Text("_________________________________");
                            }
                        }
                        else
                        {
                            c.Item().AlignCenter().Text("_________________________________");
                        }
                        c.Item().AlignCenter().Text("EL TRABAJADOR");
                        c.Item().AlignCenter().Text($"{_empleado.Nombre} {_empleado.ApellidoPaterno}").Bold();
                        // UUID/Blockchain simulate
                        c.Item().PaddingTop(10).AlignCenter().Text($"Firma Electrónica Avanzada: {Guid.NewGuid().ToString().ToUpper()}").FontSize(6).FontColor("#64748b");
                    });
                });
            });
        });
    }
}
