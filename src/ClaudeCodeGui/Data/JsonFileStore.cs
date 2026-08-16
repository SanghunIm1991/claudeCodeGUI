using System.Text.Json;

namespace ClaudeCodeGui.Data;

/// <summary>
/// 1レコード=1JSONファイルとして保存する、単一ユーザー向けの簡易ストア。
/// 同時書き込みはコレクションごとのSemaphoreで直列化する。
/// </summary>
public class JsonFileStore<T>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _dir;
    private readonly Func<T, string> _getId;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonFileStore(string dataRoot, string collectionName, Func<T, string> getId)
    {
        _dir = Path.Combine(dataRoot, collectionName);
        _getId = getId;
        Directory.CreateDirectory(_dir);
    }

    private string PathFor(string id) => Path.Combine(_dir, $"{id}.json");

    public async Task<List<T>> GetAllAsync()
    {
        var result = new List<T>();
        foreach (var file in Directory.EnumerateFiles(_dir, "*.json"))
        {
            await using var stream = File.OpenRead(file);
            var item = await JsonSerializer.DeserializeAsync<T>(stream);
            if (item is not null) result.Add(item);
        }
        return result;
    }

    public async Task<T?> GetAsync(string id)
    {
        var path = PathFor(id);
        if (!File.Exists(path)) return default;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream);
    }

    public async Task SaveAsync(T item)
    {
        await _lock.WaitAsync();
        try
        {
            var path = PathFor(_getId(item));
            var tmpPath = path + ".tmp";
            await using (var stream = File.Create(tmpPath))
            {
                await JsonSerializer.SerializeAsync(stream, item, JsonOptions);
            }
            File.Move(tmpPath, path, overwrite: true);
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task DeleteAsync(string id)
    {
        var path = PathFor(id);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}
