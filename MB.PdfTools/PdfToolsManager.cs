﻿using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using System.Text;
using ImageMagick;

namespace MB.PdfTools
{
    public interface ICommand<T>
    {
        public CommandResult Execute(T commandParameters);
    }

    public class CommandResult
    {
        public string Output { get; set; }
        public string ErrorMessage { get; private set; }
        public bool IsOk { get { return ErrorMessage == null; } }

        public static CommandResult Error(string errorMessage, string output)
        {
            return new CommandResult { ErrorMessage = errorMessage, Output = output };
        }

        public static CommandResult Ok(string output)
        {
            return new CommandResult { Output = output };
        }

        private CommandResult() { }
    }

    public class MergeCommandParameters
    {
        public string[] Files { get; private set; }
        public string OutFile { get; private set; }
        public string Orientation { get; private set; }

        public MergeCommandParameters(IEnumerable<string> files, string outFiile, string orientation)
        {
            Files = files.ToArray();
            OutFile = outFiile;
            Orientation = orientation;
        }
    }

    public class MergeCommand : ICommand<MergeCommandParameters>
    {
        public CommandResult Execute(MergeCommandParameters commandParameters)
        {
            var sbOut = new StringBuilder();

            using (PdfDocument outPdf = new PdfDocument())
            {
                foreach (var file in commandParameters.Files)
                {
                    sbOut.Append($"Adding file '{file}'... ");

                    if (file.ToLower().EndsWith(".pdf"))
                    {
                        using (PdfDocument doc = PdfReader.Open(file, PdfDocumentOpenMode.Import))
                        {
                            var nPages = CopyPages(doc, outPdf);
                            sbOut.AppendLine($"Ok ({nPages} pages)");
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

                        page.Orientation = PdfSharpCore.PageOrientation.Portrait;
                        
                        if (commandParameters.Orientation == "l")
                            page.Orientation = PdfSharpCore.PageOrientation.Landscape;

                        if (commandParameters.Orientation == "a" && imgX > imgY) // auto orientation
                            page.Orientation = PdfSharpCore.PageOrientation.Landscape;

                        float pageX = (int)page.MediaBox.Size.Width;
                        float pageY = (int)page.MediaBox.Size.Height;

                        if (page.Orientation == PdfSharpCore.PageOrientation.Landscape)
                        {
                            var tmp = pageX;
                            pageX = pageY;
                            pageY = tmp;
                        }

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

                        sbOut.AppendLine("Ok");
                    }
                }

                outPdf.Save(commandParameters.OutFile);

                sbOut.AppendLine();
                sbOut.AppendLine($"File '{commandParameters.OutFile}' created.");

                return CommandResult.Ok(sbOut.ToString());
            }
        }

        private int CopyPages(PdfDocument from, PdfDocument to)
        {
            for (int i = 0; i < from.PageCount; i++)
            {
                to.AddPage(from.Pages[i]);
            }

            return from.PageCount;
        }
    }

    public class SplitCommandParameters
    {
        public string[] Files { get; private set; }
        public string OutFile { get; private set; }

        public SplitCommandParameters(IEnumerable<string> files, string outFiile)
        {
            Files = files.ToArray();
            OutFile = outFiile;
        }
    }

    public class SplitCommand : ICommand<SplitCommandParameters>
    {
        public CommandResult Execute(SplitCommandParameters commandParameters)
        {
            var sbOut = new StringBuilder();
            var totalPages = 1;

            //MagickNET.SetGhostscriptDirectory(@"c:\ghostScript");

            var settings = new MagickReadSettings();
            settings.Density = new Density(300, 300);

            foreach (var file in commandParameters.Files)
            {
                using (var images = new MagickImageCollection())
                {
                    sbOut.Append($"Splitting file '{file}'... ");

                    try
                    {
                        images.Read(file, settings);
                    }
                    catch (ImageMagick.MagickDelegateErrorException ex)
                    {
                        sbOut.AppendLine($"\nERROR: cannot create images; please check that Ghostscript is installed on your machine (see command help for details)");
                        return CommandResult.Error(ex.Message, sbOut.ToString());
                    }

                    foreach (var image in images)
                    {
                        image.Format = MagickFormat.Jpg;
                        image.Write($"{commandParameters.OutFile}_{totalPages++:0000}.jpg");
                    }

                    sbOut.AppendLine($"Ok ({images.Count} pages)");
                }
            }

            sbOut.AppendLine();
            sbOut.AppendLine($"File(s) created.");

            return CommandResult.Ok(sbOut.ToString());
        }
    }
}