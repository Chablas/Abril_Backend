using Abril_Backend.Infrastructure.Interfaces;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using Abril_Backend.Infrastructure.Models;
using System.IO;
using Abril_Backend.Application.DTOs;
using ClosedXML.Excel;

namespace Abril_Backend.Infrastructure.InternalServices
{
    public class ExcelService
    {
        public ExcelService()
        {
            
        }

        public async Task<byte[]> GenerateLessonsExcel(List<LessonListDTO> lessons)
        {
            var templatePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "excel",
                "Lecciones_Aprendidas.xlsx"
            );

            using var workbook = new XLWorkbook(templatePath);
            var worksheet = workbook.Worksheet(1);

            int row = 8;

            foreach (var lesson in lessons)
            {
                worksheet.Cell(row, 2).Value = lesson.ProjectDescription;
                worksheet.Cell(row, 3).Value = lesson.Period;
                worksheet.Cell(row, 4).Value = lesson.PhaseDescription;
                worksheet.Cell(row, 5).Value = lesson.StageDescription;
                worksheet.Cell(row, 6).Value = lesson.LayerDescription;
                worksheet.Cell(row, 7).Value = lesson.SubStageDescription;
                worksheet.Cell(row, 8).Value = lesson.SubSpecialtyDescription;
                worksheet.Cell(row, 9).Value = lesson.ProblemDescription;
                worksheet.Cell(row, 10).Value = lesson.ReasonDescription;
                worksheet.Cell(row, 11).Value = lesson.LessonDescription;
                worksheet.Cell(row, 12).Value = lesson.ImpactDescription;

                var range = worksheet.Range(row, 2, row, 18);
                range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                range.Style.Alignment.WrapText = true;

                var oportunidadImages = lesson.Images
                    .Where(i => i.ImageTypeDescription == "OPORTUNIDAD")
                    .Take(3)
                    .ToList();

                var mejoraImages = lesson.Images
                    .Where(i => i.ImageTypeDescription == "MEJORA")
                    .Take(3)
                    .ToList();

                for (int i = 0; i < oportunidadImages.Count; i++)
                {
                    var img = oportunidadImages[i];

                    var imagePath = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        img.ImageUrl.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString())
                    );
                    Console.WriteLine(imagePath);
                    if (File.Exists(imagePath))
                    {
                        worksheet.AddPicture(imagePath)
                            .MoveTo(worksheet.Cell(row, 13 + i))
                            .WithSize(80, 80);
                    }
                }

                for (int i = 0; i < mejoraImages.Count; i++)
                {
                    var img = mejoraImages[i];

                    var imagePath = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        img.ImageUrl.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString())
                    );
                    Console.WriteLine(imagePath);
                    if (File.Exists(imagePath))
                    {
                        worksheet.AddPicture(imagePath)
                            .MoveTo(worksheet.Cell(row, 16 + i))
                            .WithSize(80, 80);
                    }
                }

                worksheet.Row(row).Height = 120;

                row++;
            }

            //worksheet.Rows(8, row-1).AdjustToContents(2, 12);
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
}
