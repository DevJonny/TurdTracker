using Blazored.LocalStorage;

namespace TurdTracker.Tests.Fakes;

public class FakeLocalStorageService : ILocalStorageService
{
    private readonly Dictionary<string, object> _store = new();

#pragma warning disable CS0067 // Events required by interface but not used in fake
    public event EventHandler<ChangingEventArgs>? Changing;
    public event EventHandler<ChangedEventArgs>? Changed;
#pragma warning restore CS0067

    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        _store.Clear();
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> ContainKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(_store.ContainsKey(key));
    }

    public ValueTask<T?> GetItemAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var value))
        {
            return ValueTask.FromResult((T?)value);
        }
        return ValueTask.FromResult(default(T));
    }

    public ValueTask<string?> GetItemAsStringAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var value))
        {
            return ValueTask.FromResult(value.ToString());
        }
        return ValueTask.FromResult(default(string));
    }

    public ValueTask<string?> KeyAsync(int index, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<string?>(_store.Keys.ElementAt(index));
    }

    public ValueTask<IEnumerable<string>> KeysAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<IEnumerable<string>>(_store.Keys);
    }

    public ValueTask<int> LengthAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(_store.Count);
    }

    public ValueTask RemoveItemAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.Remove(key);
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveItemsAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
            _store.Remove(key);
        return ValueTask.CompletedTask;
    }

    public ValueTask SetItemAsync<T>(string key, T data, CancellationToken cancellationToken = default)
    {
        _store[key] = data!;
        return ValueTask.CompletedTask;
    }

    public ValueTask SetItemAsStringAsync(string key, string data, CancellationToken cancellationToken = default)
    {
        _store[key] = data;
        return ValueTask.CompletedTask;
    }
}
