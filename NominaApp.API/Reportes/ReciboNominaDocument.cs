using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using NominaApp.Core.Calculos;
using NominaApp.Core.Entities;
using QRCoder;
using System;

namespace NominaApp.API.Reportes;

public class ReciboNominaDocument : IDocument
{
    private readonly Empleado _empleado;
    private readonly Empresa _empresa;
    private readonly PeriodoNomina _periodo;
    private readonly ResultadoCalculo _calculo;

    public ReciboNominaDocument(Empleado empleado, Empresa empresa, PeriodoNomina periodo, ResultadoCalculo calculo)
    {
        _empleado = empleado;
        _empresa  = empresa;
        _periodo  = periodo;
        _calculo  = calculo;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(9));

            page.Content().Column(col =>
            {
                // ── ENCABEZADO EMPRESA ──
                col.Item().BorderBottom(1).PaddingBottom(8).Row(row =>
                {
                    row.ConstantItem(80).Column(c => {
                        c.Item().Text("CONTALIX").Bold().FontSize(12).FontColor("#0ea5e9");
                        c.Item().Text("NÓMINA").Bold().FontSize(12).FontColor("#0f172a");
                    });

                    row.RelativeItem().PaddingLeft(12).Column(c =>
                    {
                        c.Item().Text(_empresa.RazonSocial).Bold().FontSize(13).FontColor("#0f172a");
                        c.Item().Text($"RFC: {_empresa.RFC}");
                        c.Item().Text($"Régimen fiscal: {_empresa.RegimenFiscal}");
                        c.Item().Text(_empresa.DomicilioFiscal);
                    });

                    row.ConstantItem(180).Column(c =>
                    {
                        string uuid = Guid.NewGuid().ToString().ToUpper();
                        c.Item().Text("RECIBO DE NÓMINA CFDI").Bold().FontSize(12).FontColor("#0ea5e9").AlignRight();
                        c.Item().Text($"UUID: {uuid}").FontSize(7).FontColor("#64748b").AlignRight();
                        c.Item().PaddingTop(4).Text($"Periodo: {_periodo.FechaInicio:dd/MM/yyyy} — {_periodo.FechaFin:dd/MM/yyyy}").AlignRight();
                        c.Item().Text($"Ejercicio: {_periodo.EjercicioFiscal}").AlignRight();
                    });
                });

                col.Item().PaddingTop(10);

                // ── DATOS EMPLEADO ──
                col.Item().BorderBottom(1).PaddingBottom(8).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("DATOS DEL EMPLEADO").Bold().FontSize(10);
                        c.Item().PaddingTop(4).Text($"Nombre: {_empleado.Nombre} {_empleado.ApellidoPaterno} {_empleado.ApellidoMaterno}");
                        c.Item().Text($"RFC: {_empleado.RFC}");
                        c.Item().Text($"CURP: {_empleado.CURP}");
                        c.Item().Text($"NSS: {_empleado.NSS}");
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(" ").Bold();
                        c.Item().PaddingTop(4).Text($"Salario diario: ${_empleado.SalarioDiario:F2}");
                        c.Item().Text($"Tipo contrato: {_empleado.TipoContrato}");
                        c.Item().Text($"Tipo periodo: {_empleado.TipoPeriodo}");
                        c.Item().Text($"Fecha ingreso: {_empleado.FechaIngreso:dd/MM/yyyy}");
                    });
                });

                col.Item().PaddingTop(10);

                // ── PERCEPCIONES Y DEDUCCIONES ──
                col.Item().Row(row =>
                {
                    // Percepciones
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Background("#1a56db").Padding(4)
                            .Text("PERCEPCIONES").FontColor("#ffffff").Bold().FontSize(9);

                        foreach (var p in _calculo.Percepciones)
                        {
                            c.Item().BorderBottom(1).BorderColor("#e5e7eb").PaddingVertical(3).Row(r =>
                            {
                                r.RelativeItem().Column(inner =>
                                {
                                    inner.Item().Text(p.Concepto);
                                    inner.Item().Text(p.Explicacion).FontSize(7).FontColor("#6b7280");
                                });
                                r.ConstantItem(70).Text($"${p.Monto:F2}").AlignRight();
                            });
                        }

                        c.Item().PaddingTop(4).Row(r =>
                        {
                            r.RelativeItem().Text("TOTAL PERCEPCIONES").Bold();
                            r.ConstantItem(70).Text($"${_calculo.TotalPercepciones:F2}").Bold().AlignRight();
                        });
                    });

                    row.ConstantItem(16);

                    // Deducciones
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Background("#dc2626").Padding(4)
                            .Text("DEDUCCIONES").FontColor("#ffffff").Bold().FontSize(9);

                        foreach (var d in _calculo.Deducciones)
                        {
                            c.Item().BorderBottom(1).BorderColor("#e5e7eb").PaddingVertical(3).Row(r =>
                            {
                                r.RelativeItem().Column(inner =>
                                {
                                    inner.Item().Text(d.Concepto);
                                    inner.Item().Text(d.Explicacion).FontSize(7).FontColor("#6b7280");
                                });
                                r.ConstantItem(70).Text($"${d.Monto:F2}").AlignRight();
                            });
                        }

                        c.Item().PaddingTop(4).Row(r =>
                        {
                            r.RelativeItem().Text("TOTAL DEDUCCIONES").Bold();
                            r.ConstantItem(70).Text($"${_calculo.TotalDeducciones:F2}").Bold().AlignRight();
                        });
                    });
                });

                col.Item().PaddingTop(12);

                // ── DETALLE ISR ──
                col.Item().Background("#f3f4f6").Padding(8).Column(c =>
                {
                    c.Item().Text("DETALLE DEL CÁLCULO ISR").Bold().FontSize(9);
                    c.Item().PaddingTop(4).Text(_calculo.DetalleISR.Explicacion).FontSize(8).FontColor("#374151");
                });

                col.Item().PaddingTop(12);

                // ── NETO A PAGAR ──
                col.Item().Background("#065f46").Padding(10).Row(row =>
                {
                    row.RelativeItem().Text("NETO A PAGAR").FontColor("#ffffff").Bold().FontSize(14);
                    row.ConstantItem(120).Text($"${_calculo.NetoPagar:F2}").FontColor("#ffffff").Bold().FontSize(14).AlignRight();
                });

                col.Item().PaddingTop(20);

                // ── FIRMAS Y CÓDIGO QR ──
                col.Item().Row(row =>
                {
                    string uuid = Guid.NewGuid().ToString().ToUpper();
                    string qrUrl = $"https://verificacfdi.facturaelectronica.sat.gob.mx/default.aspx?id={uuid}&re={_empresa.RFC}&rr={_empleado.RFC}&tt={_calculo.NetoPagar:F2}";
                    
                    using var qrGenerator = new QRCodeGenerator();
                    using var qrCodeData = qrGenerator.CreateQrCode(qrUrl, QRCodeGenerator.ECCLevel.M);
                    using var qrCode = new PngByteQRCode(qrCodeData);
                    byte[] qrCodeImage = qrCode.GetGraphic(5);

                    row.ConstantItem(80).Height(80).Image(qrCodeImage);

                    row.RelativeItem().PaddingLeft(20).Column(c =>
                    {
                        c.Item().Text("Sello Digital del CFDI").FontSize(7).Bold();
                        c.Item().Text("A1b2C3d4E5f6G7h8I9j0K1l2M3n4O5p6Q7r8S9t0U1v2W3x4Y5z6A1b2C3d4E5f6G7h8I9j0K1l2M3n4O5p6Q7r8S9t0...").FontSize(6).FontColor("#64748b");
                        c.Item().PaddingTop(4).Text("Sello del SAT").FontSize(7).Bold();
                        c.Item().Text("Z9y8X7w6V5u4T3s2R1q0P9o8N7m6L5k4J3i2H1g0F9e8D7c6B5a4Z9y8X7w6V5u4T3s2R1q0P9o8N7m6L5k4J3i2...").FontSize(6).FontColor("#64748b");
                        c.Item().PaddingTop(4).Text("Cadena Original del Complemento de Certificación Digital del SAT").FontSize(7).Bold();
                        c.Item().Text("||1.1|UUID|2026-05-30T10:00:00|A1b2C3d4E5f6G7h8I9j0K1l2M3n4O5p6Q7r8S9t0U1v2W3x4Y5z6||").FontSize(6).FontColor("#64748b");
                    });

                    row.ConstantItem(40);

                    // Firmas
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Row(r =>
                        {
                            r.RelativeItem().Column(colFirma =>
                            {
                                colFirma.Item().Height(40);
                                colFirma.Item().BorderTop(1).PaddingTop(4).Text("Firma del trabajador").AlignCenter().FontSize(8);
                            });
                            r.ConstantItem(20);
                            r.RelativeItem().Column(colFirma =>
                            {
                                colFirma.Item().Height(40);
                                colFirma.Item().BorderTop(1).PaddingTop(4).Text("Firma del patrón").AlignCenter().FontSize(8);
                            });
                        });
                    });
                });
            });
        });
    }
}