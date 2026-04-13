namespace MixMeShared;

public class EngineerUpdateDto
{
    public string? Bio { get; set; }
    public int ExperienceYears { get; set; }

    // Список ID жанров, которые выбрал инженер
    public List<int> GenreIds { get; set; } = new();
}