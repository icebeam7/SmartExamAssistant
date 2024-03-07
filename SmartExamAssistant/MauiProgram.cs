using Microsoft.Extensions.Logging;

using SmartExamAssistant.Views;
using SmartExamAssistant.Services;
using SmartExamAssistant.ViewModels;
using SmartExamAssistant.DbContexts;
using SmartExamAssistant.Helpers;

namespace SmartExamAssistant
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            string dbPath = Path.Combine(FileSystem.AppDataDirectory, Constants.DbName);

            builder.Services.AddSqlite<SmartSchoolContext>($"FileName={dbPath}");

            builder.Services.AddSingleton<IDocumentService, DocumentService>()
                .AddSingleton<IDatabaseService, DatabaseService>()
                .AddSingleton<IExamService, ExamService>()
                .AddSingleton<IPDFService, PDFService>();

            builder.Services.AddScoped<DocumentsViewModel>()
                .AddScoped<ExamsViewModel>()
                .AddScoped<ExamDetailViewModel>();

            builder.Services.AddScoped<DocumentsView>()
                .AddScoped<ExamsView>()
                .AddScoped<ExamDetailView>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
