using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TinkoffSearchLib.Models;
using TinkoffSearchLib.Services;

namespace WPFController
{
    public class WpfController
    {
        public UserData UserData { get; set; }
        public List<Security> UnflteredData { get; set; } = new();
        private GetDataService getDataService;
        public  event EventHandler<string> OnMessageRecived;
        public  event EventHandler<string> OnNotificationMessageRecived;
        public  event EventHandler<List<Security>> OnViewDataChanged;

        public WpfController()
        {
            UserData =  SaveService.LoadData();
            MessageService.OnMessageRecived += (o, e) => OnMessageRecived?.Invoke(o, e);
            MessageService.OnNotificationMessageRecived += (o, e) => OnNotificationMessageRecived?.Invoke(o, e);
            UserData.OnLinearityChanged += (_, _) => FilterData();
            UserData.OnMoneyLimitValueChanged += (_, _) => FilterData();
        }

        public void SaveUserData()
        {
            SaveService.SaveData(UserData);
        }

        async public Task GetData()
        {
            if (UserData.Token == null && getDataService is null)
            {
                MessageService.SendMessage("Нет токена", false);
                return;
            }
            try
            {
                if (getDataService is null)
                    getDataService = new GetDataService(UserData.Token);
                UnflteredData = await getDataService.GetCandlesForAllSharesOnDate(UserData);
            }
            catch (Exception ex)
            {
                MessageService.SendMessage($"Ошибка получения данных:{ex.Message}", false);
            }
        }

        public void FilterData()
        {
            var result = AnalyticalService.GetLinearity(AnalyticalService.GetGrowth(UnflteredData))
              .Where(s => s.Linearity <= UserData.Linearity
              && s.Candles.Last().Close <= UserData.MoneyLimit)
              .ToList();
            OnViewDataChanged?.Invoke(this,result);
        }
    }
}
