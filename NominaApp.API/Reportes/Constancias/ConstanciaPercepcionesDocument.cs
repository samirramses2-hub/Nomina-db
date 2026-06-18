using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using NominaApp.Core.Entities;
using System;
using System.Linq;
using System.Collections.Generic;

namespace NominaApp.API.Reportes.Constancias;

public class ConstanciaPercepcionesDocument : IDocument
{
    private readonly Empleado _empleado;
    private readonly Empresa _empresa;
    private readonly int _ejercicio;
    private readonly decimal _totalPercepciones;
    private readonly decimal _totalRetenciones;

    public ConstanciaPercepcionesDocument(Empleado empleado, Empresa empresa, int ejercicio, decimal totalPercepciones, decimal totalRetenciones)
    {
        _empleado = empleado;
        _empresa = empresa;
        _ejercicio = ejercicio;
        _totalPercepciones = totalPercepciones;
        _totalRetenciones = totalRetenciones;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(2.5f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

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
                    row.RelativeItem().AlignRight().Text($"Constancia de Percepciones y Retenciones\nEjercicio: {_ejercicio}").Bold().FontSize(12);
                });

                col.Item().PaddingTop(20).Text("DATOS DEL RETENEDOR (EMPRESA)").Bold().FontSize(11).FontColor("#0f172a");
                col.Item().BorderBottom(1).PaddingBottom(5).Row(r =>
                {
                    r.RelativeItem().Text($"Razón Social: {_empresa.RazonSocial}\nRFC: {_empresa.RFC}");
                });

                col.Item().PaddingTop(15).Text("DATOS DEL TRABAJADOR").Bold().FontSize(11).FontColor("#0f172a");
                col.Item().BorderBottom(1).PaddingBottom(5).Row(r =>
                {
                    r.RelativeItem().Text($"Nombre: {_empleado.Nombre} {_empleado.ApellidoPaterno} {_empleado.ApellidoMaterno}\nRFC: {_empleado.RFC}\nCURP: {_empleado.CURP}");
                });

                col.Item().PaddingTop(20).Text("RESUMEN DE MONTOS DEL EJERCICIO").Bold().FontSize(11).FontColor("#0f172a");
                
                col.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background("#f1f5f9").Padding(5).Text("Concepto").Bold();
                        header.Cell().Background("#f1f5f9").Padding(5).Text("Monto").Bold().AlignRight();
                    });

                    table.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(5).Text("Total de Percepciones (Ingresos)");
                    table.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(5).AlignRight().Text($"${_totalPercepciones:F2}");

                    table.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(5).Text("Total de Impuesto Retenido (ISR)");
                    table.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(5).AlignRight().Text($"${_totalRetenciones:F2}");
                });

                col.Item().PaddingTop(40).Text("Este documento se expide para fines informativos del trabajador y no sustituye a la constancia oficial del SAT.").FontSize(9).FontColor("#64748b").AlignCenter();
            });
        });
    }
}
