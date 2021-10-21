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

        public Task<List<Security>> GetCandlesForAllSharesOnDate(DateTime startDate, DateTime endDate, Currency currency)
        {
            if (startDate > endDate)
                throw new ArgumentException("Первая дата больше второй");

            return GetCandlesForAllSharesOnDateInternal(startDate, endDate, currency);
        }

        private async Task<List<Security>> GetCandlesForAllSharesOnDateInternal(DateTime startDate, DateTime endDate, Currency currency)
        {
            CandleInterval interval = CandleInterval.Month;
            if ((endDate - startDate).TotalDays <= 7) interval = CandleInterval.Hour;
            else if ((endDate - startDate).TotalDays <= 90) interval = CandleInterval.Day;
            else if ((endDate - startDate).TotalDays <= 600) interval = CandleInterval.Week;
            try
            {
                MarketInstrumentList markertlist = await context.MarketStocksAsync();

                int failedInstrumentsCounter = 0;
                List<Security> securities = new();
                foreach (var instrument in markertlist.Instruments.Where((i) => i.Currency == currency))
                {
                    try
                    {
                        Thread.Sleep(250);
                        List<CandlePayload> candles = (await context.MarketCandlesAsync(instrument.Figi, DateTime.SpecifyKind(startDate, DateTimeKind.Local), DateTime.SpecifyKind(endDate, DateTimeKind.Local), interval)).Candles;
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
