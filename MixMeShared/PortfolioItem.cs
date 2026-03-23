using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MixMeShared
{
    public class PortfolioItem
    {
        public int Id { get; set; }
        public Guid EngineerId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? AudioBeforeUrl { get; set; } // Ссылка на облако (S3)
        public string? AudioAfterUrl { get; set; }
    }
}
