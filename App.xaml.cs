using iPDesktop.Data;

namespace iPDesktop;

public partial class App : Application
{
	public App(DatabaseService db)
	{
		InitializeComponent();
		_ = db.InitAsync();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}