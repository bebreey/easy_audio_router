using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AudioRouter.Core.Diagnostics;
using AudioRouter.Core.Models;

namespace AudioRouter.Core.Routing;

/// <summary>一条持久化的路由记录。身份 = <see cref="Key"/>（exe 路径）+ 设备。</summary>
public sealed class RouteRecord
{
    /// <summary>身份键（见 <see cref="RouteKey"/>）。加载旧的按 PID 存的记录时会被补齐。</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>可执行文件路径（可读、可再次匹配）。</summary>
    public string ExePath { get; set; } = string.Empty;

    /// <summary>进程名（路径拿不到时的兜底身份）。</summary>
    public string ProcessName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string DeviceId { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public RouteMode Mode { get; set; } = RouteMode.Route;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>最近一次匹配到的 PID —— 仅供诊断，不参与身份判定。</summary>
    public int LastPid { get; set; }

    /// <summary>旧版本（按 PID 存）留下的记录：无法按路径匹配。</summary>
    [JsonIgnore]
    public bool IsLegacy => string.IsNullOrEmpty(Key);

    public string DisplayKey => !string.IsNullOrEmpty(ExePath)
        ? ExePath
        : RouteKey.Describe(Key);
}

/// <summary>
/// 路由记录仓库（按身份键 + 设备索引，落盘持久化）。
///
/// 注意：CLI 每次调用都是独立进程，GUI 与 CLI 共用同一份表 ——
/// 所以这里必须是真持久化，"进程内存状态"在这个项目里不算状态。
/// </summary>
public sealed class RouteStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly List<RouteRecord> _routes = new();
    private readonly string _path;
    private readonly bool _persist;

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AudioRouter", "routes.json");

    public RouteStore(string? path = null, bool persist = true)
    {
        _path = path ?? DefaultPath;
        _persist = persist;
    }

    /// <summary>只在内存里保存（给不需要跨进程共享的宿主；GUI 现在用持久化那份）。</summary>
    public static RouteStore InMemory() => new(path: null, persist: false);

    public string FilePath => _path;

    public bool Persists => _persist;

    public IReadOnlyList<RouteRecord> All => _routes;

    public static RouteStore Load(string? path = null, bool persist = true)
    {
        var store = new RouteStore(path, persist);
        if (!persist) return store;

        // 1) 正常读取
        if (TryRead(store._path, out var records, out _))
        {
            store._routes.AddRange(records);
            return store;
        }

        var primaryExists = File.Exists(store._path);

        if (!primaryExists)
        {
            // 主文件不在（被删/首次运行）：若上一代备份还在，能捞就捞
            if (TryRead(store._path + ".bak", out var fromBak, out _))
            {
                store._routes.AddRange(fromBak);
                StartupLog.Write($"routes: {store._path} 不存在，已从 .bak 恢复 {fromBak.Count} 条");
            }

            return store;
        }

        // 2) 主文件存在但读不懂：先留下现场（可能被外部工具改坏，也可能只是写到一半）
        BackupCorrupt(store._path);

        // 3) 再用上一代好文件恢复 —— 这才是真正把记录救回来的那一步
        if (TryRead(store._path + ".bak", out var recovered, out _))
        {
            store._routes.AddRange(recovered);
            StartupLog.Write($"routes: 主文件损坏，已从 .bak 恢复 {recovered.Count} 条");
        }

        return store;
    }

