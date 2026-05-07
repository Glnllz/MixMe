using System;
using System.ComponentModel.DataAnnotations;

namespace MixMeShared;
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid UserId { get; set; } // Кому предназначено уведомление

        [Required]
        public string Text { get; set; } = string.Empty;

        public string? Link { get; set; } // Ссылка, куда перейти (например, "/order/guid")

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
