using TurdTracker.Models;

namespace TurdTracker.Services;

public interface IDiaryService
{
    Task<List<DiaryEntry>> GetAllAsync();
    Task<DiaryEntry?> GetByIdAsync(Guid id);
    Task<List<DiaryEntry>> GetByDateAsync(DateTime date);
    Task AddAsync(DiaryEntry entry);
    Task UpdateAsync(DiaryEntry entry);
    Task DeleteAsync(Guid id);
}
