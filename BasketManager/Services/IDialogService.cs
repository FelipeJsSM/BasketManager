namespace BasketManager.Services
{
    public interface IDialogService
    {
        Task<bool> ShowConfirmAsync(string title, string message, string accept, string cancel);
        Task ShowAlertAsync(string title, string message, string cancel);
        Task<string> ShowActionSheetAsync(string title, string cancel, string destruction, params string[] buttons);
    }
}
