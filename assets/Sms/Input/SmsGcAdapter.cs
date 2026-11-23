using Godot;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

/// <summary>
/// SMS 253 (Jaysonn: "i wanna be able to use mayflash so my gamecube controller work on my godot game. rivals of aether 2 does this. i want it on
/// mine so we have proper trigger work"): the GameCube controller adapter (Nintendo's Wii U one and the Mayflash in its Wii U / Switch mode, USB
/// 057E:0337) read DIRECTLY over USB, as Dolphin and Rivals of Aether 2 do -- in that mode Windows shows no gamepad at all, so no game sees it
/// through the usual joypad layer; in the Mayflash's PC mode it is a plain HID pad with flattened triggers. Here the adapter is opened through the
/// WinUSB driver (the one Zadig installs, the same driver Dolphin asks for), told to start polling (0x13), and its 37-byte reports are read on a
/// thread at the adapter's 125 Hz: for each of the four ports a status byte (0x10 wired, 0x20 a WaveBird), two button bytes (A B X Y; D-pad;
/// Start Z R L -- R and L here are the CLICKS at the end of the triggers' travel), the main stick, the C stick and the two analog triggers
/// (0..255 raw). Each port becomes a Godot joypad of its own (device FirstDevice + port) through Input.ParseInputEvent, so the project's Input
/// Map drives it like any pad: A B X Y -> buttons 0-3, Start -> 6, Z -> 10 (the right shoulder, his button_z), L click -> 9 (the left shoulder,
/// his button_l), the D-pad -> 11-14, the main stick -> axes 0 / 1, the C stick -> 2 / 3, the L and R analog pulls -> axes 4 / 5 (his button_l /
/// button_r take them as strengths: Mario's _fluddR is the R pull, the game's mAnalogR), the R CLICK -> button 17 (paddle 2) through a
/// `button_rclick` action this node adds to the map when the project has none (Mario.SmsRClick reads it: the stand-and-aim spray). The sticks are
/// CALIBRATED on the first report of a controller (its rest position is the origin, as the console does at power-on; Recalibrate() redoes it with
/// the sticks at rest), scaled by StickRange; the triggers by their rest and TriggerFull. Events go out in _PhysicsProcess (autoloads run before
/// the scene's nodes, so Mario reads the same tick's values; InputEventsInProcess sends them from _Process instead).
/// SETUP (once, Windows): the adapter's switch on "Wii U" / "Switch"; install the WinUSB driver on it with Zadig (zadig.akeo.ie: Options > List All
/// Devices, pick "WUP-028" / the 057E:0337 device, driver WinUSB, Install) -- the same step Dolphin and RoA2 players do. With the driver missing the
/// adapter is found but cannot be opened; the node says so once in the log (DriverHint). Windows only (the P/Invokes); elsewhere the node is idle.
/// Rumble: Rumble(port, on) writes the adapter's 0x11 command (RumbleAllowed).
/// Knobs: Enabled, FirstDevice (20), StickRange (100 raw counts = full tilt; the sticks reach 80-105), StickDeadzone (raw counts about the origin),
/// TriggerRest (auto from the first report) / TriggerFull (the raw pull that counts as 1.0; the click comes a little past it), AddRClickAction,
/// RescanSeconds, DebugPrint (a line per second per wired port).
/// </summary>
public partial class SmsGcAdapter : Node
{
    public static SmsGcAdapter Instance { get; private set; }

