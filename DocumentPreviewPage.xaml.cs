using iPDesktop.Data;
using iPDesktop.Models;
using iPDesktop.Services;
using SkiaSharp;

#if WINDOWS
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
#endif

namespace iPDesktop;

[QueryProperty(nameof(DocId), "docId")]
public partial class DocumentPreviewPage : ContentPage, IDrawable
{
    private readonly DatabaseService   _db;
    private readonly DocumentConverter _converter;
    private Document? _document;
    private Stream?   _pdfStream;

    // OCR crop state
    private bool   _ocrMode    = false;
    private bool   _isDragging = false;
    private bool   _ocrRunning = false;
    private PointF _cropStart;
    private PointF _cropEnd;

    public int DocId { get; set; }

    public DocumentPreviewPage(DatabaseService db, DocumentConverter converter)
    {
        InitializeComponent();
        _db             = db;
        _converter      = converter;
        CropCanvas.Drawable = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadPreviewAsync();
    }

    private async Task LoadPreviewAsync()
    {
        ShowOnly(LoadingView);

        _document = await _db.GetDocumentByIdAsync(DocId);

        if (_document is null)
        {
            ShowError("Document record not found.");
            return;
        }

        FileNameLabel.Text = _document.FileName;
        FileMetaLabel.Text = $"{_document.FileType}  ·  {_document.SizeDisplay}  ·  {_document.UploadedAt:dd MMM yyyy}";

        if (!File.Exists(_document.StoragePath))
        {
            ShowError("File not found on disk.\nIt may have been moved or deleted.");
            return;
        }

        if (!_converter.CanConvert(_document.FileType))
        {
            UnsupportedLabel.Text = _document.FileName;
            ShowOnly(UnsupportedView);
            return;
        }

        var (pdfStream, convertError) = await _converter.ConvertToPdfAsync(_document.StoragePath, _document.FileType);

        if (convertError is not null)
        {
            ShowError(convertError);
            return;
        }

        _pdfStream = pdfStream;

        if (_pdfStream is null || _pdfStream == Stream.Null)
        {
            UnsupportedLabel.Text = _document.FileName;
            ShowOnly(UnsupportedView);
            return;
        }

        PdfViewer.DocumentSource = _pdfStream;
        OcrBtn.IsVisible         = true;
        ShowOnly(PdfViewer);
    }

    private void ShowOnly(View target)
    {
        foreach (var v in new View[] { LoadingView, PdfViewer, UnsupportedView, ErrorView })
            v.IsVisible = v == target;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ShowOnly(ErrorView);
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        SetOcrMode(false);
        PdfViewer.DocumentSource = null;
        _pdfStream?.Dispose();
        _pdfStream = null;
        await Shell.Current.GoToAsync("..");
    }

    // ── OCR Mode Toggle ───────────────────────────────────────────────────────

    private void OnOcrModeToggled(object? sender, EventArgs e) => SetOcrMode(!_ocrMode);

    private void SetOcrMode(bool active)
    {
        _ocrMode             = active;
        CropCanvas.IsVisible = active;
        OcrBanner.IsVisible  = active;
        OcrBtn.Text          = active ? "Cancel OCR" : "Crop OCR";

        if (!active)
        {
            _isDragging = false;
            _cropStart  = default;
            _cropEnd    = default;
            CropCanvas.Invalidate();
        }
    }

    // ── IDrawable — draws the crop selection rectangle ────────────────────────

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var rect = GetCropRect();
        if (rect.Width < 2 || rect.Height < 2) return;

        // Semi-transparent dark overlay
        canvas.FillColor = Color.FromArgb("#80000000");
        canvas.FillRectangle(dirtyRect);

        // Selection border
        canvas.StrokeColor = Colors.White;
        canvas.StrokeSize  = 2;
        canvas.DrawRectangle(rect);

