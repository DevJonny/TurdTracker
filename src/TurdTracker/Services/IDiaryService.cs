using TurdTracker.Models;

namespace TurdTracker.Services;

public interface IDiaryService
{
    Task<List<DiaryEntry>> GetAllAsync();
    Task<List<DiaryEntry>> GetAllIncludingDeletedAsync();
    Task<DiaryEntry?> GetByIdAsync(Guid id);
    Task<List<DiaryEntry>> GetByDateAsync(DateTime date);
    Task AddAsync(DiaryEntry entry);
    Task UpdateAsync(DiaryEntry entry);
    Task DeleteAsync(Guid id);
    Task ReplaceAllAsync(List<DiaryEntry> entries);
    event Action? OnDataChanged;
}