    [Export] public bool Enabled = true;
    /// <summary>The Godot joypad device id of port 1 (ports 2-4 follow). Godot's own pads are 0-15.</summary>
    [Export(PropertyHint.Range, "16,60,1")] public int FirstDevice = 20;
    /// <summary>Raw stick counts from the origin that read as a full tilt (the GameCube stick reaches about 80 straight, 105 at a notch).</summary>
    [Export(PropertyHint.Range, "60,127,1")] public float StickRange = 100f;
    /// <summary>Raw counts about the origin that read as centred (the console's own dead zone is about 10 of 128).</summary>
    [Export(PropertyHint.Range, "0,30,1")] public float StickDeadzone = 6f;
    /// <summary>The raw trigger pull (over its rest) that reads as 1.0 -- the click comes a little past it on a stock controller (the full travel is about 200 raw).</summary>
    [Export(PropertyHint.Range, "60,230,1")] public float TriggerFull = 170f;
    /// <summary>Add a `button_rclick` action (the R trigger's click: joypad button 17, paddle 2) when the project's map has none, so Mario's SmsRClick sees the real click.</summary>
    [Export] public bool AddRClickAction = true;
    /// <summary>Send the joypad events from _Process instead of _PhysicsProcess.</summary>
    [Export] public bool InputEventsInProcess = false;
    /// <summary>How often to look for the adapter while none is open.</summary>
    [Export(PropertyHint.Range, "0.5,10,0.5")] public float RescanSeconds = 2f;
    [Export] public bool RumbleAllowed = true;
    [Export] public bool DebugPrint = false;

    public const string BuildMarker = "SMS253_GC_ADAPTER_B";
    public const int Ports = 4;
    public const ushort Vid = 0x057E, Pid = 0x0337;
    private const byte EpIn = 0x81, EpOut = 0x02;
    private const int ReportSize = 37;

    /// <summary>One port's last report, decoded.</summary>
    public struct PortState
    {
        public bool Wired, Wireless;
        public byte Buttons1, Buttons2;                   // A B X Y Dl Dr Dd Du / Start Z R L
        public byte StickX, StickY, CStickX, CStickY, TriggerL, TriggerR;
        public bool Connected => Wired || Wireless;
        public bool A => (Buttons1 & 0x01) != 0; public bool B => (Buttons1 & 0x02) != 0; public bool X => (Buttons1 & 0x04) != 0; public bool Y => (Buttons1 & 0x08) != 0;
        public bool DLeft => (Buttons1 & 0x10) != 0; public bool DRight => (Buttons1 & 0x20) != 0; public bool DDown => (Buttons1 & 0x40) != 0; public bool DUp => (Buttons1 & 0x80) != 0;
        public bool Start => (Buttons2 & 0x01) != 0; public bool Z => (Buttons2 & 0x02) != 0; public bool RClick => (Buttons2 & 0x04) != 0; public bool LClick => (Buttons2 & 0x08) != 0;
    }
    private struct Origin { public bool Set; public float SX, SY, CX, CY, TL, TR; }

    private readonly object _lock = new();
    private readonly PortState[] _latest = new PortState[Ports];
    private readonly PortState[] _sent = new PortState[Ports];
    private readonly Origin[] _origin = new Origin[Ports];
    private readonly bool[] _sentConnected = new bool[Ports];
    private readonly float[] _sentAxis = new float[Ports * 6];
    private volatile bool _run, _open;
    private Thread _thread;
    private IntPtr _usb = IntPtr.Zero;
    private SafeFileHandle _file;
    private double _rescan, _debugT;
    private string _lastError = "";
    private long _reports;
    private bool _driverHinted;

