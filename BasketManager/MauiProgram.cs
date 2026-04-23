using BasketManager.Services;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace BasketManager
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif
            builder.Services.AddSingleton<BasketManager.Services.DatabaseService>();
            builder.Services.AddSingleton<BasketManager.ViewModels.MainViewModel>();
            builder.Services.AddSingleton<IDialogService, DialogService>();
            builder.Services.AddSingleton<BasketManager.MainPage>();
            builder.Services.AddSingleton<CanchaPage>();
            builder.Services.AddTransient<DraftPage>();
            return builder.Build();
        }
    }
}
