using System;

namespace FuturesAnalyzer.Models
{
    public class DailyAccountData
    {
        public DailyPrice DailyPrice { get; set; }
        public decimal Balance { get; set; }
        public Contract Contract { get; set; }
        public Transaction CloseTransaction { get; set; }
        public Transaction OpenTransaction { get; set; }
    }
}