    /// <summary>读取并解析；文件不存在或内容不可解析都算失败（失败时不留半截状态）。</summary>
    private static bool TryRead(string path, out List<RouteRecord> records, out string error)
    {
        records = new List<RouteRecord>();
        error = string.Empty;

        try
        {
            if (!File.Exists(path))
            {
                error = "文件不存在";
                return false;
            }

            var loaded = JsonSerializer.Deserialize<List<RouteRecord>>(File.ReadAllText(path), Options);
            if (loaded is null)
            {
                error = "内容为 null";
                return false;
            }

            foreach (var record in loaded)
            {
                // 兼容旧格式：按 PID 存、没有 Key 的记录，用能拿到的信息补出身份键
                if (record.IsLegacy)
                {
                    StartupLog.Write(
                        $"routes: 旧记录无身份键，标记为 legacy（exe='{record.ExePath}' name='{record.ProcessName}'）");
                }
                else
                {
                    record.Key = RouteKey.Normalize(record.Key);
                }
            }

            records = loaded;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>把读不懂的主文件另存一份，便于事后查看/手工抢救（不覆盖任何好文件）。</summary>
    private static void BackupCorrupt(string path)
    {
        try
        {
            var backup = Path.Combine(
                Path.GetDirectoryName(path)!,
                $"routes.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json");

            File.Copy(path, backup, overwrite: true);
            StartupLog.Write($"routes: {path} 解析失败，已留下现场 {backup}");
        }
        catch (Exception ex)
        {
            StartupLog.Write($"routes: {path} 解析失败，且留存现场也失败：{ex.Message}");
        }
    }

    public bool Exists(string key, string deviceId)
        => _routes.Any(r => SameKey(r.Key, key) && SameId(r.DeviceId, deviceId));

    public IReadOnlyList<RouteRecord> Find(string key)
        => _routes.Where(r => SameKey(r.Key, key)).ToList();

    /// <summary>把「这个应用在这台设备上」的记录补出来（幂等）。</summary>
    public bool Add(RouteRecord record)
    {
        if (string.IsNullOrEmpty(record.Key))
        {
            record.Key = RouteKey.For(record.ExePath, record.ProcessName);
        }

        record.Key = RouteKey.Normalize(record.Key);

        if (Exists(record.Key, record.DeviceId)) return false;

        _routes.Add(record);
        Save();
        return true;
    }

    public bool Remove(string key, string deviceId)
        => RemoveAll(r => SameKey(r.Key, key) && SameId(r.DeviceId, deviceId)) > 0;

    public bool RemoveRecord(RouteRecord record)
        => RemoveAll(r => ReferenceEquals(r, record) ||
                          (SameKey(r.Key, record.Key) && SameId(r.DeviceId, record.DeviceId))) > 0;

    public int CountFor(string key) => _routes.Count(r => SameKey(r.Key, key));

    private int RemoveAll(Func<RouteRecord, bool> predicate)
    {
        var removed = _routes.RemoveAll(r => predicate(r));
        if (removed > 0) Save();
        return removed;
    }

    public void Save()
    {
        if (!_persist) return;

        // 原子落盘：先写临时文件，再整体替换。
        // 直接 File.WriteAllText 会先截断原文件，写到一半被强杀就留下半截 json；
        // 而半截 json 会让下次读取失败 —— 读取失败又会被当成"没有路由"，数据就这么没了。
        var temp = _path + ".tmp";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

            // 留一代**有效**的上一版：主文件被外部改坏/截断时，这是唯一能把记录救回来的东西。
            // 只备份能解析的文件 —— 否则会把损坏内容覆盖到好备份上（这条踩过）。
            if (File.Exists(_path) && TryRead(_path, out _, out _))
            {
                try
                {
                    File.Copy(_path, _path + ".bak", overwrite: true);
                }
                catch (Exception ex)
                {
                    StartupLog.Write($"routes: 备份上一代失败（继续保存）：{ex.Message}");
                }
            }

            File.WriteAllText(temp, JsonSerializer.Serialize(_routes, Options));
            File.Move(temp, _path, overwrite: true);
        }
        catch (Exception ex)
        {
            StartupLog.Write($"routes: 写入 {_path} 失败：{ex.Message}");

            try
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
            catch
            {
                // 临时文件清理失败不影响主流程
            }
        }
    }

    /// <summary>
    /// 比较身份键时先做归一化：调用方传原始路径（大小写/斜杠不同）也必须命中，
    /// 否则"按路径存"的语义会变成"按路径字符串精确相等"，非常容易踩。
    /// </summary>
    private static bool SameKey(string? a, string? b)
        => string.Equals(RouteKey.Normalize(a ?? string.Empty),
                         RouteKey.Normalize(b ?? string.Empty),
                         StringComparison.Ordinal);

    private static bool SameId(string? a, string? b)
        => string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.OrdinalIgnoreCase);
}
