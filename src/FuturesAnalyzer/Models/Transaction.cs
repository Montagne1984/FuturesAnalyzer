using System;

namespace FuturesAnalyzer.Models
{
    public class Transaction
    {
        public DateTime Date { get; set; }
        public decimal Price { get; set; }
        public Contract Contract { get; set; }
        public Behavior Behavior { get; set; }
        public decimal TransactionFee { get; set; }
    }

    public enum Behavior
    {
        Open = 1,
        Close = -1
    }
}
