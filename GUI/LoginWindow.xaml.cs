using System.Windows;
using Taiji.Core.Models;

namespace Taiji.GUI
{
    public partial class LoginWindow : Window
    {
        public LoginWindow(LoginPromptInfo hint)
        {
            InitializeComponent();
            var info = hint ?? new LoginPromptInfo();
            AccountBox.Text = info.Account ?? "";
            RememberBox.IsChecked = info.RememberAutoLogin;
            AccountBox.Focus();
        }

        public LoginCredentials Credentials { get; private set; }

        private void Login_OnClick(object sender, RoutedEventArgs e)
        {
            var account = (AccountBox.Text ?? "").Trim();
            var password = PasswordBox.Password ?? "";
            if (account.Length == 0)
            {
                MessageBox.Show(this, "请输入用户名", "登录", MessageBoxButton.OK, MessageBoxImage.Warning);
                AccountBox.Focus();
                return;
            }
            if (password.Length == 0)
            {
                MessageBox.Show(this, "请输入密码", "登录", MessageBoxButton.OK, MessageBoxImage.Warning);
                PasswordBox.Focus();
                return;
            }

            Credentials = new LoginCredentials
            {
                Account = account,
                Password = password,
                RememberAutoLogin = RememberBox.IsChecked == true
            };
            DialogResult = true;
        }

        private void Cancel_OnClick(object sender, RoutedEventArgs e)
        {
            Credentials = LoginCredentials.CancelledResult();
            DialogResult = false;
        }
    }
}
