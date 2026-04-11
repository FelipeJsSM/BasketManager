using BasketManager.ViewModels;

namespace BasketManager;

public partial class DraftPage : ContentPage
{
    public DraftPage(MainViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}