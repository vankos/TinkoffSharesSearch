using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tinkoff.Trading.OpenApi.Models;
using Tinkoff.Trading.OpenApi.Network;
using TinkoffSearchLib.Models;

namespace TinkoffSearchLib.Services
{
    public class GetDataService
    {
        private readonly Context context;
        public GetDataService(string token)
        {
            try
            {
                var connection = ConnectionFactory.GetConnection(token);
                context =  connection.Context;
            }
            catch (Exception)
            {
                MessageService.SendMessage("Не удалось получить контекст",false);
                throw;
            }
        }

        public Task<List<Security>> GetCandlesForAllSharesOnDate(UserData userData)
        {
            if (userData.StartDate > userData.EndDate)
                throw new ArgumentException("Первая дата больше второй");

            return GetCandlesForAllSharesOnDateInternal(userData);
        }

        private async Task<List<Security>> GetCandlesForAllSharesOnDateInternal(UserData userData)
        {
            CandleInterval interval = CandleInterval.Month;
            if ((userData.EndDate - userData.StartDate).TotalDays <= 7) interval = CandleInterval.Hour;
            else if ((userData.EndDate - userData.StartDate).TotalDays <= 90) interval = CandleInterval.Day;
            else if ((userData.EndDate - userData.StartDate).TotalDays <= 600) interval = CandleInterval.Week;
            try
            {
                List<MarketInstrument> marketInstruments = new();
                marketInstruments.AddRange((await context.MarketStocksAsync().ConfigureAwait(false)).Instruments);

                if (!userData.IsUSD)
                    marketInstruments = marketInstruments.Where(instr => instr.Currency == Currency.Rub).ToList();
                if (!userData.IsRUR)
                    marketInstruments = marketInstruments.Where(instr => instr.Currency == Currency.Usd).ToList();

                int failedInstrumentsCounter = 0;
                List<Security> securities = new();
                foreach (var instrument in marketInstruments)
                {
                    try
                    {
                        Thread.Sleep(250);
                        List<CandlePayload> candles = (await context.MarketCandlesAsync(instrument.Figi, DateTime.SpecifyKind(userData.StartDate, DateTimeKind.Local), DateTime.SpecifyKind(userData.EndDate, DateTimeKind.Local), interval).ConfigureAwait(false)).Candles;
                        if (candles.Count>0)
                        {
                            securities.Add(new Security()
                            {
                                Name = instrument.Name,
                                Candles = candles
                            });
                        }
                    }
                    catch (Exception)
                    {
                        failedInstrumentsCounter++;
                    }
                }
                string message = $"Всего {securities.Count + failedInstrumentsCounter} акций \nУспешно {securities.Count} акций\nЗафейлилось {failedInstrumentsCounter} акций";
                MessageService.SendMessage(message, true);
                return securities;
            }
            catch (Exception e)
            {
                MessageService.SendMessage(e.Message, false);
                return null;
            }
        }
    }
}
