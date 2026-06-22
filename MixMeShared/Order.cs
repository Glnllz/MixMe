using System.ComponentModel.DataAnnotations;

public class Order
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public Guid EngineerId { get; set; }
    public int ServiceId { get; set; }

    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Created"; 

    // ЮKassa 
    public string? PaymentId { get; set; } 
    public string? PaymentStatus { get; set; } 
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
}