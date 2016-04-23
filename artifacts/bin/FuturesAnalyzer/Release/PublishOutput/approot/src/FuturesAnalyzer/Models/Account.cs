using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FuturesAnalyzer.Models.States;

namespace FuturesAnalyzer.Models
{
    public class Account
    {
        public decimal Balance { get; set; }
        public Contract Contract { get; set; }
        public MarketState MarketState { get; set; }
        public bool? IsLastTransactionLoss { get; set; }
        public decimal TransactionFeeRate { get; set; }

        public decimal DeductTransactionFee(decimal price, int unit)
        {
            var transactionFee = price * TransactionFeeRate * unit;
            Balance -= transactionFee;
            return transactionFee;
        }
    }
}