        // Corner handles
        const float h = 14f;
        canvas.StrokeSize = 3;
        canvas.DrawLine(rect.X,         rect.Y + h,      rect.X,     rect.Y);
        canvas.DrawLine(rect.X,         rect.Y,          rect.X + h, rect.Y);
        canvas.DrawLine(rect.Right - h, rect.Y,          rect.Right, rect.Y);
        canvas.DrawLine(rect.Right,     rect.Y,          rect.Right, rect.Y + h);
        canvas.DrawLine(rect.X,         rect.Bottom - h, rect.X,     rect.Bottom);
        canvas.DrawLine(rect.X,         rect.Bottom,     rect.X + h, rect.Bottom);
        canvas.DrawLine(rect.Right - h, rect.Bottom,     rect.Right, rect.Bottom);
        canvas.DrawLine(rect.Right,     rect.Bottom,     rect.Right, rect.Bottom - h);
    }

    private RectF GetCropRect() => new(
        Math.Min(_cropStart.X, _cropEnd.X),
        Math.Min(_cropStart.Y, _cropEnd.Y),
        Math.Abs(_cropEnd.X - _cropStart.X),
        Math.Abs(_cropEnd.Y - _cropStart.Y));

    // ── Pointer events (mouse on desktop; mapped to touch on mobile) ──────────

    private void OnCropPointerPressed(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(CropCanvas);
        if (pos is null) return;
        _cropStart  = new PointF((float)pos.Value.X, (float)pos.Value.Y);
        _cropEnd    = _cropStart;
        _isDragging = true;
        CropCanvas.Invalidate();
    }

    private void OnCropPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging) return;
        var pos = e.GetPosition(CropCanvas);
        if (pos is null) return;
        _cropEnd = new PointF((float)pos.Value.X, (float)pos.Value.Y);
        CropCanvas.Invalidate();
    }

    private async void OnCropPointerReleased(object? sender, PointerEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        var pos = e.GetPosition(CropCanvas);
        if (pos is not null)
            _cropEnd = new PointF((float)pos.Value.X, (float)pos.Value.Y);
        CropCanvas.Invalidate();

        var rect = GetCropRect();
        if (rect.Width > 10 && rect.Height > 10)
            await RunOcrAsync(rect);
    }

    // ── Pan events (reliable drag fallback for touch on Android / iOS) ────────

    private async void OnCropPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Running when _isDragging:
                _cropEnd = new PointF(
                    _cropStart.X + (float)e.TotalX,
                    _cropStart.Y + (float)e.TotalY);
                CropCanvas.Invalidate();
                break;

            case GestureStatus.Completed when _isDragging:
                _isDragging = false;
                CropCanvas.Invalidate();
                var rect = GetCropRect();
                if (rect.Width > 10 && rect.Height > 10)
                    await RunOcrAsync(rect);
                break;

            case GestureStatus.Canceled when _isDragging:
                _isDragging = false;
                _cropStart  = default;
                _cropEnd    = default;
                CropCanvas.Invalidate();
                break;
        }
    }

    // ── OCR pipeline ──────────────────────────────────────────────────────────

    private async Task RunOcrAsync(RectF cropRect)
    {
        if (_ocrRunning) return;
        _ocrRunning      = true;
        OcrBtn.IsEnabled = false;
        OcrBtn.Text      = "Processing…";

        try
        {
            // 1. Capture the PDF viewer element
            var shot = await PdfViewer.CaptureAsync();
            if (shot is null)
            {
                await DisplayAlertAsync("OCR Failed", "Could not capture the document view.", "OK");
                return;
            }

            // 2. Crop to the drawn rectangle using SkiaSharp
            byte[] croppedBytes;
            using (var imgStream = await shot.OpenReadAsync())
            {
                using var ms = new MemoryStream();
                await imgStream.CopyToAsync(ms);
                croppedBytes = CropImageBytes(
                    ms.ToArray(), cropRect,
                    (float)PdfViewer.Width, (float)PdfViewer.Height);
            }

            // 3. Run OCR using the best available engine for this platform
            var text = await RecognizePlatformAsync(croppedBytes);

            // 4. Show result with copy option
            if (string.IsNullOrEmpty(text))
                await DisplayAlertAsync("OCR Result", "No text detected in the selected area.", "OK");
            else
            {
                bool copy = await DisplayAlertAsync("Extracted Text", text, "Copy", "Close");
                if (copy)
                    await Clipboard.Default.SetTextAsync(text);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("OCR Failed", ex.Message, "OK");
        }
        finally
        {
            _ocrRunning      = false;
            OcrBtn.IsEnabled = true;
            SetOcrMode(false);
        }
    }

    // ── Platform OCR implementations ──────────────────────────────────────────

#if WINDOWS
    private static async Task<string> RecognizePlatformAsync(byte[] imageBytes)
    {
        using var ras = new InMemoryRandomAccessStream();
        using var writer = new DataWriter(ras);
        writer.WriteBytes(imageBytes);
        await writer.StoreAsync();
        ras.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(ras);
        var bitmap  = await decoder.GetSoftwareBitmapAsync();

        var engine = OcrEngine.TryCreateFromUserProfileLanguages()
                  ?? OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en"));
        if (engine is null) return "";

        var result = await engine.RecognizeAsync(bitmap);
        return string.Join("\n", result.Lines.Select(l => l.Text));
    }
#elif IOS || MACCATALYST
    private static Task<string> RecognizePlatformAsync(byte[] imageBytes)
    {
        // TODO: implement using Vision.VNRecognizeTextRequest
        return Task.FromResult("OCR is coming soon on iOS / macOS.");
    }
#elif ANDROID
    private static Task<string> RecognizePlatformAsync(byte[] imageBytes)
    {
        // TODO: implement using Google ML Kit TextRecognition
        return Task.FromResult("OCR is coming soon on Android.");
    }
#else
    private static Task<string> RecognizePlatformAsync(byte[] imageBytes)
        => Task.FromResult("OCR is not supported on this platform.");
#endif

    // ── SkiaSharp crop helper ─────────────────────────────────────────────────

    private static byte[] CropImageBytes(byte[] imageBytes, RectF crop, float viewW, float viewH)
    {
        using var src = SKBitmap.Decode(imageBytes);
        if (src is null || viewW <= 0 || viewH <= 0) return imageBytes;

        float sx = src.Width  / viewW;
        float sy = src.Height / viewH;

        int x = Math.Clamp((int)(crop.X      * sx), 0, src.Width);
        int y = Math.Clamp((int)(crop.Y      * sy), 0, src.Height);
        int w = Math.Clamp((int)(crop.Width  * sx), 0, src.Width  - x);
        int h = Math.Clamp((int)(crop.Height * sy), 0, src.Height - y);

        if (w <= 0 || h <= 0) return imageBytes;

        using var cropped = new SKBitmap(w, h);
        using var cvs     = new SKCanvas(cropped);
        cvs.DrawBitmap(src, SKRectI.Create(x, y, w, h), new SKRect(0, 0, w, h));

        using var outMs = new MemoryStream();
        cropped.Encode(outMs, SKEncodedImageFormat.Png, 100);
        return outMs.ToArray();
    }
}
