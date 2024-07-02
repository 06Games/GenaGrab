using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Logging;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using DiscordRPC;
using GeneaGrab.Core.Helpers;
using GeneaGrab.Core.Models;
using GeneaGrab.Helpers;
using GeneaGrab.Services;
using GeneaGrab.Views;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Formatting.Compact;
using SingleInstance;
using URIScheme;

// IApplicationPlatformEvents.RaiseUrlsOpened is not obsolete, it's just an unstable API
#pragma warning disable CS0618

namespace GeneaGrab
{
    public partial class App : Application
    {
        private ISingleInstanceService SingleInstance { get; set; } = null!;
        public DiscordRpcClient Discord { get; } = new("1120393636455129229");

        public override void Initialize()
        {
            using (var client = new DatabaseContext())
            {
                client.Database.Migrate();
            }
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(new RenderedCompactJsonFormatter(), Path.Combine(LocalData.LogFolder, $"{DateTime.UtcNow:yyyy-MM-dd HH-mm-ss}.ndjson"))
                .WriteTo.Debug(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{Area} {Source}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
            Data.SetLogger(Log.Logger);
            Logger.Sink = new SerilogSink();

            Data.GetImage = LocalData.GetImage;
            Data.SaveImage = LocalData.SaveImageAsync;
            Data.ToThumbnail = LocalData.ToThumbnailAsync;

            UrlsOpened += (_, e) =>
            {
                Log.Information("Opened by Url: {Urls}", e.Urls);
                foreach (var url in e.Urls)
                {
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) continue;
                    if (uri.Scheme == "geneagrab")
                    {
                        var query = uri.ParseQueryString();
                        if (!Uri.TryCreate(query["url"], UriKind.Absolute, out uri)) continue;
                    }
                    Dispatcher.UIThread.Post(() => NavigationService.OpenTab(NavigationService.NewTab<RegistryViewer>(uri)));
                }
            };

            var desktop = ApplicationLifetime as ClassicDesktopStyleApplicationLifetime;
            var urls = desktop?.Args?.Where(arg => Uri.IsWellFormedUriString(arg, UriKind.Absolute)).ToArray() ?? Array.Empty<string>();
            SingleInstance = new SingleInstanceService("GeneaGrab");
            SingleInstance.TryStartSingleInstance();
            if (SingleInstance.IsFirstInstance)
            {
                SingleInstance.StartListenServer();
                Log.Information("This is the first instance");

                SingleInstance.Received.Subscribe(receive => Task.Run(async () =>
                {
                    var (message, response) = receive;
                    var args = await Json.ToObjectAsync<string[]>(message);
                    urls = args.Where(arg => Uri.IsWellFormedUriString(arg, UriKind.Absolute)).ToArray();
                    if (urls.Length > 0) ((IApplicationPlatformEvents)this).RaiseUrlsOpened(urls);

                    response("success"); // Send response
                }));
            }
            else
            {
                Log.Information("This is not the first instance");
                if (desktop != null)
                    try
                    {
#pragma warning disable VSTHRD002
                        // This instance is not intended to be displayed, so it's not really a problem if the main thread freezes for a while
                        Task.Run(async () => await SingleInstance.SendMessageToFirstInstanceAsync(await Json.StringifyAsync(desktop.Args))).Wait();
#pragma warning restore VSTHRD002
                        if (urls.Length > 0) return;
                    }
                    catch (Exception e) { Log.Error(e, "Communication error with first instance"); }
            }
            AvaloniaXamlLoader.Load(this);
            if (urls.Length > 0) ((IApplicationPlatformEvents)this).RaiseUrlsOpened(urls);

            if (desktop != null)
            {
                desktop.MainWindow = new MainWindow();
                desktop.Exit += (_, _) =>
                {
                    Discord.Dispose();
                    SingleInstance.Dispose();
                };
            }

            Discord.Initialize(); //Connect to the RPC
            // Refresh Discord RPC every 500 ms
            var timer = new System.Timers.Timer(500);
            timer.Elapsed += (_, _) =>
            {
                if (!Discord.IsDisposed) Discord.Invoke();
            };
            timer.Start();

            try { desktop?.Start(desktop.Args ?? []); }
            catch (Exception e) { Log.Fatal(e, "A fatal error occured"); }
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) // On macOS this is done by the Info.plist file
            {
                try
                {
                    const string scheme = @"geneagrab";
                    var path = Environment.ProcessPath;
                    if (path is null) return;
                    var service = URISchemeServiceFactory.GetURISchemeSerivce(scheme, @$"URL:{scheme} Protocol", path);
#if DEBUG
                    if (!service.CheckAny()) // Check if the protocol is registered to any application.
#else
                    if (!service.Check()) // Check if the protocol is registered to the current application.
#endif
                        service.Set(); // Register the service.
                }
                catch (PlatformNotSupportedException e)
                {
                    Log.Warning(e, "Couldn't register Uri Scheme");
                }
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
