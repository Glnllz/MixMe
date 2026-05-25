using System.ComponentModel.DataAnnotations;

public class Order
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public Guid EngineerId { get; set; }
    public int ServiceId { get; set; }

    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Created"; // Created, Paid, InProgress, Completed, Disputed, Cancelled

    // ЮKassa нюансы
    public string? PaymentId { get; set; } // ID от ЮKassa
    public string? PaymentStatus { get; set; } // Статус от ЮKassa (pending, waiting_for_capture, succeeded)
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
}