    public bool Open => _open;
    public long Reports => _reports;
    public string LastError => _lastError;
    public PortState State(int port) { lock (_lock) return _latest[Mathf.Clamp(port, 0, Ports - 1)]; }
    public const string DriverHint = "the GameCube adapter (USB 057E:0337) is plugged in but cannot be opened: install the WinUSB driver on it with Zadig (Options > List All Devices, pick the WUP-028 / 057E:0337 device, driver WinUSB, Install) -- the step Dolphin and Rivals of Aether 2 ask for too; then replug";

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        if (AddRClickAction && !InputMap.HasAction("button_rclick"))
        {
            InputMap.AddAction("button_rclick");
            InputMap.ActionAddEvent("button_rclick", new InputEventJoypadButton { Device = -1, ButtonIndex = JoyButton.Paddle2 });
            if (DebugPrint) GD.Print("[SMS gc adapter] button_rclick added to the map (joypad button 17, the R trigger's click)");
        }
        if (!Enabled || Engine.IsEditorHint()) return;
        if (!OperatingSystem.IsWindows()) { _lastError = "not Windows: the adapter reader is idle"; return; }
        TryOpen();
    }

    public override void _ExitTree() { Close(); if (Instance == this) Instance = null; }

    public override void _Process(double delta)
    {
        if (!Enabled || Engine.IsEditorHint()) return;
        if (!_open)
        {
            _rescan += delta;
            if (_rescan >= RescanSeconds && OperatingSystem.IsWindows()) { _rescan = 0; TryOpen(); }
        }
        if (InputEventsInProcess) Pump();
        if (DebugPrint && _open)
        {
            _debugT += delta;
            if (_debugT >= 1.0)
            {
                _debugT = 0;
                for (int p = 0; p < Ports; p++)
                {
                    var s = State(p);
                    if (s.Connected) GD.Print($"[SMS gc adapter] port {p + 1}: A{(s.A ? 1 : 0)} B{(s.B ? 1 : 0)} X{(s.X ? 1 : 0)} Y{(s.Y ? 1 : 0)} Z{(s.Z ? 1 : 0)} St{(s.Start ? 1 : 0)} L{(s.LClick ? 1 : 0)}/{s.TriggerL} R{(s.RClick ? 1 : 0)}/{s.TriggerR} stick {s.StickX},{s.StickY} c {s.CStickX},{s.CStickY} ({_reports} reports)");
                }
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Enabled || Engine.IsEditorHint() || InputEventsInProcess) return;
        Pump();
    }

    // ---- the reports into Godot's input ------------------------------------------------------------------------------------------------------
    private static float Stick(byte raw, float origin, float range, float dead)
    {
        float d = raw - origin;
        if (Mathf.Abs(d) <= dead) return 0f;
        return Mathf.Clamp(d / range, -1f, 1f);
    }
    private static float Trigger(byte raw, float rest, float full) => Mathf.Clamp((raw - rest) / Mathf.Max(1f, full), 0f, 1f);

    /// <summary>The latest reports to Godot: a connection change, each changed button, each moved axis.</summary>
    private void Pump()
    {
        PortState[] now = new PortState[Ports];
        lock (_lock) Array.Copy(_latest, now, Ports);
        for (int p = 0; p < Ports; p++)
        {
            var s = now[p]; var was = _sent[p]; int dev = FirstDevice + p;
            if (s.Connected && !_origin[p].Set)
            {
                _origin[p] = new Origin { Set = true, SX = s.StickX, SY = s.StickY, CX = s.CStickX, CY = s.CStickY, TL = s.TriggerL, TR = s.TriggerR };
                if (DebugPrint) GD.Print($"[SMS gc adapter] port {p + 1} {(s.Wireless ? "WaveBird" : "controller")}: origin stick {s.StickX},{s.StickY} c {s.CStickX},{s.CStickY} triggers {s.TriggerL}/{s.TriggerR}");
            }
            if (s.Connected != _sentConnected[p])
            {
                _sentConnected[p] = s.Connected;
                if (!s.Connected) { _origin[p].Set = false; ReleaseAll(p, was); }
                if (DebugPrint) GD.Print($"[SMS gc adapter] port {p + 1}: {(s.Connected ? "connected" : "gone")} (device {dev})");
            }
            if (!s.Connected) { _sent[p] = s; continue; }
            var o = _origin[p];
            Button(dev, JoyButton.A, was.A, s.A); Button(dev, JoyButton.B, was.B, s.B); Button(dev, JoyButton.X, was.X, s.X); Button(dev, JoyButton.Y, was.Y, s.Y);
            Button(dev, JoyButton.Start, was.Start, s.Start); Button(dev, JoyButton.RightShoulder, was.Z, s.Z);
            Button(dev, JoyButton.LeftShoulder, was.LClick, s.LClick); Button(dev, JoyButton.Paddle2, was.RClick, s.RClick);
            Button(dev, JoyButton.DpadUp, was.DUp, s.DUp); Button(dev, JoyButton.DpadDown, was.DDown, s.DDown); Button(dev, JoyButton.DpadLeft, was.DLeft, s.DLeft); Button(dev, JoyButton.DpadRight, was.DRight, s.DRight);
            Axis(p, dev, JoyAxis.LeftX, Stick(s.StickX, o.SX, StickRange, StickDeadzone));
            Axis(p, dev, JoyAxis.LeftY, -Stick(s.StickY, o.SY, StickRange, StickDeadzone));          // the adapter's Y grows upward; Godot's axis 1 is down-positive
            Axis(p, dev, JoyAxis.RightX, Stick(s.CStickX, o.CX, StickRange, StickDeadzone));
            Axis(p, dev, JoyAxis.RightY, -Stick(s.CStickY, o.CY, StickRange, StickDeadzone));
            Axis(p, dev, JoyAxis.TriggerLeft, Trigger(s.TriggerL, o.TL, TriggerFull));
            Axis(p, dev, JoyAxis.TriggerRight, Trigger(s.TriggerR, o.TR, TriggerFull));
            _sent[p] = s;
        }
        // Godot buffers parsed events until the next frame's flush (use_accumulated_input); flushed here, the actions read the new values in THIS tick,
        // before Mario's _PhysicsProcess (an autoload is the root's first child)
        if (_sentAny) { Input.FlushBufferedEvents(); _sentAny = false; }
    }
    private bool _sentAny;
    private void Button(int dev, JoyButton b, bool was, bool now)
    {
        if (was == now) return;
        Input.ParseInputEvent(new InputEventJoypadButton { Device = dev, ButtonIndex = b, Pressed = now });
        _sentAny = true;
    }
    private void Axis(int port, int dev, JoyAxis a, float v)
    {
        int i = port * 6 + (int)a;
        if (Mathf.Abs(_sentAxis[i] - v) < 0.004f) return;
        _sentAxis[i] = v;
        Input.ParseInputEvent(new InputEventJoypadMotion { Device = dev, Axis = a, AxisValue = v });
        _sentAny = true;
    }
    private void ReleaseAll(int p, PortState was)
    {
        int dev = FirstDevice + p;
        foreach (var b in new[] { JoyButton.A, JoyButton.B, JoyButton.X, JoyButton.Y, JoyButton.Start, JoyButton.RightShoulder, JoyButton.LeftShoulder, JoyButton.Paddle2, JoyButton.DpadUp, JoyButton.DpadDown, JoyButton.DpadLeft, JoyButton.DpadRight })
            Input.ParseInputEvent(new InputEventJoypadButton { Device = dev, ButtonIndex = b, Pressed = false });
        for (int a = 0; a < 6; a++) { _sentAxis[p * 6 + a] = 0f; Input.ParseInputEvent(new InputEventJoypadMotion { Device = dev, Axis = (JoyAxis)a, AxisValue = 0f }); }
        _sentAny = true;
    }

    /// <summary>The sticks' and triggers' rest read again from the next reports (hold nothing).</summary>
    public void Recalibrate() { for (int p = 0; p < Ports; p++) _origin[p].Set = false; }

    // ---- the report ------------------------------------------------------------------------------------------------------------------------------
    /// <summary>A 37-byte report (0x21, then 9 bytes a port) into the four ports' states. Returns false for anything else.</summary>
    public static bool Parse(byte[] r, PortState[] into)
    {
        if (r == null || r.Length < ReportSize || r[0] != 0x21) return false;
        for (int p = 0; p < Ports; p++)
        {
            int i = 1 + p * 9;
            byte status = r[i];
            into[p] = new PortState
            {
                Wired = (status & 0x10) != 0, Wireless = (status & 0x20) != 0,
                Buttons1 = r[i + 1], Buttons2 = r[i + 2],
                StickX = r[i + 3], StickY = r[i + 4], CStickX = r[i + 5], CStickY = r[i + 6], TriggerL = r[i + 7], TriggerR = r[i + 8],
            };
        }
        return true;
    }

    /// <summary>For drives: a report fed in as if the adapter had sent it (no USB needed). The next Pump turns it into events.</summary>
    public bool InjectReport(byte[] report)
    {
        var tmp = new PortState[Ports];
        if (!Parse(report, tmp)) return false;
        lock (_lock) { Array.Copy(tmp, _latest, Ports); _reports++; }
        return true;
    }
    /// <summary>For drives: the decoded state of a port as text.</summary>
    public string Describe(int port)
    {
        var s = State(port);
        if (!s.Connected) return $"port {port + 1}: none";
        return $"port {port + 1}: {(s.Wireless ? "WaveBird" : "wired")} A{(s.A ? 1 : 0)} B{(s.B ? 1 : 0)} X{(s.X ? 1 : 0)} Y{(s.Y ? 1 : 0)} Z{(s.Z ? 1 : 0)} St{(s.Start ? 1 : 0)} Lc{(s.LClick ? 1 : 0)} Rc{(s.RClick ? 1 : 0)} D{(s.DUp ? "U" : "")}{(s.DDown ? "D" : "")}{(s.DLeft ? "L" : "")}{(s.DRight ? "R" : "")} stick {s.StickX},{s.StickY} c {s.CStickX},{s.CStickY} L {s.TriggerL} R {s.TriggerR}";
    }

    // ---- the USB side (WinUSB) ---------------------------------------------------------------------------------------------------------------------
    private void TryOpen()
    {
        if (_open) return;
        var paths = FindDevicePaths();
        if (paths.Count == 0) { _lastError = "no adapter (057E:0337) present"; return; }
        string err = "";
        foreach (string path in paths)
        {
            try
            {
                var file = CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL | FILE_FLAG_OVERLAPPED, IntPtr.Zero);
                if (file.IsInvalid) { err = $"CreateFile failed ({Marshal.GetLastWin32Error()}) on {path}"; continue; }
                if (!WinUsb_Initialize(file, out IntPtr usb)) { err = $"WinUsb_Initialize failed ({Marshal.GetLastWin32Error()}) on {path}"; file.Dispose(); continue; }
                uint timeout = 250; WinUsb_SetPipePolicy(usb, EpIn, PIPE_TRANSFER_TIMEOUT, 4, ref timeout);
                byte[] start = { 0x13 };
                if (!WinUsb_WritePipe(usb, EpOut, start, 1, out _, IntPtr.Zero)) { err = $"the start command failed ({Marshal.GetLastWin32Error()})"; WinUsb_Free(usb); file.Dispose(); continue; }
                _file = file; _usb = usb; _open = true; _run = true; _lastError = "";
                _thread = new Thread(ReadLoop) { IsBackground = true, Name = "SmsGcAdapter" };
                _thread.Start();
                GD.Print($"[SMS gc adapter] open: {path}");
                return;
            }
            catch (Exception ex) { err = ex.Message; }
        }
        Hint(err);
    }
    public const string InUseHint = "the GameCube adapter is plugged in and has its WinUSB driver, but another program has it open -- Delfinovin, Dolphin, Slippi or another adapter tool (WinUSB hands the adapter to one program at a time): close it and the game takes the adapter within a couple of seconds";
    /// <summary>True when the adapter's driver registered its own interface GUID (a WinUSB install: Zadig / libwdi / Dolphin's) -- then a refused open means another program holds it.</summary>
    public bool DriverInstalled { get; private set; }
    private void Hint(string err)
    {
        _lastError = err;
        if (_driverHinted) return;
        _driverHinted = true;
        GD.PushWarning($"[SMS gc adapter] {err}: {(DriverInstalled ? InUseHint : DriverHint)} [{Discovery}]");
    }

    private void ReadLoop()
    {
        var buf = new byte[ReportSize];
        var tmp = new PortState[Ports];
        int failures = 0;
        while (_run)
        {
            bool ok = WinUsb_ReadPipe(_usb, EpIn, buf, (uint)buf.Length, out uint got, IntPtr.Zero);
            if (!ok)
            {
                int e = Marshal.GetLastWin32Error();
                if (e == ERROR_SEM_TIMEOUT || e == ERROR_IO_PENDING) continue;      // nothing in 250 ms: the loop checks _run and reads again
                if (++failures < 5) continue;
                _lastError = $"the read failed ({e}): the adapter is gone";
                CallDeferred(nameof(Lost));
                return;
            }
            failures = 0;
            if (got == ReportSize && Parse(buf, tmp)) lock (_lock) { Array.Copy(tmp, _latest, Ports); _reports++; }
        }
    }
    private void Lost()
    {
        GD.Print($"[SMS gc adapter] {_lastError}");
        Close();
        lock (_lock) for (int p = 0; p < Ports; p++) _latest[p] = default;
    }
    private void Close()
    {
        _run = false;
        if (_usb != IntPtr.Zero) { try { WinUsb_Free(_usb); } catch { } _usb = IntPtr.Zero; }
        _file?.Dispose(); _file = null;
        _open = false;
    }

    /// <summary>The adapter's rumble command (0x11, a byte a port: 1 on, 0 off).</summary>
    public void Rumble(int port, bool on)
    {
        if (!_open || !RumbleAllowed || port < 0 || port >= Ports) return;
        var cmd = new byte[] { 0x11, 0, 0, 0, 0 };
        cmd[1 + port] = (byte)(on ? 1 : 0);
        WinUsb_WritePipe(_usb, EpOut, cmd, 5, out _, IntPtr.Zero);
    }

    // ---- Windows ---------------------------------------------------------------------------------------------------------------------------------
    private static readonly Guid GuidDevInterfaceUsbDevice = new("A5DCBF10-6530-11D2-901F-00C04FB951ED");
    private const uint DIGCF_PRESENT = 0x02, DIGCF_DEVICEINTERFACE = 0x10;
    private const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000, FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2, OPEN_EXISTING = 3, FILE_ATTRIBUTE_NORMAL = 0x80, FILE_FLAG_OVERLAPPED = 0x40000000;
    private const uint PIPE_TRANSFER_TIMEOUT = 0x03;
    private const int ERROR_SEM_TIMEOUT = 121, ERROR_IO_PENDING = 997;

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVICE_INTERFACE_DATA { public uint cbSize; public Guid InterfaceClassGuid; public uint Flags; public IntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA { public uint cbSize; public Guid ClassGuid; public uint DevInst; public IntPtr Reserved; }
    private const uint DICS_FLAG_GLOBAL = 1, DIREG_DEV = 1, KEY_READ = 0x20019;

    /// <summary>The interface paths of the present USB device 057E:0337, the ones its own driver registered first (Zadig's WinUSB install writes a
    /// DeviceInterfaceGUIDs value under the device's Device Parameters; libusb opens through those) and the hub's generic GUID_DEVINTERFACE_USB_DEVICE
    /// path last (a WinUSB-driven device opens through it too on current Windows).</summary>
    /// <summary>What the last discovery saw (for the log and the drives).</summary>
    public string Discovery { get; private set; } = "";
    private System.Collections.Generic.List<string> FindDevicePaths()
    {
        var found = new System.Collections.Generic.List<string>();
        var guidsSeen = new System.Collections.Generic.List<string>();
        string regErr = "";
        string want = $"vid_{Vid:x4}&pid_{Pid:x4}";
        var generic = GuidDevInterfaceUsbDevice;
        IntPtr set = SetupDiGetClassDevs(ref generic, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
        if (set == IntPtr.Zero || set == new IntPtr(-1)) return found;
        var own = new System.Collections.Generic.List<Guid>();
        try
        {
            for (uint i = 0; ; i++)
            {
                var data = new SP_DEVICE_INTERFACE_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>() };
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref generic, i, ref data)) break;
                var info = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
                string path = InterfacePath(set, ref data, ref info);
                if (path == null || !path.ToLowerInvariant().Contains(want)) continue;
                found.Add(path);
                // the device's own interface GUIDs (REG_MULTI_SZ "DeviceInterfaceGUIDs" under its Device Parameters key)
                IntPtr key = SetupDiOpenDevRegKey(set, ref info, DICS_FLAG_GLOBAL, 0, DIREG_DEV, KEY_READ);
                if (key == IntPtr.Zero || key == new IntPtr(-1)) regErr = $"SetupDiOpenDevRegKey failed ({Marshal.GetLastWin32Error()})";
                else
                {
                    try
                    {
                        uint type = 0, size = 0;
                        if (RegQueryValueEx(key, "DeviceInterfaceGUIDs", IntPtr.Zero, ref type, null, ref size) == 0 && size > 0)
                        {
                            var raw = new byte[size];
                            if (RegQueryValueEx(key, "DeviceInterfaceGUIDs", IntPtr.Zero, ref type, raw, ref size) == 0)
                                foreach (string g in System.Text.Encoding.Unicode.GetString(raw).Split('\0', StringSplitOptions.RemoveEmptyEntries))
                                { guidsSeen.Add(g.Trim()); if (Guid.TryParse(g.Trim(), out Guid gg)) own.Add(gg); }
                        }
                        else regErr = $"no DeviceInterfaceGUIDs value ({Marshal.GetLastWin32Error()})";
                    }
                    finally { RegCloseKey(key); }
                }
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        var first = new System.Collections.Generic.List<string>();
        foreach (var g in own)
        {
            var guid = g;
            IntPtr s2 = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (s2 == IntPtr.Zero || s2 == new IntPtr(-1)) continue;
            try
            {
                for (uint i = 0; ; i++)
                {
                    var data = new SP_DEVICE_INTERFACE_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>() };
                    if (!SetupDiEnumDeviceInterfaces(s2, IntPtr.Zero, ref guid, i, ref data)) break;
                    var info = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
                    string path = InterfacePath(s2, ref data, ref info);
                    if (path != null && path.ToLowerInvariant().Contains(want) && !first.Contains(path)) first.Add(path);
                }
            }
            finally { SetupDiDestroyDeviceInfoList(s2); }
        }
        DriverInstalled = own.Count > 0;
        Discovery = $"generic paths {found.Count}; registry GUIDs [{string.Join(", ", guidsSeen)}]{(regErr != "" ? " (" + regErr + ")" : "")}; own interface paths {first.Count}";
        first.AddRange(found);
        return first;
    }
    private static string InterfacePath(IntPtr set, ref SP_DEVICE_INTERFACE_DATA data, ref SP_DEVINFO_DATA info)
    {
        SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out uint need, ref info);
        if (need == 0) return null;
        IntPtr detail = Marshal.AllocHGlobal((int)need);
        try
        {
            Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);       // cbSize of SP_DEVICE_INTERFACE_DETAIL_DATA_W (the documented values)
            if (!SetupDiGetDeviceInterfaceDetail(set, ref data, detail, need, out _, ref info)) return null;
            return Marshal.PtrToStringUni(detail + 4);
        }
        finally { Marshal.FreeHGlobal(detail); }
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)] private static extern bool SetupDiEnumDeviceInterfaces(IntPtr devInfo, IntPtr devInfoData, ref Guid interfaceClassGuid, uint memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr devInfo, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr detail, uint detailSize, out uint requiredSize, ref SP_DEVINFO_DATA deviceInfoData);
    [DllImport("setupapi.dll", SetLastError = true)] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr devInfo);
    [DllImport("setupapi.dll", SetLastError = true)] private static extern IntPtr SetupDiOpenDevRegKey(IntPtr devInfo, ref SP_DEVINFO_DATA devInfoData, uint scope, uint hwProfile, uint keyType, uint samDesired);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] private static extern int RegQueryValueEx(IntPtr key, string name, IntPtr reserved, ref uint type, byte[] data, ref uint size);
    [DllImport("advapi32.dll")] private static extern int RegCloseKey(IntPtr key);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFile(string fileName, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("winusb.dll", SetLastError = true)] private static extern bool WinUsb_Initialize(SafeFileHandle deviceHandle, out IntPtr interfaceHandle);
    [DllImport("winusb.dll", SetLastError = true)] private static extern bool WinUsb_Free(IntPtr interfaceHandle);
    [DllImport("winusb.dll", SetLastError = true)] private static extern bool WinUsb_ReadPipe(IntPtr h, byte pipeId, byte[] buffer, uint length, out uint transferred, IntPtr overlapped);
    [DllImport("winusb.dll", SetLastError = true)] private static extern bool WinUsb_WritePipe(IntPtr h, byte pipeId, byte[] buffer, uint length, out uint transferred, IntPtr overlapped);
    [DllImport("winusb.dll", SetLastError = true)] private static extern bool WinUsb_SetPipePolicy(IntPtr h, byte pipeId, uint policyType, uint valueLength, ref uint value);
}
