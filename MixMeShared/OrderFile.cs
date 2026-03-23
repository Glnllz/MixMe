using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MixMeShared
{
    public class OrderFile
    {
        public int Id { get; set; }
        public Guid OrderId { get; set; }
        public Guid UploaderId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty; // "stem", "mix", "reference"
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
