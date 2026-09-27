namespace App01.Shared.Application.Interfaces;

public interface ICacheDataService
{
    void Set(string key, string value);
    string? Get(string key);
    bool TryGet(string key, out string? value);
    void Remove(string key);
    bool Contains(string key);
}