﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Runtime.Serialization.Formatters.Binary;
using Tinkoff.Trading.OpenApi.Network;
using Tinkoff.Trading.OpenApi.Models;
using System.Data;
using System.Net;
using System.Text.RegularExpressions;
using Python.Runtime;

namespace Tinkoff
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string saveFilePath = "savedData.dat";
        private Context context = null;
        private readonly SaveData savedata;
        private readonly bool IsInitializing;

        public MainWindow()
        {
            IsInitializing = true;
            InitializeComponent();
            savedata= SaveData.GetSaveData();
            TokenTextEdit.Text = savedata.Token+"";
            MoneyLimit.Text = savedata.MoneyLimitValue + "";
            USDRadioButton.IsChecked = savedata.Currency == Currency.Usd;
            RubRadioButton.IsChecked = savedata.Currency == Currency.Rub;
            StartDate.SelectedDate = savedata.StartDate;
            EndDate.SelectedDate = savedata.EndDate;
            try
            {
                context = GetContext(savedata.Token);
                ErrorTextBlock.Text = "";
            }
            catch (Exception)
            {
                ErrorTextBlock.Text = "Неправильный токен";
            }
            IsInitializing = false;
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsInitializing) return;
            try
            {
                savedata.MoneyLimitValue = decimal.Parse(MoneyLimit.Text);
                savedata.Save();
                ErrorTextBlock.Text = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"При изменении лимита произошла ошибка: {ex.Message}");
            }
        }

        private void TextBox_TextChanged_1(object sender, TextChangedEventArgs e)
        {
            if (IsInitializing) return;
            try
            {
                savedata.Token = TokenTextEdit.Text;
                savedata.Save();
                context = GetContext(savedata.Token);
                ErrorTextBlock.Text = "";
            }
            catch (Exception)
            {
                ErrorTextBlock.Text = "Неправильный токен";
            }
        }

        private Context GetContext(string token)
        {
            try
            {
                var connection = ConnectionFactory.GetConnection(token);
                ErrorTextBlock.Text = "";
                return connection.Context;
            }
            catch (Exception)
            {
                ErrorTextBlock.Text = "Не удалось получить контекст";
                throw;
            }
        }

        private async Task GetPriceChange(Context context, Currency curr, DateTime startDate, DateTime endDate)
        {
            if (startDate > endDate)
            { ErrorTextBlock.Text = "Первая дата больше второй"; return; }

            CandleInterval interval;
            if ((endDate - startDate).TotalDays <=7) interval = CandleInterval.Hour;
                else  if ((endDate - startDate).TotalDays <= 90) interval = CandleInterval.Day;
                        else  interval = CandleInterval.Week;
            try
            {
                ErrorTextBlock.Text = "Идет загрузка";
                decimal priceLimit = savedata.MoneyLimitValue;
                MarketInstrumentList markertlist = await context.MarketStocksAsync();
                DataTable diff = new DataTable();
                diff.Columns.Add("Name");
                diff.Columns.Add("Change", typeof(decimal));
                diff.Columns.Add("Линейность");
                diff.Columns.Add("Zacks рейтинг");
                int failedInstrumentCount = 0;
                foreach (var instrument in markertlist.Instruments.Where((i) => i.Currency == curr))
                {
                    try
                    {
                        Thread.Sleep(100);
                        List<CandlePayload> candles = (await context.MarketCandlesAsync(instrument.Figi, DateTime.SpecifyKind(startDate, DateTimeKind.Local), DateTime.SpecifyKind(endDate, DateTimeKind.Local), interval)).Candles;
                        if (candles.Count > 0 && candles.Last().Close < priceLimit)
                        {

                            string name = instrument.Name;
                            decimal growth = Math.Round((candles[0].Open - candles.Last().Close) / candles.Last().Close * 100, 2) * -1;
                            decimal linearity = GetMadeUpCoeff(candles);
                            string zacksScore = linearity<(decimal)0.1 && curr!=Currency.Rub && growth>0? await GetZacksScore(instrument.Ticker):"";
                            diff.Rows.Add(name, growth, $"{linearity:#0.##%}",zacksScore);
                        }
                    }
                    catch (Exception)
                    {
                        failedInstrumentCount++;
                    }
                }
                DataDataGrid.ItemsSource = diff.DefaultView;
                ErrorTextBlock.Text = $"Всего {diff.Rows.Count+ failedInstrumentCount} акций \nПоказано {diff.Rows.Count} акций\nЗафейлилось {failedInstrumentCount} акций";
            }
            catch (Exception e)
            {
                ErrorTextBlock.Text=e.Message;
            }
        }

        private decimal GetMadeUpCoeff(List<CandlePayload> candles)
        {
            decimal k = (candles.Last().Close - candles.First().Close) / candles.Count;
            decimal b = candles.First().Close;
            List<decimal> diffs = new List<decimal>();
            for (int x = 1; x <= candles.Count; x++)
            {
                diffs.Add(Math.Abs(candles[x - 1].Close-(k * x+b))/ (k * x + b));
            }
            return diffs.Average();
        }

        private async Task<string> GetZacksScore(string secCode)
        {
            MatchCollection matches;
            using (WebClient client = new WebClient())
            {
                string htmlCode = await client.DownloadStringTaskAsync($"https://zacks.com/stock/quote/{secCode}");
                matches = Regex.Matches(htmlCode, " <p class=\"rank_view\">\\n\\s*(\\d.*?)<span");
            }

            if (matches.Count > 0 )
            {
                if(matches[0].Groups.Count>1)
                    return matches[0].Groups[1].Value;
                
            }
            return string.Empty;
            


        }

        [Serializable]
        private class SaveData
        {
            public string Token { get; set; }
            public decimal MoneyLimitValue { get; set; }
            public Currency Currency { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public static SaveData GetSaveData()
            {
                try
                {
                    using (FileStream fs = File.OpenRead(saveFilePath))
                    {
                        BinaryFormatter bf = new BinaryFormatter();
                        return (SaveData)bf.Deserialize(fs);
                    }
                }
                catch (Exception)
                {
                    return new SaveData();
                }
            }

            public void Save()
            {
                using (FileStream fs = File.OpenWrite(saveFilePath))
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(fs, this);
                }
            }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            ErrorTextBlock.Text = "";
            if (context != null)
                await GetPriceChange(context, savedata.Currency,savedata.StartDate, savedata.EndDate);
        }

        private void RubRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (IsInitializing) return;
            savedata.Currency = RubRadioButton?.IsChecked == true ? Currency.Rub : Currency.Usd;
            savedata.Save();
        }

        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsInitializing) return;
            savedata.StartDate = StartDate.SelectedDate.GetValueOrDefault();
            savedata.Save();
        }

        private void EndDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsInitializing) return;
            savedata.EndDate = ((DateTime)e.AddedItems[0]).AddDays(1) > DateTime.Now ? DateTime.Now : ((DateTime)e.AddedItems[0]).AddDays(1);
            savedata.Save();
        }
    }
}
