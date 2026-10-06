using System.Runtime.Versioning;
using Avalonia.Platform;
using GilSweep.Core.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace GilSweep.Desktop.Services;

/// <summary>
/// Windows toasts for a desktop app without a package identity: Gil Sweep registers its
/// AppUserModelID under HKCU so Windows shows the toasts with its name and icon. Uninstalling
/// removes the registration again. Toasts only come while the app runs, so a click is handled
/// in-process and brings the window back.
/// </summary>
[SupportedOSPlatform("windows10.0.17763.0")]
public sealed class WindowsToastNotifier : INotifier
{
    /// <summary>
    /// The ID Velopack gives the Start menu shortcut (velopack.&lt;packId&gt;), so Windows files the
    /// toasts under the same "Gil Sweep" app as the shortcut. The registry entry covers portable copies.
    /// </summary>
    public const string AppId = "velopack.GilSweep";

    private static readonly string RegistryKey = $@"Software\Classes\AppUserModelId\{AppId}";
    private readonly ILogger<WindowsToastNotifier> _logger;
    private readonly IAppEnvironment _environment;
    private ToastNotifier? _notifier;
    private bool _failed;

    public WindowsToastNotifier(IAppEnvironment environment, ILogger<WindowsToastNotifier> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    /// <summary>Raised when the user clicks a toast.</summary>
    public event EventHandler? Activated;

    public void Show(string title, string body)
    {
        if (_failed)
        {
            return;
        }

        try
        {
            _notifier ??= Register();
            var xml = new XmlDocument();
            xml.LoadXml($"<toast><visual><binding template=\"ToastGeneric\"><text>{Escape(title)}</text><text>{Escape(body)}</text></binding></visual></toast>");
            var toast = new ToastNotification(xml);
            toast.Activated += (_, _) => Activated?.Invoke(this, EventArgs.Empty);
            _notifier.Show(toast);
        }
        catch (Exception ex)
        {
            // Notifications are a convenience; the alert log in Watchlist still has every alert.
            _failed = true;
            _logger.LogWarning(ex, "Windows notifications are unavailable");
        }
    }

    public static void Unregister()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(RegistryKey, throwOnMissingSubKey: false);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
        }
    }

    private ToastNotifier Register()
    {
        var icon = Path.Combine(_environment.DataDirectory, "app-icon.png");
        if (!File.Exists(icon))
        {
            Directory.CreateDirectory(_environment.DataDirectory);
            using var source = AssetLoader.Open(new Uri("avares://GilSweep/Assets/app-icon-256.png"));
            using var target = File.Create(icon);
            source.CopyTo(target);
        }

        using (var key = Registry.CurrentUser.CreateSubKey(RegistryKey))
        {
            key.SetValue("DisplayName", "Gil Sweep");
            key.SetValue("IconUri", icon);
        }

        return ToastNotificationManager.CreateToastNotifier(AppId);
    }

    private static string Escape(string text) => System.Security.SecurityElement.Escape(text) ?? "";
}
