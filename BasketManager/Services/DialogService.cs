namespace BasketManager.Services
{
    public class DialogService : IDialogService
    {
        public Task<bool> ShowConfirmAsync(string title, string message, string accept, string cancel) =>
            App.Current.MainPage.DisplayAlert(title, message, accept, cancel);

        public Task ShowAlertAsync(string title, string message, string cancel) =>
            App.Current.MainPage.DisplayAlert(title, message, cancel);

        public Task<string> ShowActionSheetAsync(string title, string cancel, string destruction, params string[] buttons) =>
            App.Current.MainPage.DisplayActionSheet(title, cancel, destruction, buttons);
    }
}
