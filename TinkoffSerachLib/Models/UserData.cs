using System;

namespace TinkoffSearchLib.Models
{
    [Serializable]
    public class UserData
    {
        private decimal moneyLimitValue;
        private decimal linearity;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Token { get; set; }

        public decimal MoneyLimit
        {
            get { return moneyLimitValue; }
            set
            {
                moneyLimitValue = value;
                OnMoneyLimitValueChanged?.Invoke(this, value);
            }
        }

        public decimal Linearity
        {
            get { return linearity; }
            set
            {
                linearity = value;
                OnLinearityChanged?.Invoke(this, value);
            }
        }

        public bool IsUSD { get; set; }
        public bool IsRUR { get; set; }

        public bool IsShares { get; set; }
        public bool IsETF { get; set; }

        public event EventHandler<decimal> OnMoneyLimitValueChanged;

        public event EventHandler<decimal> OnLinearityChanged;
    }
}