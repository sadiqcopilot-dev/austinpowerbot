using System;
using System.Threading.Tasks;
using System.Windows;

namespace AustinXPowerBot.Desktop.Views
{
    public partial class NotificationsPopup : Window
    {
        public NotificationsPopup()
        {
            InitializeComponent();
            Loaded += NotificationsPopup_Loaded;
        }

        private void NotificationsPopup_Loaded(object sender, RoutedEventArgs e)
        {
            // position bottom-right of primary screen
            var working = SystemParameters.WorkArea;
            Left = working.Right - Width - 12;
            Top = working.Bottom - Height - 12;
        }

        public void SetContent(string title, string message)
        {
            TitleBlock.Text = title ?? "";
            MessageBlock.Text = message ?? "";
        }

        public async Task ShowForAsync(TimeSpan duration)
        {
            Show();
            try { await Task.Delay(duration); } catch { }
            try { Close(); } catch { }
        }
    }
}
