using Microsoft.Extensions.Logging;
using iPDesktop.Data;
using iPDesktop.Services;
using Syncfusion.Maui.Core.Hosting;

namespace iPDesktop;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureSyncfusionCore()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		builder.Services.AddSingleton<DatabaseService>();
		builder.Services.AddSingleton<DocumentConverter>();
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<DashboardPage>();
		builder.Services.AddTransient<DocumentPreviewPage>();

		return builder.Build();
	}
}
