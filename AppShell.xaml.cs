namespace iPDesktop;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(nameof(DashboardPage), typeof(DashboardPage));
		Routing.RegisterRoute(nameof(DocumentPreviewPage), typeof(DocumentPreviewPage));
	}
}
