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

        public DailyAccountData Clone()
        {
            var data = new DailyAccountData
            {
                DailyPrice = DailyPrice,
                Balance = Balance,
                PercentageBalance = PercentageBalance,
                RealTimePercentageBalance = RealTimePercentageBalance,
                Contract = Contract,
                CloseTransaction = CloseTransaction,
                OpenTransaction = OpenTransaction,
                NextTransaction = NextTransaction,
                InternalProfit = InternalProfit
            };
            return data;
        }
    }
}
