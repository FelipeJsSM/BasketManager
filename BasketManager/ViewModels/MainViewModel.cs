using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BasketManager.Models; // Asegúrate de que este sea el nombre de tu proyecto
using BasketManager.Services;
using System.Collections.ObjectModel;

namespace BasketManager.ViewModels
{
    // La clase debe ser 'partial' para que el generador trabaje
    public partial class MainViewModel : ObservableObject
    {
        private readonly DatabaseService _db;

        public ObservableCollection<Jugador> ListaEspera { get; set; } = new();

        // Al usar '_', el generador crea automáticamente 'EquipoGanadorEspera' (con E mayúscula)
        [ObservableProperty]
        private Jugador? _equipoGanadorEspera;

        // Al usar '_', el generador crea automáticamente 'EstaEnModoClasificatorio' (con E mayúscula)
        [ObservableProperty]
        private bool _estaEnModoClasificatorio;

        public MainViewModel(DatabaseService db)
        {
            _db = db;
            _ = CargarJugadores();
        }

        [RelayCommand]
        public async Task FinalizarJuego(bool ganoEquipoA)
        {
            // Placeholder para que compile, luego pondremos la lógica real
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
    }
}