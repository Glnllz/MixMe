using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;


namespace MixMeShared
{
    public class UserProfile
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string Username { get; set; } = string.Empty;

        public string? FullName { get; set; }

        [Required, EmailAddress]
        
        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty; // В дипломном API будем сравнивать хеши
        public int RoleId { get; set; } // Внешний ключ
        
        [System.Text.Json.Serialization.JsonIgnore] // Добавь это!
        public Role? Role { get; set; }
        public string? AvatarUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
