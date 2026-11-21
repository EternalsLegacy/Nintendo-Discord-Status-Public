using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using NintendoDiscordStatus.Core.Constants;
using NintendoDiscordStatus.Core.Models;
using NintendoDiscordStatus.Core.Presenters;
using NintendoDiscordStatus.Core.Services;
using NintendoDiscordStatus.Services;

namespace NintendoDiscordStatus;

public class App : Application
{
    private TrayIcon? AppTrayIcon;
    private NativeMenuItem? RecentGamesRootItem;
    private HttpClient? SharedHttpClient;
    private DiscordService? AppDiscordService;
    private MainPresenter? AppPresenter;
    private MainWindow? AppMainWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime Desktop)
        {
            SharedHttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(AppConstants.ImageRequestTimeoutSeconds)
            };

            SettingsService Settings = SettingsService.Global;
            Settings.Load();

            LibraryService Library = new LibraryService(HttpClientInstance: SharedHttpClient);
            PresetService Presets = new PresetService();
            SystemTimeProvider TimeProvider = new SystemTimeProvider();
            AppDiscordService = new DiscordService(new DiscordRpcClientFactory(), TimeProvider);
            ImageCacheService ImageCache = new ImageCacheService(SharedHttpClient);
            UpdateService Updates = new UpdateService(new SystemProcessRunner(), SharedHttpClient);

            AppPresenter = new MainPresenter(
                Settings,
                Library,
                Presets,
                AppDiscordService,
                ImageCache,
                Updates,
                TimeProvider);

            AppMainWindow = new MainWindow(
                AppPresenter,
                Settings,
                Library,
                Presets,
                AppDiscordService,
                Updates);

            AppMainWindow.RecentGamesUpdatedForTray += (Sender, Args) => RebuildRecentGamesTrayMenu();

            Desktop.MainWindow = AppMainWindow;
            ConfigureTrayIcon(Desktop, Settings);
            RebuildRecentGamesTrayMenu();

            Settings.SettingsChanged += (Sender, UpdatedSettings) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (AppTrayIcon != null)
                    {
                        AppTrayIcon.IsVisible = UpdatedSettings.MinimizeToTrayOnClose;
                    }
                });
            };

            Desktop.Exit += OnDesktopExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureTrayIcon(IClassicDesktopStyleApplicationLifetime Desktop, SettingsService Settings)
    {
        NativeMenu TrayMenu = new NativeMenu();

        NativeMenuItem OpenItem = new NativeMenuItem("Open Nintendo Rich Presence");
        OpenItem.Click += (Sender, Args) => AppMainWindow?.ShowFromTray();
        TrayMenu.Items.Add(OpenItem);

        RecentGamesRootItem = new NativeMenuItem("Recent Games")
        {
            Menu = new NativeMenu(),
            IsEnabled = false
        };
        TrayMenu.Items.Add(RecentGamesRootItem);

        NativeMenuItem StopItem = new NativeMenuItem("Stop Discord RPC");
        StopItem.Click += (Sender, Args) => AppMainWindow?.StopPresenceFromTray();
        TrayMenu.Items.Add(StopItem);

        TrayMenu.Items.Add(new NativeMenuItemSeparator());

        NativeMenuItem ExitItem = new NativeMenuItem("Exit");
        ExitItem.Click += (Sender, Args) =>
        {
            AppMainWindow?.ForceExit();
            Desktop.Shutdown();
        };
        TrayMenu.Items.Add(ExitItem);

        WindowIcon? TrayWindowIcon = TryLoadAppIcon();

        AppTrayIcon = new TrayIcon
        {
            Icon = TrayWindowIcon,
            ToolTipText = $"Nintendo Rich Presence v{AppConstants.CurrentVersionString}",
            Menu = TrayMenu,
            IsVisible = Settings.Current.MinimizeToTrayOnClose
        };
        AppTrayIcon.Clicked += (Sender, Args) => AppMainWindow?.ShowFromTray();

        TrayIcon.SetIcons(this, new TrayIcons { AppTrayIcon });
    }

    private void RebuildRecentGamesTrayMenu()
    {
        if (RecentGamesRootItem?.Menu == null || AppMainWindow == null)
        {
            return;
        }

        RecentGamesRootItem.Menu.Items.Clear();
        IReadOnlyList<RecentGameModel> RecentGames = AppMainWindow.GetRecentGamesSnapshot();
        RecentGamesRootItem.IsEnabled = RecentGames.Count > 0;

        foreach (RecentGameModel Item in RecentGames)
        {
            NativeMenuItem GameMenuItem = new NativeMenuItem($"{Item.GameName} ({Item.ConsoleName})");
            GameMenuItem.Click += (Sender, Args) =>
                AppMainWindow.QuickSwitchFromTray(Item.ConsoleName, Item.GameName);
            RecentGamesRootItem.Menu.Items.Add(GameMenuItem);
        }
    }

    private void OnDesktopExit(object? Sender, ControlledApplicationLifetimeExitEventArgs Args)
    {
        if (AppTrayIcon != null)
        {
            AppTrayIcon.IsVisible = false;
            AppTrayIcon.Dispose();
            AppTrayIcon = null;
        }

        AppPresenter?.Dispose();
        AppDiscordService?.Dispose();
        SharedHttpClient?.Dispose();
    }

    private static WindowIcon? TryLoadAppIcon()
    {
        try
        {
            using Stream IconStream = AssetLoader.Open(new Uri("avares://NintendoDiscordStatus/app_icon.ico"));
            return new WindowIcon(IconStream);
        }
        catch
        {
            return null;
        }
    }
}
