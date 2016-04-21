using System;

namespace FuturesAnalyzer.Models
{
    public class DailyPrice
    {
        public DateTime Date { get; set; }
        public decimal ClosePrice { get; set; }
        public decimal OpenPrice { get; set; }
        public decimal HighestPrice { get; set; }
        public decimal LowestPrice { get; set; }
        public int Turnover { get; set; }
    }
}
