using iPDesktop.Data;
using iPDesktop.Models;
using iPDesktop.Services;

namespace iPDesktop;

[QueryProperty(nameof(DocId), "docId")]
public partial class DocumentPreviewPage : ContentPage
{
    private readonly DatabaseService   _db;
    private readonly DocumentConverter _converter;
    private Document? _document;
    private Stream?   _pdfStream;

    public int DocId { get; set; }

    public DocumentPreviewPage(DatabaseService db, DocumentConverter converter)
    {
        InitializeComponent();
        _db        = db;
        _converter = converter;
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

        FileNameLabel.Text        = _document.FileName;
        FileMetaLabel.Text        = $"{_document.FileType}  ·  {_document.SizeDisplay}  ·  {_document.UploadedAt:dd MMM yyyy}";
        OpenExternalBtn.IsVisible = File.Exists(_document.StoragePath);

        if (!File.Exists(_document.StoragePath))
        {
            ShowError("File not found on disk.\nIt may have been moved or deleted.");
            return;
        }

        if (!_converter.CanConvert(_document.FileType))
        {
            UnsupportedLabel.Text     = _document.FileName;
            OpenExternalBtn.IsVisible = false;
            ShowOnly(UnsupportedView);
            return;
        }

        // Convert to PDF on background thread — only runs when user taps the document
        var (pdfStream, convertError) = await _converter.ConvertToPdfAsync(_document.StoragePath, _document.FileType);

        if (convertError is not null)
        {
            ShowError(convertError);
            return;
        }

        _pdfStream = pdfStream;

        if (_pdfStream is null || _pdfStream == Stream.Null)
        {
            UnsupportedLabel.Text     = _document.FileName;
            OpenExternalBtn.IsVisible = false;
            ShowOnly(UnsupportedView);
            return;
        }

        // Feed the PDF stream to Syncfusion — viewer handles paging, zoom, scroll
        PdfViewer.DocumentSource = _pdfStream;
        ShowOnly(PdfViewer);
    }

    private void ShowOnly(View target)
    {
        foreach (var v in new View[] { LoadingView, PdfViewer, UnsupportedView, ErrorView })
            v.IsVisible = v == target;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text           = message;
        OpenExternalBtn.IsVisible = false;
        ShowOnly(ErrorView);
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        // Unload PDF and free stream memory before navigating away
        PdfViewer.DocumentSource = null;
        _pdfStream?.Dispose();
        _pdfStream = null;
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
