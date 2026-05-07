using SkiaSharp;

namespace iPDesktop.Services;

public class DocumentConverter
{
    private static readonly HashSet<string> ImageTypes =
        ["JPG", "JPEG", "PNG", "BMP", "WEBP", "ICO", "TIFF", "TIF", "MTIF", "GIF"];

    private static readonly HashSet<string> TextTypes =
        ["TXT", "MD", "CSV", "LOG", "JSON", "XML", "HTML", "HTM", "YAML", "YML",
         "CS", "JS", "TS", "PY", "CSS", "JAVA", "CPP", "C", "H", "SH", "BAT"];

    public bool CanConvert(string fileType)
    {
        var t = fileType.ToUpper().Trim('.');
        return t == "PDF" || ImageTypes.Contains(t) || TextTypes.Contains(t);
    }

    // Returns a PDF Stream for SfPdfViewer, or null if unsupported / failed
    public async Task<(Stream? stream, string? error)> ConvertToPdfAsync(string filePath, string fileType)
    {
        try
        {
            var type = fileType.ToUpper().Trim('.');

            if (type == "PDF")
            {
                var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return (fs, null);
            }

            if (ImageTypes.Contains(type))
            {
                var stream = await Task.Run(() => ImagesToPdf(filePath));
                return stream is null
                    ? (null, "Could not decode the image file.")
                    : (stream, null);
            }

            if (TextTypes.Contains(type))
            {
                var stream = await Task.Run(() => TextToPdf(filePath));
                return (stream, null);
            }

            return (null, $"'{fileType}' files cannot be previewed.");
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    // ── Images → PDF ─────────────────────────────────────────────────────────

    private Stream? ImagesToPdf(string path)
    {
        var frames = DecodeAllFrames(path);
        if (frames.Count == 0) return null;

        var ms  = new MemoryStream();
        using var doc = SKDocument.CreatePdf(ms);

        foreach (var bmp in frames)
        {
            // Landscape or portrait A4, image centred and scaled to fit
            bool landscape = bmp.Width > bmp.Height;
            float pW = landscape ? 842f : 595f;
            float pH = landscape ? 595f : 842f;

            float scale = Math.Min(pW / bmp.Width, pH / bmp.Height);
            float drawW = bmp.Width  * scale;
            float drawH = bmp.Height * scale;

            var canvas = doc.BeginPage(pW, pH);
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(bmp,
                SKRect.Create((pW - drawW) / 2f, (pH - drawH) / 2f, drawW, drawH));
            doc.EndPage();
            bmp.Dispose();
        }

        doc.Close();
        ms.Position = 0;
        return ms;
    }

    private List<SKBitmap> DecodeAllFrames(string path)
    {
        var results = new List<SKBitmap>();

        // Read file into memory first — avoids path encoding issues with native libs
        byte[] fileBytes;
        try { fileBytes = File.ReadAllBytes(path); }
        catch { return results; }

        using var codec = SKCodec.Create(new SKMemoryStream(fileBytes));

        // Single-frame (JPG, PNG, BMP, WEBP…) — FrameCount is 0 or 1
        if (codec is null || codec.FrameCount <= 1)
        {
            var bmp = SKBitmap.Decode(fileBytes);
            if (bmp is not null) results.Add(bmp);
            return results;
        }

        // Multi-frame (multi-page TIFF, animated GIF…)
        var info = codec.Info;
        for (int i = 0; i < codec.FrameCount; i++)
        {
            var bmp  = new SKBitmap(info);
            var opts = new SKCodecOptions(i);
            if (codec.GetPixels(info, bmp.GetPixels(), opts) == SKCodecResult.Success)
                results.Add(bmp);
            else
                bmp.Dispose();
        }

        return results;
    }

    // ── Text → PDF ───────────────────────────────────────────────────────────

    private Stream TextToPdf(string path)
    {
        string text;
        try
        {
            using var reader = new StreamReader(path);
            var buf  = new char[30_000];
            var read = reader.Read(buf, 0, buf.Length);
            text = new string(buf, 0, read);
            if (!reader.EndOfStream)
                text += "\n\n[Preview truncated — showing first 30,000 characters]";
        }
        catch (Exception ex)
        {
            text = $"[Could not read file: {ex.Message}]";
        }

        var lines = text.Replace("\r\n", "\n").Split('\n');

        const float pW          = 595f;
        const float pH          = 842f;
        const float padX        = 40f;
        const float padY        = 40f;
        const float fSz         = 11f;
        const float lineH       = 16f;
        int   linesPerPage      = (int)((pH - padY * 2) / lineH);

        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        using var font  = new SKFont(SKTypeface.Default, fSz);

        var ms  = new MemoryStream();
        using var doc = SKDocument.CreatePdf(ms);

        for (int start = 0; start < lines.Length; start += linesPerPage)
        {
            var canvas = doc.BeginPage(pW, pH);
            canvas.Clear(SKColors.White);

            float y   = padY + fSz;
            int   end = Math.Min(start + linesPerPage, lines.Length);
            for (int li = start; li < end; li++)
            {
                canvas.DrawText(lines[li] ?? "", padX, y, font, paint);
                y += lineH;
            }

            doc.EndPage();
        }

        doc.Close();
        ms.Position = 0;
        return ms;
    }
}
