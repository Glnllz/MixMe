using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MixMeShared
{
    public class Post
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? ImageUrl { get; set; } // Ссылка на картинку
        public string? AudioUrl { get; set; } // Ссылка на трек
        public int LikeCount { get; set; }    // Количество лайков
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
