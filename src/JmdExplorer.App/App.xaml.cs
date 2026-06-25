using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Messaging;
using JmdExplorer.App.Services;
using JmdExplorer.App.ViewModels;
using JmdExplorer.Core.Abstractions;
using JmdExplorer.Core.Services;
using JmdExplorer.Decoders.Plugins;
using JmdExplorer.Decoders.Profiles;
using JmdExplorer.Infrastructure.Files;
using JmdExplorer.Infrastructure.Logging;
using JmdExplorer.Infrastructure.Reporting;
using Microsoft.Extensions.DependencyInjection;

namespace JmdExplorer.App;

public partial class App : Application
{
    private IServiceProvider _services = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logger = new FileAppLogger();
        HookGlobalExceptionHandlers(logger);

        var sc = new ServiceCollection();
        ConfigureServices(sc, logger);
        _services = sc.BuildServiceProvider();

        logger.Info("JMD Explorer starting up.");

        var window = _services.GetRequiredService<MainWindow>();
        window.DataContext = _services.GetRequiredService<MainViewModel>();
        window.Show();
    }

    private static void ConfigureServices(IServiceCollection sc, IAppLogger logger)
    {
        // Infrastructure / cross-cutting
        sc.AddSingleton<IAppLogger>(logger);
        sc.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        sc.AddSingleton(new FileServiceOptions());
        sc.AddSingleton<FileService>();
        sc.AddSingleton<ReportWriter>();

        // Core services
        sc.AddSingleton<RegionDetector>();
        sc.AddSingleton(new SignatureScanner());
        sc.AddSingleton<StringScanner>();
        sc.AddSingleton<RecordPatternDetector>();
        sc.AddSingleton<RawBlockExtractor>();

        // Format profiles (order independent; UnknownBinary is the fallback)
        sc.AddSingleton<IFormatProfile, XenonDataFormatV4sProfile>();
        sc.AddSingleton<IFormatProfile, UnknownBinaryProfile>();
        sc.AddSingleton<FormatDetector>();

        // Decoder plugins (registration order = display order)
        sc.AddSingleton<IJmdDecoderPlugin, GenericSignatureScannerPlugin>();
        sc.AddSingleton<IJmdDecoderPlugin, XenonV4sInspectorPlugin>();
        sc.AddSingleton<IJmdDecoderPlugin, XenonV4sDecoderPlugin>();

        // App services
        sc.AddSingleton<IDialogService, DialogService>();
        sc.AddSingleton<AppSession>();

        // Page view models (stateful singletons that subscribe to AppSession)
        sc.AddSingleton<FileInspectorViewModel>();
        sc.AddSingleton<HexViewerViewModel>();
        sc.AddSingleton<StructureAnalyzerViewModel>();
        sc.AddSingleton<EmbeddedScannerViewModel>();
        sc.AddSingleton<StringScannerViewModel>();
        sc.AddSingleton<ExtractedFilesViewModel>();
        sc.AddSingleton<DecoderPluginsViewModel>();
        sc.AddSingleton<ExportReportViewModel>();
        sc.AddSingleton<SettingsViewModel>();

        sc.AddSingleton<MainViewModel>();
        sc.AddSingleton<MainWindow>();
    }

    private void HookGlobalExceptionHandlers(IAppLogger logger)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            logger.Error("Unhandled UI exception", args.Exception);
            MessageBox.Show(
                "An unexpected error occurred but JMD Explorer kept running.\n\n" + args.Exception.Message,
                "Unexpected error", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true; // never crash the app
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            logger.Error("Unhandled domain exception", args.ExceptionObject as Exception);

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            logger.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };
    }
}
