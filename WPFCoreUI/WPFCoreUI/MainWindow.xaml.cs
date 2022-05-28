using System;
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
using System.Windows.Input;
using Notifications.Wpf.Core;
using TinkoffSearchLib.Models;

namespace Tinkoff
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static RoutedCommand ShowCommand { get; set; } = new RoutedCommand();
        private readonly WPFController.WpfController controller;
        public MainWindow()
        {
            controller = new WPFController.WpfController();
            controller.OnMessageRecived += (_, e) =>ErrorTextBlock.Text = e;
            controller.OnNotificationMessageRecived += (_, e) =>
            {
                ErrorTextBlock.Text = e;
                var notificationManager = new NotificationManager();
                notificationManager.ShowAsync(new NotificationContent
                {
                    Title = "Tinkoff shares",
                    Message = e,
                    Type = NotificationType.Success
                });
            };
            controller.OnViewDataChanged += (_, data) => DataDataGrid.ItemsSource = data;

            InitializeComponent();

            ShowCommand.InputGestures.Add(new KeyGesture(Key.Enter, ModifierKeys.None));

            DataContext = controller.UserData;

            StartDate.SelectedDate = DateTime.Now.AddYears(-1);
            EndDate.SelectedDate = DateTime.Now;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            controller.SaveUserData();
        }

        private async void ShowCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            MessageBoxResult messageBoxResult= MessageBoxResult.Yes;
            if (controller.UnflteredData.Count != 0)
                messageBoxResult = MessageBox.Show("Зарузить данные заново?", "Зарузить данные?", MessageBoxButton.YesNo);
            if (controller.UnflteredData.Count == 0 || messageBoxResult == MessageBoxResult.Yes)
            {
                ErrorTextBlock.Text = "Загрузка данных";
                await controller.GetData();
                controller.FilterData();
            }
        }
    }
}
