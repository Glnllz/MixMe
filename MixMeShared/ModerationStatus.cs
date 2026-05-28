using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MixMeShared;

public enum ModerationStatus
{
    Pending = 0,    // На проверке
    Approved = 1,   // Одобрено (видимо всем)
    Rejected = 2    // Отклонено
}
