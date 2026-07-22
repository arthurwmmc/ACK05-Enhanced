using Ack05Enhanced.Core;
using Ack05Enhanced.Probe;
using Ack05Enhanced.Windows;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    var command = args.Length == 0 ? "list" : args[0].ToLowerInvariant();

    try
    {
        return command switch
        {
            "list" => ListDevices(),
            "decode" => Decode(args.Skip(1).ToArray()),
            "validate-config" => await ValidateConfigAsync(args.Skip(1).ToArray()),
            "foreground" => ShowForegroundProcess(args.Skip(1).ToArray()),
            "monitor" => await MonitorAsync(args.Skip(1).ToArray()),
            "watch" => await WatchAsync(args.Skip(1).ToArray()),
            "run" => await RunConfiguredAsync(args.Skip(1).ToArray()),
            "raw-input" => await RawInputAsync(args.Skip(1).ToArray()),
            "help" or "--help" or "-h" => ShowHelp(),
            _ => UnknownCommand(command),
        };
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Stopped.");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"ERROR: {exception.Message}");
        return 1;
    }
}

static int ListDevices()
{
    var devices = HidDiscovery.EnumerateAck05Collections();
    if (devices.Count == 0)
    {
        Console.WriteLine("No present ACK05 HID collections found (VID 28BD / PID 0202).");
        return 2;
    }

    for (var index = 0; index < devices.Count; index++)
    {
        var device = devices[index];
        Console.WriteLine($"[{index}] {device.Manufacturer ?? "?"} {device.Product ?? "?"}");
        Console.WriteLine($"    VID:PID     {device.VendorId:X4}:{device.ProductId:X4}");
        Console.WriteLine($"    Usage       {device.UsagePage:X4}:{device.Usage:X4}");
        Console.WriteLine($"    Report bytes input={device.InputReportLength} output={device.OutputReportLength}");
        Console.WriteLine($"    Vendor raw  {(device.IsAck05VendorCollection ? "candidate" : "no")}");
        Console.WriteLine($"    Path         {device.Path}");
    }

    return 0;
}

static int Decode(string[] values)
{
    if (values.Length == 0)
    {
        Console.Error.WriteLine("decode requires hexadecimal bytes.");
        return 2;
    }

    var normalized = string.Concat(values)
        .Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase)
        .Replace("-", string.Empty)
        .Replace(":", string.Empty)
        .Replace(",", string.Empty)
        .Replace(" ", string.Empty);

    byte[] report;
    try
    {
        report = Convert.FromHexString(normalized);
    }
    catch (FormatException)
    {
        Console.Error.WriteLine("Invalid hexadecimal report.");
        return 2;
    }

    if (!Ack05RawReportDecoder.TryDecode(report, out var snapshot, out var error))
    {
        Console.Error.WriteLine(error);
        return 3;
    }

    var held = Enum.GetValues<Ack05Button>()
        .Where(snapshot.IsHeld)
        .Select(button => button.ToString())
        .ToArray();

    Console.WriteLine($"Held: {(held.Length == 0 ? "none" : string.Join(", ", held))}");
    Console.WriteLine($"Roller: {snapshot.Roller}");
    return 0;
}

static async Task<int> ValidateConfigAsync(string[] values)
{
    if (values.Length != 1)
    {
        Console.Error.WriteLine("validate-config requires exactly one JSON path.");
        return 2;
    }

    var path = Path.GetFullPath(values[0]);
    var configuration = Ack05ConfigurationJson.Deserialize(
        await File.ReadAllTextAsync(path));
    Console.WriteLine(
        $"Valid schema v{configuration.SchemaVersion}: " +
        $"{configuration.Profiles.Length} profile(s), default={configuration.DefaultProfileId}");
    return 0;
}

static int ShowForegroundProcess(string[] values)
{
    if (values.Length != 0)
    {
        Console.Error.WriteLine("foreground accepts no options.");
        return 2;
    }

    var processName = new WindowsForegroundProcessProvider().GetForegroundProcessName();
    Console.WriteLine(processName is null
        ? "No foreground process was available."
        : $"Foreground process: {processName}");
    return processName is null ? 2 : 0;
}

