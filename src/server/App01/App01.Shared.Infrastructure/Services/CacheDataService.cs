using System.Collections.Concurrent;

using App01.Shared.Application.Interfaces;

namespace App01.Shared.Infrastructure.Services;

public class CacheDataService : ICacheDataService
{
    private readonly ConcurrentDictionary<string, string> _cache = new();

    public void Set(string key, string value)
        => _cache[key] = value;

    public string? Get(string key)
        => _cache.TryGetValue(key, out var value) ? value : null;

    public bool TryGet(string key, out string? value)
        => _cache.TryGetValue(key, out value);

    public void Remove(string key)
        => _cache.TryRemove(key, out _);

    public bool Contains(string key)
        => _cache.ContainsKey(key);
}