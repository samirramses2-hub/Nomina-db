using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using NominaApp.Core.Entities;
using System;

namespace NominaApp.API.Reportes.Constancias;

public class CartaTrabajoDocument : IDocument
{
    private readonly Empleado _empleado;
    private readonly Empresa _empresa;

    public CartaTrabajoDocument(Empleado empleado, Empresa empresa)
    {
        _empleado = empleado;
        _empresa = empresa;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(2.5f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(11).FontFamily(Fonts.Arial));

            page.Content().Column(col =>
            {
                // Encabezado
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("CONTALIX").Bold().FontSize(16).FontColor("#0ea5e9");
                        c.Item().Text("NÓMINA").Bold().FontSize(16).FontColor("#0f172a");
                    });
                    row.RelativeItem().AlignRight().Text($"Lugar y fecha de expedición:\n{_empresa.DomicilioFiscal}, a {DateTime.Now:dd 'de' MMMM 'de' yyyy}").FontSize(10);
                });

                col.Item().PaddingTop(40).AlignCenter().Text("A QUIEN CORRESPONDA:").Bold().FontSize(12);

                col.Item().PaddingTop(30).Text(text =>
                {
                    text.Span("Por medio de la presente, hacemos constar que el (la) C. ");
                    text.Span($"{_empleado.Nombre} {_empleado.ApellidoPaterno} {_empleado.ApellidoMaterno}").Bold();
                    text.Span(", presta sus servicios en esta empresa ").Bold();
                    text.Span($"{_empresa.RazonSocial}").Bold();
                    text.Span(", desde el día ");
                    text.Span($"{_empleado.FechaIngreso:dd 'de' MMMM 'de' yyyy}").Bold();
                    text.Span(", desempeñando actualmente el puesto de ");
                    text.Span($"{_empleado.Puesto ?? "Empleado"}").Bold();
                    text.Span(", con un salario diario de ");
                    text.Span($"${_empleado.SalarioDiario:F2}").Bold();
                    text.Span(" MXN.\n\n");
                    
                    text.Span("Se extiende la presente a petición del interesado(a) y para los fines que a este convengan.");
                });

                col.Item().PaddingTop(80).AlignCenter().Column(c =>
                {
                    c.Item().AlignCenter().Text("_________________________________________");
                    c.Item().AlignCenter().Text("Departamento de Recursos Humanos");
                    c.Item().AlignCenter().Text(_empresa.RazonSocial).Bold();
                });
            });
        });
    }
}