static async Task<int> MonitorAsync(string[] options)
{
    var enableBitmask = false;
    int? collectionIndex = null;

    for (var index = 0; index < options.Length; index++)
    {
        if (options[index] == "--enable-bitmask")
        {
            enableBitmask = true;
            continue;
        }

        if (options[index] == "--index")
        {
            if (index + 1 >= options.Length ||
                !int.TryParse(options[++index], out var parsedIndex) ||
                parsedIndex < 0)
            {
                Console.Error.WriteLine("--index requires a non-negative collection index.");
                return 2;
            }

            collectionIndex = parsedIndex;
            continue;
        }

        Console.Error.WriteLine($"Unknown monitor option: {options[index]}");
        return 2;
    }

    if (enableBitmask && collectionIndex.HasValue)
    {
        Console.Error.WriteLine("--index is read-only and cannot be combined with --enable-bitmask.");
        return 2;
    }

    var devices = HidDiscovery.EnumerateAck05Collections();
    HidDeviceInfo selectedDevice;

    if (collectionIndex.HasValue)
    {
        if (collectionIndex.Value >= devices.Count)
        {
            Console.Error.WriteLine(
                $"Collection index {collectionIndex.Value} is unavailable; found {devices.Count} collections.");
            return 2;
        }

        selectedDevice = devices[collectionIndex.Value];
        Console.WriteLine(
            $"Read-only collection [{collectionIndex.Value}] " +
            $"{selectedDevice.UsagePage:X4}:{selectedDevice.Usage:X4}, " +
            $"input={selectedDevice.InputReportLength} bytes.");
    }
    else
    {
        var candidates = devices
            .Where(device => device.IsAck05VendorCollection)
            .ToArray();

        if (candidates.Length == 0)
        {
            Console.Error.WriteLine("No ACK05 vendor HID collection (FF0A:0001) is present.");
            return 2;
        }

        if (candidates.Length > 1)
        {
            Console.Error.WriteLine("Multiple ACK05 vendor collections found; disconnect extra devices first.");
            return 2;
        }

        selectedDevice = candidates[0];
    }

    if (enableBitmask)
    {
        Console.WriteLine("WARNING: --enable-bitmask will write only the documented ACK05 command");
        Console.WriteLine("         02-B0-04-00... to the vendor HID collection.");
    }

    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    };

    await HidMonitor.RunAsync(selectedDevice, enableBitmask, cancellation.Token);
    return 0;
}

static async Task<int> RawInputAsync(string[] options)
{
    if (options.SequenceEqual(new[] { "--list" }))
    {
        RawInputMonitor.ListDevices();
        return 0;
    }

    var diagnoseOtherSources = options.SequenceEqual(new[] { "--diagnostic-sources" });
    if (options.Length > 0 && !diagnoseOtherSources)
    {
        Console.Error.WriteLine(
            "raw-input accepts only --list or --diagnostic-sources.");
        return 2;
    }

    if (HidDiscovery.EnumerateAck05Collections().Count == 0)
    {
        Console.Error.WriteLine("No ACK05 HID collections are present.");
        return 2;
    }

    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    };

    await RawInputMonitor.RunAsync(diagnoseOtherSources, cancellation.Token);
    return 0;
}

static async Task<int> WatchAsync(string[] options)
{
    var unknown = options.Where(option => option != "--enable-bitmask").ToArray();
    if (unknown.Length > 0)
    {
        Console.Error.WriteLine($"Unknown watch option: {string.Join(", ", unknown)}");
        return 2;
    }

    var enableBitmask = options.Contains("--enable-bitmask");
    if (enableBitmask)
    {
        Console.WriteLine("WARNING: --enable-bitmask sends the documented ACK05 raw-mode command");
        Console.WriteLine("         once after each successful vendor collection connection.");
    }
    else
    {
        Console.WriteLine("Read-only reconnecting watch: no command will be sent to the device.");
    }

    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    };

    var service = new Ack05InputService(
        new HidAck05ReportSourceFactory(enableBitmask),
        new ConsoleAck05InputSink());
    await service.RunAsync(cancellation.Token);
    return 0;
}

