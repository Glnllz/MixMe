using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MixMeShared;

namespace MixMeShared;
public class Engineer
{
    [Key, ForeignKey("Profile")] public Guid ProfileId { get; set; }
    public UserProfile? Profile { get; set; }
    public string? Bio { get; set; }
    public List<Genre> Genres { get; set; } = new();
    public int ExperienceYears { get; set; }
    public double AvgRating { get; set; }
}