using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace Cattech.Optimizer.Pro.UI;

/// <summary>
/// Punto de entrada de la aplicación. La composición de servicios ocurre en
/// <see cref="ViewModels.MainViewModel"/>, que se enlaza como DataContext en MainWindow.
/// Además instala un manejador global de errores: un fallo inesperado queda en
/// logs\app-YYYYMMDD.log y se informa al técnico, en lugar de cerrar la app en silencio.
/// </summary>
public partial class App : Application
{
    private static readonly object LogLock = new();
    private bool _unhandledErrorShown;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // La app queda viva: el técnico puede guardar su trabajo y adjuntar el log.
        e.Handled = true;
        ReportUnexpectedError(e.Exception, "Interfaz de usuario");
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
            ReportUnexpectedError(exception, "Proceso");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        ReportUnexpectedError(e.Exception, "Tarea en segundo plano");
    }

    private void ReportUnexpectedError(Exception exception, string origin)
    {
        var logPath = TryWriteLog(origin, exception);
        if (_unhandledErrorShown)
            return;
        _unhandledErrorShown = true;

        try
        {
            MessageBox.Show(
                "Ocurrió un error inesperado y la aplicación siguió funcionando.\n\n" +
                $"Origen: {origin}\n" +
                (string.IsNullOrWhiteSpace(logPath)
                    ? "No se pudo escribir el registro de errores."
                    : $"El detalle técnico quedó registrado en:\n{logPath}") +
                "\n\nPodés continuar trabajando; si el problema se repite, adjuntá ese archivo al reporte.",
                "CATTECH OPTIMIZER PRO",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch
        {
            // Nunca dejar que el manejador de errores vuelva a fallar.
        }
    }

    private static string TryWriteLog(string origin, Exception exception)
    {
        try
        {
            var root = AppDomain.CurrentDomain.BaseDirectory;
            var directory = Path.Combine(root, "logs");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"app-{DateTime.Now:yyyyMMdd}.log");

            var builder = new StringBuilder();
            builder.AppendLine(new string('-', 78));
            builder.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {origin}");
            builder.AppendLine(exception.ToString());
            if (exception is AggregateException aggregate)
            {
                foreach (var inner in aggregate.Flatten().InnerExceptions)
                    builder.AppendLine("  -> " + inner);
            }
            builder.AppendLine();

            lock (LogLock)
            {
                File.AppendAllText(path, builder.ToString(), Encoding.UTF8);
            }
            return path;
        }
        catch
        {
            return string.Empty;
        }
    }
}
