using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

using MrExStrap.Integrations.BloxGen;

namespace MrExStrap.UI.ViewModels.Settings
{
    // AltGen tab. Generates Roblox alt accounts through BloxGen using the user's OWN API key
    // (entered here, stored locally). Advertises the maintainer's affiliate link so users sign
    // up through it. No key is ever embedded in the app.
    public class AltGenViewModel : NotifyPropertyChangedViewModel
    {
        public string ApiKey
        {
            get => App.Settings.Prop.BloxGenApiKey;
            set
            {
                App.Settings.Prop.BloxGenApiKey = value?.Trim() ?? "";
                OnPropertyChanged(nameof(ApiKey));
            }
        }

        // The account types BloxGen exposes (sent verbatim as the "type" param). "dump" and the
        // aged types may need a higher BloxGen role — the API returns a clear message if not.
        public string[] AltTypes { get; } = { "alt", "+30 days old", "+1 year old", "5+ years old", "dump" };

        private string _selectedType = "alt";
        public string SelectedType
        {
            get => _selectedType;
            set { _selectedType = value; OnPropertyChanged(nameof(SelectedType)); }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(nameof(IsBusy)); OnPropertyChanged(nameof(CanGenerate)); }
        }

        public bool CanGenerate => !_isBusy;

        private string _status = "";
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); OnPropertyChanged(nameof(HasStatus)); }
        }
        public bool HasStatus => !string.IsNullOrEmpty(_status);

        private string _username = "";
        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(nameof(Username)); OnPropertyChanged(nameof(HasResult)); }
        }

        private string _password = "";
        public string Password
        {
            get => _password;
            set { _password = value; OnPropertyChanged(nameof(Password)); }
        }

        private string _cookie = "";
        public string Cookie
        {
            get => _cookie;
            set { _cookie = value; OnPropertyChanged(nameof(Cookie)); OnPropertyChanged(nameof(HasResult)); }
        }

        private string _avatarUrl = "";
        public string AvatarUrl
        {
            get => _avatarUrl;
            set { _avatarUrl = value; OnPropertyChanged(nameof(AvatarUrl)); OnPropertyChanged(nameof(HasAvatar)); }
        }
        public bool HasAvatar => !string.IsNullOrEmpty(_avatarUrl);

        private string _userId = "";
        public string UserId
        {
            get => _userId;
            set { _userId = value; OnPropertyChanged(nameof(UserId)); OnPropertyChanged(nameof(HasUserId)); }
        }
        public bool HasUserId => !string.IsNullOrEmpty(_userId);

        public bool HasResult => !string.IsNullOrEmpty(_username) || !string.IsNullOrEmpty(_cookie);

        public ICommand GenerateCommand => new AsyncRelayCommand(GenerateAsync);
        public ICommand CopyUsernameCommand => new RelayCommand(() => CopyToClipboard(Username, "Username"));
        public ICommand CopyPasswordCommand => new RelayCommand(() => CopyToClipboard(Password, "Password"));
        public ICommand CopyCookieCommand => new RelayCommand(() => CopyToClipboard(Cookie, ".ROBLOSECURITY cookie"));

        private async Task GenerateAsync()
        {
            const string LOG_IDENT = "AltGenViewModel::GenerateAsync";

            if (_isBusy)
                return;

            if (string.IsNullOrWhiteSpace(App.Settings.Prop.BloxGenApiKey))
            {
                Status = "Enter your BloxGen API key first. Don't have one? Use the \"Get a free key\" button above.";
                return;
            }

            IsBusy = true;
            Status = "Generating…";

            try
            {
                var result = await BloxGenClient.GenerateAsync(App.Settings.Prop.BloxGenApiKey, SelectedType);

                if (result.Success)
                {
                    Username = result.Username ?? "";
                    Password = result.Password ?? "";
                    Cookie = result.Cookie ?? "";
                    AvatarUrl = result.AvatarUrl ?? "";
                    UserId = result.Id?.ToString() ?? "";

                    string region = string.IsNullOrEmpty(result.Region) ? "" : $" · region {result.Region}";
                    string cost = result.Cost.HasValue ? $" · cost {result.Cost.Value}" : "";
                    Status = $"Done — generated a '{SelectedType}' account.{region}{cost}";
                }
                else
                {
                    Status = result.Error ?? "Generation failed.";
                    if (!string.IsNullOrEmpty(result.RawResponse))
                        App.Logger.WriteLine(LOG_IDENT, $"Raw BloxGen response: {result.RawResponse}");
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                Status = $"{ex.GetType().Name}: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void CopyToClipboard(string value, string label)
        {
            if (string.IsNullOrEmpty(value))
                return;

            try
            {
                Clipboard.SetText(value);
                Status = $"{label} copied to clipboard.";
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("AltGenViewModel::CopyToClipboard", ex);
            }
        }
    }
}
