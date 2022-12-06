﻿using Microsoft.Extensions.Configuration;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using System.Reflection;

Console.WriteLine($"PdfMerge v." + Assembly.GetEntryAssembly()?.GetName().Version?.ToString());
Console.WriteLine($"Merges one or more jpg, png or pdf files into a single pdf file.");
Console.WriteLine();

var builder = new ConfigurationBuilder().AddCommandLine(args);
var configuration = builder.Build();

args = args.Where(x => !x.StartsWith("--")).ToArray(); // remove options

if (args.Length==0)
{
    Console.WriteLine($"Usage: PdfMerge.exe file1 [file2 file3 ... fileN] [--outFile=outFileName]");
    Console.WriteLine($"Example: PdfMerge.exe pdf_001.pdf image_001.png image_002.png --outFile=example.pdf");
    return 1;
}

foreach (var file in args)
{
    var info = new FileInfo(file);

    if (!info.Exists)
    {
        Console.WriteLine($"File '{file}' not found. Exiting.");
        return 1;
    }

    var extension = info.Extension.ToLower();

    if (extension!=".pdf" && extension != ".jpg" && extension != ".png")
    {
        Console.WriteLine($"Only pdf, jpg and png files accepted. Exiting");
        return 1;
    }
}

using (PdfDocument outPdf = new PdfDocument())
{
    foreach (var file in args)
    {
        Console.Write($"Adding file '{file}'... ");

        if (file.ToLower().EndsWith(".pdf"))
        {
            using (PdfDocument doc = PdfReader.Open(file, PdfDocumentOpenMode.Import))
            {
                CopyPages(doc, outPdf);
            }
        }
        else if (file.ToLower().EndsWith(".jpg") || file.ToLower().EndsWith(".png"))
        {
            PdfPage page = outPdf.AddPage();
            // page.Size = PdfSharpCore.PageSize.A5;
            XGraphics gfx = XGraphics.FromPdfPage(page);
            XImage image = XImage.FromFile(file);

            float imgX = image.PixelWidth;
            float imgY = image.PixelHeight;

            float pageX = (int)page.MediaBox.Size.Width;
            float pageY = (int)page.MediaBox.Size.Height;

            var imgRatio = imgX / imgY;
            var pageRatio = pageX / pageY;

            int startX, startY, finalX, finalY;

            if (imgRatio > pageRatio)
            {
                finalX = (int)pageX;
                finalY = (int)(imgY * pageX / imgX);

                startX = 0;
                startY = (int)(pageY - finalY) / 2;
            }
            else 
            {
                finalX = (int)(imgX * pageY / imgY);
                finalY = (int)pageY;

                startX = (int)(pageX - finalX) / 2; ;
                startY = 0;
            }

            gfx.DrawImage(image, startX, startY, finalX, finalY);
        }

        Console.WriteLine("Ok");
    }

    var outFile = configuration["outFile"] ?? DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".pdf";

    outPdf.Save(outFile);

    Console.WriteLine($"File '{outFile}' created.");
}

return 0;

void CopyPages(PdfDocument from, PdfDocument to)
{
    for (int i = 0; i < from.PageCount; i++)
    {
        to.AddPage(from.Pages[i]);
    }
}
