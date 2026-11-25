using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Ack05Enhanced.RawProbe;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Directory.CreateDirectory("captures/private");
        using var log = new StreamWriter($"captures/private/p09-{DateTime.Now:yyyyMMdd-HHmmss-fff}.jsonl") { AutoFlush = true };
        // Optional one-based stage numbers allow isolated repetitions of missed stimuli.
        using var form = new Trial(log, args.Select(int.Parse).ToArray());
        Application.Run(form);
    }
}

internal sealed class Trial : Form
{
    private static readonly string[] Protocol = [
        "Teclado · Ctrl+O", "Teclado · Ctrl+N", "Teclado · Ctrl+S", "Teclado · Ctrl+Z",
        "Teclado · + numérico", "Teclado · − numérico",
        .. Enumerable.Range(1, 10).Select(i => $"ACK05 · K{i}"),
        "ACK05 · um passo horário", "ACK05 · um passo anti-horário", "ACK05 · K11 breve",
        "Repetir teclado · Ctrl+O", "Repetir teclado · Ctrl+S"
    ];
    private readonly string[] steps;
    private readonly StreamWriter log;
    private readonly Label prompt = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
    private readonly System.Windows.Forms.Timer clock = new() { Interval = 6000 };
    private readonly DeviceCatalog catalog = new();
    private readonly Dictionary<nint, Source> devices = new();
    private readonly PacketDecoder decoder = new();
    private int step = -1;
    private bool capturing;

    internal Trial(StreamWriter log, int[] selected)
    {
        this.log = log;
        steps = selected.Length == 0 ? Protocol : selected.Select(i =>
            i >= 1 && i <= Protocol.Length ? Protocol[i - 1] : throw new ArgumentOutOfRangeException(nameof(selected))).ToArray();
        Text = "ACK05 · Diagnóstico Raw Input";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(680, 240);
        Font = new Font(SystemFonts.MessageBoxFont!.FontFamily, 16);
        Controls.Add(prompt);
        PreviewNext();
        prompt.MouseClick += (_, e) => { if (!capturing && e.Button == MouseButtons.Left) Start(); };
        clock.Tick += (_, _) => FinishStage();
        Shown += (_, _) =>
        {
            Register(1, 6); Register(1, 2); Register(13, 2);
            catalog.Refresh();
            foreach (var item in Inventory()) Identify(item.Handle, item.Type);
            Log("ready", null);
        };
        Deactivate += (_, _) =>
        {
            if (!capturing) return;
            clock.Stop();
            capturing = false;
            Log("interrupted", null);
            decoder.Clear();
            step--;
            prompt.Text = "Foco perdido. Clique aqui para repetir a etapa interrompida.";
        };
        FormClosed += (_, _) => { clock.Dispose(); Log("closed", null); };
    }

    private void Start()
    {
        if (step + 1 >= steps.Length) { Close(); return; }
        capturing = true;
        step++;
        prompt.Text = $"Agora · {step+1} / {steps.Length}\n\n{steps[step]}\nAcione uma vez e solte.";
        Log("stage", null);
        clock.Start();
    }

    private void PreviewNext()
    {
        prompt.Text = $"Próximo · {steps[step+1]}\n\nClique aqui quando estiver pronto.\nApós o clique, você terá seis segundos.";
    }

    private void FinishStage()
    {
        clock.Stop();
        capturing = false;
        Log("stage-end", null);
        decoder.Clear();
        if (step + 1 == steps.Length)
        {
            Log("completed", null);
            prompt.Text = "Captura concluída. Clique aqui para fechar.";
            return;
        }
        PreviewNext();
    }

    private void Log(string kind, object? data) => log.WriteLine(JsonSerializer.Serialize(new {
        time = DateTimeOffset.Now, kind, stage = step >= 0 && step < steps.Length ? steps[step] : null, data }));

    private Source Identify(nint handle, uint type)
    {
        var source = catalog.Resolve(handle, type);
        devices[handle] = source;
        Log("device", source);
        return source;
    }

    protected override void WndProc(ref Message m)
    {
        try
        {
            if (m.Msg == 0x00ff && capturing && GetForegroundWindow() == Handle) Record(m.LParam);
            if (m.Msg == 0x00fe)
            {
                var h = m.LParam;
                var arrival = m.WParam == 1;
                devices.Remove(h, out var old);
                decoder.Forget(h);
                Log(arrival ? "arrival" : "removal", new { handle = $"0x{h:X}", old });
                if (arrival)
                {
                    catalog.Refresh();
                    foreach (var item in Inventory().Where(x => x.Handle == h)) Identify(h, item.Type);
                }
            }
        }
        catch (Exception e) { Log("error", e.Message); }
        base.WndProc(ref m);
    }

    private unsafe void Record(nint input)
    {
        uint size = 0;
        var headerSize = (uint)sizeof(Header);
        if (GetRawInputData(input, 0x10000003, null, ref size, headerSize) != 0 || size < headerSize)
            throw new Win32Exception();
        var bytes = new byte[size];
        fixed (byte* p = bytes)
        {
            if (GetRawInputData(input, 0x10000003, p, ref size, headerSize) != bytes.Length)
                throw new Win32Exception();
            var header = *(Header*)p;
            if (header.Size < headerSize || header.Size > bytes.Length) throw new InvalidDataException("RAWINPUT dwSize");
            if (!devices.TryGetValue(header.Handle, out var source)) source = Identify(header.Handle, header.Type);
            Log("input", new { source, packet = decoder.Decode(header.Type, header.Handle,
                bytes.AsSpan((int)headerSize, (int)(header.Size - headerSize))) });
        }
    }

    private void Register(ushort page, ushort usage)
    {
        Registration[] request = [new(page, usage, 0x2000, Handle)]; // DEVNOTIFY, foreground only
        bool ok = RegisterRawInputDevices(request, 1, (uint)Marshal.SizeOf<Registration>());
        Log("registration", new { page, usage, ok, error = ok ? 0 : Marshal.GetLastWin32Error() });
        if (!ok && page == 1) throw new Win32Exception();
    }

    private static unsafe Entry[] Inventory()
    {
        uint count = 0;
        var stride = (uint)sizeof(Entry);
        for (int retry = 0; retry < 3; retry++)
        {
            if (GetRawInputDeviceList(null, ref count, stride) != 0) throw new Win32Exception();
            if (count == 0) return [];
            var entries = new Entry[count];
            fixed (Entry* p = entries)
            {
                var actual = GetRawInputDeviceList(p, ref count, stride);
                if (actual != uint.MaxValue) return entries[..(int)actual];
                if (Marshal.GetLastWin32Error() != 122) throw new Win32Exception();
            }
        }
        throw new IOException("Raw Input inventory changed during enumeration.");
    }

    [StructLayout(LayoutKind.Sequential)] private record struct Registration(ushort Page, ushort Usage, uint Flags, nint Window);
    [StructLayout(LayoutKind.Sequential)] private struct Entry { public nint Handle; public uint Type; }
    [StructLayout(LayoutKind.Sequential)] private struct Header { public uint Type, Size; public nint Handle, Param; }
    [DllImport("user32")] private static extern nint GetForegroundWindow();
    [DllImport("user32", SetLastError=true)] private static extern bool RegisterRawInputDevices(Registration[] requests, uint count, uint size);
    [DllImport("user32", SetLastError=true)] private static extern unsafe uint GetRawInputDeviceList(Entry* entries, ref uint count, uint size);
    [DllImport("user32", SetLastError=true)] private static extern unsafe uint GetRawInputData(nint input, uint command, byte* data, ref uint size, uint headerSize);
}
