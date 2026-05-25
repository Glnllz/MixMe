using System;

namespace MixMeShared
{
    public class EngineerStatsDto
    {
        public int TotalProjects { get; set; }
        public double AvgRating { get; set; }
        public int ExperienceYears { get; set; }

        // Список доходов по месяцам (для отчетов)
        public List<MonthlyIncomeDto> IncomeHistory { get; set; } = new();
    }

    public class MonthlyIncomeDto
    {
        public string MonthName { get; set; } = ""; // Напр: "Май 2026"
        public decimal Amount { get; set; }
        public int Count { get; set; } // Сколько заказов было в этом месяце
    }
}