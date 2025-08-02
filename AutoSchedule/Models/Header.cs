namespace AutoSchedule.Models;

public class Header
{
    public int DayNum { get; set; }
        
    public DayOfWeek Dow { get; set; }
        
    public string? Start { get; set; }
        
    public string? End { get; set; }

    public string Location { get; set; } = string.Empty;
}