static async Task<int> RunConfiguredAsync(string[] options)
{
    string? configPath = null;
    var enableBitmask = false;
    var allowSendInput = false;
    var allowTargetLaunch = false;

    for (var index = 0; index < options.Length; index++)
    {
        switch (options[index])
        {
            case "--config" when index + 1 < options.Length:
                configPath = options[++index];
                break;
            case "--enable-bitmask":
                enableBitmask = true;
                break;
            case "--allow-sendinput":
                allowSendInput = true;
                break;
            case "--allow-target-launch":
                allowTargetLaunch = true;
                break;
            default:
                Console.Error.WriteLine($"Unknown or incomplete run option: {options[index]}");
                return 2;
        }
    }

    if (configPath is null || !enableBitmask || !allowSendInput)
    {
        Console.Error.WriteLine(
            "run requires --config <path> --enable-bitmask --allow-sendinput.");
        Console.Error.WriteLine(
            "Both device raw-mode output and Windows synthetic input must be explicitly enabled.");
        return 2;
    }

    var json = await File.ReadAllTextAsync(configPath);
    var configuration = Ack05ConfigurationJson.Deserialize(json);
    var profile = new Ack05ProfileSelector(configuration).Select(processName: null);

    Console.WriteLine("WARNING: configured run sends the documented ACK05 raw-mode command");
    Console.WriteLine("         and emits Windows SendInput keyboard/mouse events.");
    Console.WriteLine(allowTargetLaunch
        ? "WARNING: configured Open Target actions may launch applications, files, folders, or URLs."
        : "Open Target actions are disabled; add --allow-target-launch to enable them.");
    Console.WriteLine($"Config: {Path.GetFullPath(configPath)}");
    Console.WriteLine($"Profile: {profile.Id} ({profile.Name})");

    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    };

    using var sink = new ConsoleConfiguredAck05InputSink(
        configuration,
        allowTargetLaunch: allowTargetLaunch);
    var service = new Ack05InputService(
        new HidAck05ReportSourceFactory(enableBitmask: true),
        sink,
        new Ack05InputServiceOptions(
            TimeSpan.FromSeconds(1),
            64,
            useKeymapLayers: true));
    var serviceTask = service.RunAsync(cancellation.Token);
    var profileTask = sink.MonitorForegroundProfileAsync(cancellation.Token);
    var tapHoldTask = sink.MonitorTapHoldAsync(cancellation.Token);
    try
    {
        var completed = await Task.WhenAny(serviceTask, profileTask, tapHoldTask);
        if (completed != serviceTask && completed.IsFaulted)
        {
            cancellation.Cancel();
            await completed;
        }

        await serviceTask;
    }
    finally
    {
        cancellation.Cancel();
        try
        {
            await profileTask;
        }
        catch (OperationCanceledException)
        {
        }

        try
        {
            await tapHoldTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    return 0;
}

static int ShowHelp()
{
    Console.WriteLine("ACK05 Enhanced Probe");
    Console.WriteLine("  list                              list present ACK05 HID collections");
    Console.WriteLine("  decode <hex bytes>                decode a captured raw report");
    Console.WriteLine("  validate-config <json path>       validate config without device/output access");
    Console.WriteLine("  foreground                        show current foreground process name");
    Console.WriteLine("  monitor                           read reports without device writes");
    Console.WriteLine("  monitor --index <n>               read one listed collection without writes");
    Console.WriteLine("  monitor --enable-bitmask          send known raw-mode command, then read");
    Console.WriteLine("  watch                             reconnecting read-only vendor monitor");
    Console.WriteLine("  watch --enable-bitmask            reconnect and re-enable known raw mode");
    Console.WriteLine("  run --config <path> --enable-bitmask --allow-sendinput [--allow-target-launch]");
    Console.WriteLine("                                    run one profile; target launch is separately opt-in");
    Console.WriteLine("  raw-input                         observe ACK05 Windows Raw Input events");
    Console.WriteLine("  raw-input --list                  list Windows Raw Input device paths");
    Console.WriteLine("  raw-input --diagnostic-sources    also identify non-ACK05 event sources");
    return 0;
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    return 2;
}
