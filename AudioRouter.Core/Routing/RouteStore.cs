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

        try
        {
            if (!File.Exists(store._path)) return store;

            var loaded = JsonSerializer.Deserialize<List<RouteRecord>>(File.ReadAllText(store._path), Options);
            if (loaded is null) return store;

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

            store._routes.AddRange(loaded);
        }
        catch (Exception ex)
        {
            StartupLog.Write($"routes: 读取 {store._path} 失败：{ex.Message}");
        }

        return store;
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

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_routes, Options));
        }
        catch (Exception ex)
        {
            StartupLog.Write($"routes: 写入 {_path} 失败：{ex.Message}");
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
