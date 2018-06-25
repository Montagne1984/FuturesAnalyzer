using System;
using FuturesAnalyzer.Models.States;

namespace FuturesAnalyzer.Models
{
    public class DailyAccountData
    {
        public DailyPrice DailyPrice { get; set; }
        public decimal Balance { get; set; }
        public decimal PercentageBalance { get; set; }
        public decimal RealTimePercentageBalance { get; set; }
        public Contract Contract { get; set; }
        public Transaction CloseTransaction { get; set; }
        public Transaction OpenTransaction { get; set; }
        public string NextTransaction { get; set; }
        public decimal InternalProfit { get; set; }
    }
}
