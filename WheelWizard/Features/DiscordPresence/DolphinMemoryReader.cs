using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace WheelWizard.DiscordPresence;

/// <summary>
/// Reads which screen Mario Kart Wii is on out of Dolphin's emulated memory. It only ever reads, so the game and
/// the online server see nothing different. Windows only.
/// </summary>
public sealed class DolphinMemoryReader : IDisposable
{
    private const uint ProcessVmRead = 0x0010;
    private const uint ProcessQueryInformation = 0x0400;
    private const uint MemCommit = 0x1000;
    private const uint MemMapped = 0x40000;
    private const long Mem1Size = 0x2000000;
    private const long Mem2Size = 0x4000000;

    // SectionMgr::sInstance per game region, from rr-pulsar's symbols.txt and versions.txt (K is not listed there).
    private static readonly Dictionary<char, uint> SectionMgrAddress = new()
    {
        ['P'] = 0x809C1E38,
        ['E'] = 0x809BD508,
        ['J'] = 0x809C0E98,
    };

    // RKSYS::Mgr::sInstance per region, translated with the same version table.
    private static readonly Dictionary<char, uint> RksysManagerAddress = new()
    {
        ['P'] = 0x809BD748,
        ['E'] = 0x809B8F88,
        ['J'] = 0x809BC7A8,
    };

    private int _processId;
    private nint _handle;
    private long _mem1;
    private long _mem2;
    private char _region;
    private string _attachError = "";
    private readonly object _gate = new();

    public static bool IsSupported => OperatingSystem.IsWindows();

