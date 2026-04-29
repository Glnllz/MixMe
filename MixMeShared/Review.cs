using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MixMeShared
{
    public class Review
    {
        public int Id { get; set; }
        public Guid OrderId { get; set; }
        public Guid ReviewerId { get; set; } // Кто оставил
        public Guid RevieweeId { get; set; } // Кому (инженеру)
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? ReviewerName { get; set; }
    }
}
