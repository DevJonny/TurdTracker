using TurdTracker.Models;
using TurdTracker.Services;

namespace TurdTracker.Tests.Fakes;

public class FakeDiaryService : IDiaryService
{
    private readonly List<DiaryEntry> _entries = [];
    public List<string> MethodCalls { get; } = [];

    public event Action? OnDataChanged;

    public Task<List<DiaryEntry>> GetAllAsync()
    {
        MethodCalls.Add(nameof(GetAllAsync));
        return Task.FromResult(_entries.Where(e => !e.IsDeleted).ToList());
    }

    public Task<List<DiaryEntry>> GetAllIncludingDeletedAsync()
    {
        MethodCalls.Add(nameof(GetAllIncludingDeletedAsync));
        return Task.FromResult(_entries.ToList());
    }

    public Task<DiaryEntry?> GetByIdAsync(Guid id)
    {
        MethodCalls.Add(nameof(GetByIdAsync));
        return Task.FromResult(_entries.Where(e => !e.IsDeleted).FirstOrDefault(e => e.Id == id));
    }

    public Task<List<DiaryEntry>> GetByDateAsync(DateTime date)
    {
        MethodCalls.Add(nameof(GetByDateAsync));
        return Task.FromResult(_entries.Where(e => !e.IsDeleted && e.Timestamp.Date == date.Date).ToList());
    }

    public Task AddAsync(DiaryEntry entry)
    {
        MethodCalls.Add(nameof(AddAsync));
        entry.LastModified = DateTime.UtcNow;
        _entries.Add(entry);
        OnDataChanged?.Invoke();
        return Task.CompletedTask;
    }

    public Task UpdateAsync(DiaryEntry entry)
    {
        MethodCalls.Add(nameof(UpdateAsync));
        var index = _entries.FindIndex(e => e.Id == entry.Id);
        if (index >= 0)
        {
            entry.LastModified = DateTime.UtcNow;
            _entries[index] = entry;
            OnDataChanged?.Invoke();
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        MethodCalls.Add(nameof(DeleteAsync));
        var entry = _entries.FirstOrDefault(e => e.Id == id);
        if (entry != null)
        {
            entry.IsDeleted = true;
            entry.LastModified = DateTime.UtcNow;
            OnDataChanged?.Invoke();
        }
        return Task.CompletedTask;
    }

    public Task ReplaceAllAsync(List<DiaryEntry> entries)
    {
        MethodCalls.Add(nameof(ReplaceAllAsync));
        _entries.Clear();
        _entries.AddRange(entries);
        return Task.CompletedTask;
    }

    /// <summary>Seeds entries directly without triggering events or recording calls.</summary>
    public void SeedEntries(params DiaryEntry[] entries)
    {
        _entries.AddRange(entries);
    }

    /// <summary>Fires OnDataChanged for testing subscriber behavior.</summary>
    public void RaiseOnDataChanged() => OnDataChanged?.Invoke();
}
