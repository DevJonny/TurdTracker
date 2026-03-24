namespace TurdTracker.Models;

public class DiaryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public int BristolType { get; set; }
    public string Notes { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
}
