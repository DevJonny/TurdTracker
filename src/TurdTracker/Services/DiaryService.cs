using Blazored.LocalStorage;
using TurdTracker.Models;

namespace TurdTracker.Services;

public class DiaryService : IDiaryService
{
    private const string StorageKey = "diary-entries";
    private readonly ILocalStorageService _localStorage;

    public DiaryService(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public async Task<List<DiaryEntry>> GetAllAsync()
    {
        return await _localStorage.GetItemAsync<List<DiaryEntry>>(StorageKey) ?? [];
    }

    public async Task<DiaryEntry?> GetByIdAsync(Guid id)
    {
        var entries = await GetAllAsync();
        return entries.FirstOrDefault(e => e.Id == id);
    }

    public async Task<List<DiaryEntry>> GetByDateAsync(DateTime date)
    {
        var entries = await GetAllAsync();
        return entries.Where(e => e.Timestamp.Date == date.Date).ToList();
    }

    public async Task AddAsync(DiaryEntry entry)
    {
        var entries = await GetAllAsync();
        entries.Add(entry);
        await _localStorage.SetItemAsync(StorageKey, entries);
    }

    public async Task UpdateAsync(DiaryEntry entry)
    {
        var entries = await GetAllAsync();
        var index = entries.FindIndex(e => e.Id == entry.Id);
        if (index >= 0)
        {
            entries[index] = entry;
            await _localStorage.SetItemAsync(StorageKey, entries);
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        var entries = await GetAllAsync();
        entries.RemoveAll(e => e.Id == id);
        await _localStorage.SetItemAsync(StorageKey, entries);
    }
}
