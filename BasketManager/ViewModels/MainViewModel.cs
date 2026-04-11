using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BasketManager.Models; 
using BasketManager.Services;
using System.Collections.ObjectModel;

namespace BasketManager.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly DatabaseService _db;

        public ObservableCollection<Jugador> ListaEspera { get; set; } = new();

        [ObservableProperty]
        private Jugador? _equipoGanadorEspera;

        [ObservableProperty]
        private bool _estaEnModoClasificatorio;

        public ObservableCollection<Jugador> EquipoA { get; set; } = new();
        public ObservableCollection<Jugador> EquipoB { get; set; } = new();

        [ObservableProperty]
        private string _nombreNuevo = string.Empty;

        [ObservableProperty]
        private bool _haPagadoNuevo;

        public MainViewModel(DatabaseService db)
        {
            _db = db;
            _ = CargarJugadores();
        }

        [RelayCommand]
        public async Task AgregarJugador()
        {
            if (string.IsNullOrWhiteSpace(NombreNuevo)) return;

            var nuevo = new Jugador
            {
                Nombre = NombreNuevo,
                HaPagado = HaPagadoNuevo,
                HoraRegistro = DateTime.Now 
            };

            await _db.SaveJugadorAsync(nuevo);

            NombreNuevo = string.Empty;
            HaPagadoNuevo = false;
            await CargarJugadores();
        }

        [RelayCommand]
        public async Task EliminarJugador(Jugador jugador)
        {
            await _db.DeleteJugadorAsync(jugador);
            await CargarJugadores();
        }

        [RelayCommand]
        public async Task FinalizarJuego(bool ganoEquipoA)
        {
            Jugador tempGanador = new Jugador { Nombre = "Ganador" };

            if (tempGanador.VictoriasConsecutivas == 2)
            {
                EstaEnModoClasificatorio = true;
                EquipoGanadorEspera = tempGanador;
                await IniciarClasificatorio();
            }
            else if (EstaEnModoClasificatorio)
            {
                EstaEnModoClasificatorio = false;
                await IniciarRetoContraCampeon(tempGanador);
            }
        }

        private async Task IniciarClasificatorio() => await Task.CompletedTask;
        private async Task IniciarRetoContraCampeon(Jugador ganador) => await Task.CompletedTask;

        private async Task CargarJugadores()
        {
            var lista = await _db.GetJugadoresAsync();
            ListaEspera.Clear();
            foreach (var j in lista)
            {
                ListaEspera.Add(j);
            }
        }

        [RelayCommand]
        public async Task IniciarSiguienteJuego()
        {
            var disponibles = ListaEspera.Where(j => j.HaPagado).ToList();

            if (disponibles.Count < 5)
            {
                await App.Current.MainPage.DisplayAlert("Faltan jugadores", "Necesitas al menos 5 personas pagadas para que entre un equipo.", "OK");
                return;
            }

        }
    }
}