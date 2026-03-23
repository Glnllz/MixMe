using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MixMeShared
{
    public class Message
    {
        public int Id { get; set; }
        public Guid OrderId { get; set; } // Чат привязан к заказу
        public Guid SenderId { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }
}
