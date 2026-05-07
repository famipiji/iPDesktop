using iPDesktop.Data;

namespace iPDesktop;

public partial class MainPage : ContentPage
{
    private readonly DatabaseService _db;

    public MainPage(DatabaseService db)
    {
        InitializeComponent();
        _db = db;
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        var username = UsernameEntry.Text?.Trim() ?? "";
        var password = PasswordEntry.Text ?? "";

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowError("Please enter username and password.");
            return;
        }

        LoginBtn.IsEnabled = false;
        var user = await _db.GetUserAsync(username, password);
        LoginBtn.IsEnabled = true;

        if (user is null)
        {
            ShowError("Invalid username or password.");
            return;
        }

        ErrorLabel.IsVisible = false;
        await Shell.Current.GoToAsync(nameof(DashboardPage));
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
