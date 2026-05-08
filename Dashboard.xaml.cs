using iPDesktop.Data;
using iPDesktop.Models;
using System.Collections.ObjectModel;

namespace iPDesktop;

public partial class DashboardPage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly ObservableCollection<Document> _documents = [];
    private readonly string _storageDir;

    private int _currentPage = 0;
    private const int PageSize = 50;
    private bool _isLoadingMore = false;
    private bool _isSearching = false;
    private string _searchQuery = "";

    public DashboardPage(DatabaseService db)
    {
        InitializeComponent();
        _db = db;
        _storageDir = Path.Combine(FileSystem.AppDataDirectory, "documents");
        Directory.CreateDirectory(_storageDir);
        DocumentsCollection.ItemsSource = _documents;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshDocumentsAsync();
    }
//upload docs button
    private async void OnUploadClicked(object? sender, EventArgs e)
    {
        try
        {
            UploadBtn.IsEnabled = false;
            StatusLabel.Text = "Picking file...";

            var result = await FilePicker.Default.PickAsync(new PickOptions());

            if (result is null)
            {
                StatusLabel.Text = "";
                return;
            }

            StatusLabel.Text = "Uploading...";

            var ext = Path.GetExtension(result.FileName).TrimStart('.').ToUpper();
            var destFileName = $"{Guid.NewGuid().ToString("N")}_{result.FileName}";
            var destPath = Path.Combine(_storageDir, destFileName);

            using var sourceStream = await result.OpenReadAsync();
            using var destStream = File.Create(destPath);
            await sourceStream.CopyToAsync(destStream);

            var fileInfo = new FileInfo(destPath);
            var doc = new Document
            {
                FileName = result.FileName,
                FileType = string.IsNullOrEmpty(ext) ? "FILE" : ext,
                FileSizeBytes = fileInfo.Length,
                StoragePath = destPath,
                UploadedAt = DateTime.UtcNow
            };

            await _db.InsertDocumentAsync(doc);
            await RefreshDocumentsAsync();
            StatusLabel.Text = "Uploaded successfully.";

            await Task.Delay(2000);
            StatusLabel.Text = "";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "Upload failed.";
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
        finally
        {
            UploadBtn.IsEnabled = true;
        }
    }

    private async void OnDocumentSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not Document doc) return;
        DocumentsCollection.SelectedItem = null;
        await Shell.Current.GoToAsync($"{nameof(DocumentPreviewPage)}?docId={doc.Id}");
    }

    private async void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchQuery = e.NewTextValue?.Trim() ?? "";
        _isSearching = !string.IsNullOrEmpty(_searchQuery);
        _currentPage = 0;
        _documents.Clear();
        await LoadPageAsync();
    }

    private async void OnLoadMore(object? sender, EventArgs e)
    {
        if (_isLoadingMore) return;
        _isLoadingMore = true;
        _currentPage++;
        await LoadPageAsync();
        _isLoadingMore = false;
    }

    private async Task RefreshDocumentsAsync()
    {
        _currentPage = 0;
        _documents.Clear();
        await LoadPageAsync();
    }

    // ── Menu bar dropdowns ────────────────────────────────────────────────────

    private void OnMenuFileClicked(object? sender, EventArgs e)
        => ToggleDropdown(FileDropdown, (View)sender!);

    private void OnMenuSettingsClicked(object? sender, EventArgs e)
        => ToggleDropdown(SettingsDropdown, (View)sender!);

    private void OnMenuHelpClicked(object? sender, EventArgs e)
        => ToggleDropdown(HelpDropdown, (View)sender!);

    private void ToggleDropdown(Border target, View anchor)
    {
        bool open = !target.IsVisible;
        CloseAllDropdowns();
        if (open)
        {
            target.Margin = new Thickness(anchor.X, 32, 0, 0);
            target.IsVisible = true;
            DropdownOverlay.IsVisible = true;
        }
    }

    private void CloseAllDropdowns()
    {
        FileDropdown.IsVisible     = false;
        SettingsDropdown.IsVisible = false;
        HelpDropdown.IsVisible     = false;
        DropdownOverlay.IsVisible  = false;
    }

    private void OnOverlayTapped(object? sender, TappedEventArgs e) => CloseAllDropdowns();

    private async void OnDropdownUploadClicked(object? sender, EventArgs e)
    {
        CloseAllDropdowns();
        await Task.Yield();
        OnUploadClicked(sender, e);
    }

    private void OnDropdownExitClicked(object? sender, EventArgs e)
    {
        CloseAllDropdowns();
        Application.Current?.Quit();
    }

    private async void OnDropdownPreferencesClicked(object? sender, EventArgs e)
    {
        CloseAllDropdowns();
        await DisplayAlertAsync("Settings", "Preferences coming soon.", "OK");
    }

    private async void OnDropdownProfileClicked(object? sender, EventArgs e)
    {
        CloseAllDropdowns();
        await Navigation.PushModalAsync(new ProfileSettingPage());
    }

    private async void OnDropdownAboutClicked(object? sender, EventArgs e)
    {
        CloseAllDropdowns();
        await DisplayAlertAsync("About", "iPDesktop v1.0\nDocument viewer & OCR tool.", "OK");
    }

    private async Task LoadPageAsync()
    {
        List<Document> page;

        if (_isSearching)
            page = await _db.SearchDocumentsAsync(_searchQuery, _currentPage, PageSize);
        else
            page = await _db.GetDocumentsAsync(_currentPage, PageSize);

        foreach (var doc in page)
            _documents.Add(doc);
    }
}
