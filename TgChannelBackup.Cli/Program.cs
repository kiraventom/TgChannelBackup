using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using TgChannelBackup.Core;
using TgChannelBackup.Core.Downloader;

namespace TgChannelBackup.Cli;

public static class Program
{
    private const string APP_NAME = "TgChannelBackup";

    public static async Task<int> Main(string[] args)
    {
        var envPath = Path.Combine(AppContext.BaseDirectory, DotNetEnv.Env.DEFAULT_ENVFILENAME);

        DotNetEnv.Env.Load(envPath);

        var builder = Host.CreateApplicationBuilder();

        var appDir = GetProjectDirPath();
        var logsDir = Path.Combine(appDir, "logs");
        Directory.CreateDirectory(logsDir);

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
            .WriteTo.File(Path.Combine(logsDir, "backup-.log"), rollingInterval: RollingInterval.Day);

        var logger = loggerConfig.CreateLogger();

        WTelegram.Helpers.Log = (lvl, msg) => 
        {
            if (lvl is >= (int)LogEventLevel.Warning)
                logger.Write((LogEventLevel)lvl, msg);
        }; 

        var runOptions = BuildRunOptions(logger, args);
        if (runOptions is null)
            return 1;

        var backupPath = Path.Combine(appDir, "main.db");

        builder.Services
            .AddSerilog(logger)
            .AddSingleton(runOptions)
            .AddSingleton<BackupDb>(c => new BackupDb(backupPath))
            .AddSingleton<TelegramService>()
            .AddSingleton<PhotoDownloader>()
            .AddSingleton<DocumentDownloader>()
            .AddHostedService<BackupWorker>();

        await builder.Build().RunAsync();

        return 0;
    }

    private static string GetProjectDirPath()
    {
        if (OperatingSystem.IsWindows())
        {
            var appDataDirPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataDirPath, APP_NAME);
        }

        var homeDirPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homeDirPath, ".local", "share", APP_NAME);
    }

    // TODO CheckCommand
    private static RunOptions BuildRunOptions(ILogger logger, string[] args)
    {
        var channel = new Option<long>("--channel", "-c") 
        { 
            Required = true,
            Description = "ID of channel to backup. Required"
        };

        var target = new Option<string>("--target", "-t")
        {
            Description = "Specify directory to save files to. If not specified, current directory will be selected"
        }.AcceptLegalFilePathsOnly();

        var start = new Option<long?>("--start", "-s")
        {
            Description = "Continue from specific message ID"
        };

        var idFile = new Option<string>("--file", "-f")
        {
            Description = "TODO"
        }.AcceptLegalFileNamesOnly();

        var dryRun = new Option<bool>("--dry", "-d")
        {
            Description = "Do everything exactly the same except writing files"
        };

        var reconcile = new Option<bool>("--reconcile", "-r")
        {
            Description = "Overwrite instead of ignore on hash mismatch"
        };

        var rootCommand = new RootCommand()
        {
            channel, target, start, idFile, dryRun, reconcile
        };

        var parseResult = rootCommand.Parse(args);
        foreach (var error in parseResult.Errors)
            logger.Fatal(error.Message);

        if (parseResult.Errors.Any())
            return null;

        var channelValue = parseResult.GetRequiredValue(channel);
        var targetValue = parseResult.GetValue(target);
        var startValue = parseResult.GetValue(start);
        var idFileValue = parseResult.GetValue(idFile);
        var dryRunValue = parseResult.GetValue(dryRun);
        var reconcileValue = parseResult.GetValue(reconcile);

        if (string.IsNullOrEmpty(targetValue))
            targetValue = Environment.CurrentDirectory;

        return new RunOptions() 
        { 
            ChannelId = channelValue, 
            TargetDir = targetValue, 
            StartId = startValue, 
            IdFile = idFileValue, 
            DryRun = dryRunValue,
            Reconcile = reconcileValue
        };
    }
}
