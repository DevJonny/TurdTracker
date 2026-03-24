using Blazored.LocalStorage;
using TurdTracker.Models;

namespace TurdTracker.Services;

public class DiaryService : IDiaryService
{
    private const string StorageKey = "diary-entries";
    private readonly ILocalStorageService _localStorage;
    private bool _hasMigrated;

    public event Action? OnDataChanged;

    public DiaryService(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public async Task<List<DiaryEntry>> GetAllAsync()
    {
        var entries = await GetAllRawAsync();
        await BackfillLastModifiedIfNeeded(entries);
        return entries.Where(e => !e.IsDeleted).ToList();
    }

    public async Task<List<DiaryEntry>> GetAllIncludingDeletedAsync()
    {
        var entries = await GetAllRawAsync();
        await BackfillLastModifiedIfNeeded(entries);
        return entries;
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
        entry.LastModified = DateTime.UtcNow;
        var entries = await GetAllRawAsync();
        entries.Add(entry);
        await SaveAsync(entries);
        OnDataChanged?.Invoke();
    }

    public async Task UpdateAsync(DiaryEntry entry)
    {
        entry.LastModified = DateTime.UtcNow;
        var entries = await GetAllRawAsync();
        var index = entries.FindIndex(e => e.Id == entry.Id);
        if (index >= 0)
        {
            entries[index] = entry;
            await SaveAsync(entries);
            OnDataChanged?.Invoke();
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        var entries = await GetAllRawAsync();
        var entry = entries.FirstOrDefault(e => e.Id == id);
        if (entry != null)
        {
            entry.IsDeleted = true;
            entry.LastModified = DateTime.UtcNow;
            await SaveAsync(entries);
            OnDataChanged?.Invoke();
        }
    }

    public async Task ReplaceAllAsync(List<DiaryEntry> entries)
    {
        await SaveAsync(entries);
    }

    private async Task<List<DiaryEntry>> GetAllRawAsync()
    {
        return await _localStorage.GetItemAsync<List<DiaryEntry>>(StorageKey) ?? [];
    }

    private async Task SaveAsync(List<DiaryEntry> entries)
    {
        await _localStorage.SetItemAsync(StorageKey, entries);
    }

    private async Task BackfillLastModifiedIfNeeded(List<DiaryEntry> entries)
    {
        if (_hasMigrated)
            return;

        bool changed = false;
        foreach (var entry in entries)
        {
            if (entry.LastModified == default(DateTime))
            {
                entry.LastModified = entry.Timestamp.ToUniversalTime();
                changed = true;
            }
        }

        if (changed)
        {
            await SaveAsync(entries);
        }

        _hasMigrated = true;
    }
}
