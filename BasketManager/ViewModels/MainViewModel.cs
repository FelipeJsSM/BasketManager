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

        [ObservableProperty]
        private ObservableCollection<Jugador> _equipoA = new();

        [ObservableProperty]
        private ObservableCollection<Jugador> _equipoB = new();

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
            var ganadores = ganoEquipoA ? EquipoA : EquipoB;
            var perdedores = ganoEquipoA ? EquipoB : EquipoA;

            if (ganadores.Count == 0 || perdedores.Count == 0)
            {
                await App.Current.MainPage.DisplayAlert("Error", "No hay jugadores en la cancha.", "OK");
                return;
            }

            foreach (var p in perdedores)
            {
                p.VictoriasConsecutivas = 0;
                p.EstaEnCancha = false;
                p.HoraRegistro = DateTime.Now; // Regla 3: Al fondo de la lista
                await _db.SaveJugadorAsync(p);
            }

            foreach (var g in ganadores) g.VictoriasConsecutivas++;

            // Lógica de Rotación:
            if (ganadores.Count > 0 && ganadores.First().VictoriasConsecutivas == 2)
            {
                await RotarDiezJugadores(ganadores);
            }
            else
            {
                await EntrarCincoNuevos(perdedores);
            }

            await CargarJugadores();
        }

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
            var disponibles = ListaEspera.Where(j => j.HaPagado && !j.EstaEnCancha)
                                         .OrderBy(j => j.HoraRegistro)
                                         .Take(10).ToList();

            if (disponibles.Count < 5)
            {
                await App.Current.MainPage.DisplayAlert("Faltan jugadores", "Necesitas al menos 5 personas pagadas para que entre un equipo.", "OK");
                return;
            }

            // Si hay 10, iniciamos el primer juego de la liga
            EquipoA.Clear();
            EquipoB.Clear();

            for (int i = 0; i < disponibles.Count; i++)
            {
                disponibles[i].EstaEnCancha = true;
                if (i < 5) EquipoA.Add(disponibles[i]);
                else if (i < 10) EquipoB.Add(disponibles[i]);
                await _db.SaveJugadorAsync(disponibles[i]);
            }
        }

        private async Task RotarDiezJugadores(IEnumerable<Jugador> ganadores)
        {
            // 1. Sacamos a los ganadores de la cancha
            foreach (var g in ganadores)
            {
                g.EstaEnCancha = false;
                g.VictoriasConsecutivas = 0;
                g.HoraRegistro = DateTime.Now;
                await _db.SaveJugadorAsync(g);
            }

            EquipoA.Clear();
            EquipoB.Clear();

            // 2. Buscamos a los próximos 10
            var listos = ListaEspera.Where(j => j.HaPagado && !j.EstaEnCancha)
                                    .OrderBy(j => j.HoraRegistro)
                                    .Take(10).ToList();

            if (listos.Count < 10)
            {
                bool respuesta = await App.Current.MainPage.DisplayAlert(
                    "Faltan Jugadores",
                    $"Solo hay {listos.Count} listos. ¿Esperamos a que llegue alguien o rotamos solo 5?",
                    "Esperar", "Rotar 5");

                if (respuesta) return;
            }

            // 3. Repartimos los nuevos
            for (int i = 0; i < listos.Count; i++)
            {
                listos[i].EstaEnCancha = true;
                if (i < 5) EquipoA.Add(listos[i]);
                else EquipoB.Add(listos[i]);
                await _db.SaveJugadorAsync(listos[i]);
            }
        }

        private async Task EntrarCincoNuevos(IEnumerable<Jugador> perdedores)
        {
            if (EquipoA.Count == 0 || !EquipoA.Any(j => j.EstaEnCancha)) EquipoA.Clear();
            else EquipoB.Clear();

            var proximosCinco = ListaEspera.Where(j => j.HaPagado && !j.EstaEnCancha)
                                           .OrderBy(j => j.HoraRegistro)
                                           .Take(5).ToList();

            foreach (var j in proximosCinco)
            {
                j.EstaEnCancha = true;
                if (EquipoA.Count < 5) EquipoA.Add(j);
                else EquipoB.Add(j);
                await _db.SaveJugadorAsync(j);
            }
        }
    }
} 