    /// <summary>The current section id, or null when it cannot be read (not Mario Kart Wii yet, or unsupported).</summary>
    /// <summary>Which of the four save licenses is selected in the game (0 to 3), or null when it cannot be read.</summary>
    public int? ReadLicenseIndex(int processId)
    {
        lock (_gate)
        {
            if (!IsSupported || !Attach(processId))
                return null;

            try
            {
                var save = ReadUInt32(RksysManagerAddress[_region]);
                if (save is null || !IsMainMemory(save.Value))
                    return null;

                // curLicenseId is the u16 at offset 0x36 of the save manager.
                var word = ReadUInt32(save.Value + 0x34);
                return word is { } value && (value & 0xFFFF) <= 3 ? (int)(value & 0xFFFF) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public int? ReadSectionId(int processId)
    {
        lock (_gate)
            return ReadSectionIdUnlocked(processId);
    }

    private int? ReadSectionIdUnlocked(int processId)
    {
        if (!IsSupported)
            return null;

        try
        {
            if (!Attach(processId))
                return null;

            var manager = ReadUInt32(SectionMgrAddress[_region]);
            if (manager is null || !IsMainMemory(manager.Value))
                return null;

            var section = ReadUInt32(manager.Value);
            if (section is null || !IsMainMemory(section.Value))
                return null;

            return (int?)ReadUInt32(section.Value + 0);
        }
        catch (Exception)
        {
            Detach();
            return null;
        }
    }

    private bool Attach(int processId)
    {
        // A stopped or restarted game leaves the remembered locations pointing at nothing, so confirm the game is
        // still there before trusting them; otherwise look for it again.
        if (_handle != 0 && _processId == processId && _mem1 != 0 && _mem2 != 0 && StillMarioKartWii())
            return true;

        Detach();
        _handle = OpenProcess(ProcessVmRead | ProcessQueryInformation, false, processId);
        if (_handle == 0)
        {
            _attachError = "Could not open the Dolphin process. If one of the two programs runs as administrator, run both the same way.";
            return false;
        }
        _processId = processId;

        // MEM1 is a 32 MB mapped region starting with the game id; MEM2 (64 MB) is the next such region after it.
        // The section manager lives in MEM2, so both are needed.
        long address = 0;
        while (VirtualQueryEx(_handle, (nint)address, out var info, (nuint)Marshal.SizeOf<MemoryBasicInformation>()) != 0)
        {
            var baseAddress = (long)info.BaseAddress;
            var size = (long)info.RegionSize;
            if (info.State == MemCommit && info.Type == MemMapped)
            {
                if (_mem1 == 0 && size >= Mem1Size && IsMarioKartWii(baseAddress))
                    _mem1 = baseAddress;
                else if (_mem1 != 0 && size == Mem2Size)
                {
                    _mem2 = baseAddress;
                    return true;
                }
            }

            var next = baseAddress + size;
            if (next <= address)
                break;
            address = next;
        }

        Detach();
        _attachError =
            _mem1 == 0
                ? "Dolphin is open but no Mario Kart Wii game memory was found. Is the game loaded, and is it a supported region (USA, Europe, Japan)?"
                : "Found the game but not its second memory block.";
        return false;
    }

    private bool StillMarioKartWii()
    {
        var id = new byte[4];
        return ReadProcessMemory(_handle, (nint)_mem1, id, 4, out var read)
            && read == 4
            && id[0] == 'R'
            && id[1] == 'M'
            && id[2] == 'C'
            && id[3] == _region;
    }

    /// <summary>A plain-language report of each step, for the Discord settings page.</summary>
    public string Diagnose(int processId)
    {
        lock (_gate)
            return DiagnoseUnlocked(processId);
    }

    private string DiagnoseUnlocked(int processId)
    {
        if (!IsSupported)
            return "Reading the game screen only works on Windows.";

        try
        {
            _attachError = "";
            if (!Attach(processId))
                return _attachError;

            var manager = ReadUInt32(SectionMgrAddress[_region]);
            if (manager is null || !IsMainMemory(manager.Value))
                return $"Game found (region {_region}) but the menu manager is not ready yet.";

            var section = ReadUInt32(manager.Value);
            if (section is null || !IsMainMemory(section.Value))
                return $"Game found (region {_region}) but no screen is loaded yet.";

            return $"Connected. Region {_region}, screen id {ReadUInt32(section.Value)}.";
        }
        catch (Exception exception)
        {
            Detach();
            return $"Could not read the game: {exception.Message}";
        }
    }

    private bool IsMarioKartWii(long baseAddress)
    {
        var id = new byte[4];
        if (!ReadProcessMemory(_handle, (nint)baseAddress, id, 4, out _))
            return false;
        var text = Encoding.ASCII.GetString(id);
        if (text.Length != 4 || !text.StartsWith("RMC", StringComparison.Ordinal) || !SectionMgrAddress.ContainsKey(text[3]))
            return false;
        _region = text[3];
        return true;
    }

    private static bool IsMainMemory(uint address) => address is >= 0x80000000 and < 0x81800000 or >= 0x90000000 and < 0x94000000;

    private uint? ReadUInt32(uint address)
    {
        var host = address switch
        {
            >= 0x80000000 and < 0x81800000 => _mem1 + (address & 0x01FFFFFF),
            >= 0x90000000 and < 0x94000000 => _mem2 + (address & 0x03FFFFFF),
            _ => 0,
        };
        if (host == 0)
            return null;

        var buffer = new byte[4];
        if (!ReadProcessMemory(_handle, (nint)host, buffer, 4, out var read) || read != 4)
            return null;
        return BinaryPrimitives.ReadUInt32BigEndian(buffer);
    }

    private void Detach()
    {
        if (_handle != 0)
            CloseHandle(_handle);
        _handle = 0;
        _mem1 = 0;
        _mem2 = 0;
        _processId = 0;
    }

    public void Dispose() => Detach();

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryBasicInformation
    {
        public nint BaseAddress;
        public nint AllocationBase;
        public uint AllocationProtect;
        public ushort PartitionId;
        public nuint RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint access, bool inherit, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(nint handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nuint VirtualQueryEx(nint process, nint address, out MemoryBasicInformation buffer, nuint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(nint process, nint address, byte[] buffer, nuint size, out nuint read);
}
