using System.ComponentModel.DataAnnotations;

namespace MixMeShared;

public class Complaint
{
    public int Id { get; set; }
    public Guid SenderId { get; set; } // Кто жалуется
    public Guid TargetUserId { get; set; } // На кого жалуются
    public Guid? OrderId { get; set; } // Связанный заказ (если есть)

    [Required(ErrorMessage = "Выберите причину жалобы")]
    public string Reason { get; set; } = string.Empty;

    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsResolved { get; set; } = false; // Рассмотрел ли админ
}