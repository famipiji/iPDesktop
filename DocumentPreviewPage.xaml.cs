using iPDesktop.Data;
using iPDesktop.Models;

namespace iPDesktop;

[QueryProperty(nameof(DocId), "docId")]
public partial class DocumentPreviewPage : ContentPage
{
    private readonly DatabaseService _db;
    private Document? _document;

    private static readonly HashSet<string> ImageTypes = ["JPG", "JPEG", "PNG", "GIF", "BMP", "WEBP", "TIFF", "TIF", "ICO", "SVG"];
    private static readonly HashSet<string> TextTypes  = ["TXT", "MD", "CSV", "LOG", "JSON", "XML", "HTML", "HTM", "YAML", "YML", "CS", "JS", "TS", "PY", "CSS"];

    public int DocId { get; set; }

    public DocumentPreviewPage(DatabaseService db)
    {
        InitializeComponent();
        _db = db;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadPreviewAsync();
    }

    private async Task LoadPreviewAsync()
    {
        ShowOnly(LoadingView);

        // Fetch metadata from SQLite (single row by PK — instant)
        _document = await _db.GetDocumentByIdAsync(DocId);

        if (_document is null)
        {
            ShowError("Document record not found.");
            return;
        }

        FileNameLabel.Text = _document.FileName;
        FileMetaLabel.Text  = $"{_document.FileType}  ·  {_document.SizeDisplay}  ·  {_document.UploadedAt:dd MMM yyyy}";

        if (!File.Exists(_document.StoragePath))
        {
            ShowError("File not found on disk.\nIt may have been moved or deleted.");
            return;
        }

        OpenExternalBtn.IsVisible = true;

        var type = _document.FileType.ToUpper();

        try
        {
            if (ImageTypes.Contains(type))
                await ShowImageAsync(_document.StoragePath);
            else if (type == "PDF")
                ShowPdf(_document.StoragePath);
            else if (TextTypes.Contains(type))
                await ShowTextAsync(_document.StoragePath);
            else
                ShowGeneric(_document);
        }
        catch (Exception ex)
        {
            ShowError($"Could not load preview:\n{ex.Message}");
        }
    }

    private async Task ShowImageAsync(string path)
    {
        // Read file bytes on background thread — never blocks UI
        var bytes = await Task.Run(() => File.ReadAllBytes(path));
        PreviewImage.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
        ShowOnly(ImageView);
    }

    private void ShowPdf(string path)
    {
        var uri = new Uri(path).AbsoluteUri;
        PdfView.Source = new UrlWebViewSource { Url = uri };
        ShowOnly(PdfView);
    }

    private async Task ShowTextAsync(string path)
    {
        // Read on background thread to keep UI responsive for large files
        var content = await Task.Run(() =>
        {
            const int maxChars = 50_000;
            using var reader = new StreamReader(path);
            var buffer = new char[maxChars];
            var read = reader.Read(buffer, 0, maxChars);
            var text = new string(buffer, 0, read);
            return reader.EndOfStream ? text : text + "\n\n[Showing first 50,000 characters…]";
        });

        TextContent.Text = content;
        ShowOnly(TextView);
    }

    private void ShowGeneric(Document doc)
    {
        GenericFileName.Text = doc.FileName;
        GenericMeta.Text     = $"{doc.FileType}  ·  {doc.SizeDisplay}";
        OpenExternalBtn.IsVisible = false; // shown inside GenericView instead
        ShowOnly(GenericView);
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        OpenExternalBtn.IsVisible = false;
        ShowOnly(ErrorView);
    }

    // Only one content area visible at a time — O(1), no rebind cost
    private void ShowOnly(View target)
    {
        foreach (var v in new View[] { LoadingView, ImageView, PdfView, TextView, GenericView, ErrorView })
            v.IsVisible = v == target;
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        // Free image bytes from memory when leaving
        PreviewImage.Source = null;
        await Shell.Current.GoToAsync("..");
    }

    private async void OnOpenExternalClicked(object? sender, EventArgs e)
    {
        if (_document is null || !File.Exists(_document.StoragePath)) return;
        await Launcher.Default.OpenAsync(new OpenFileRequest
        {
            File = new ReadOnlyFile(_document.StoragePath)
        });
    }
}
