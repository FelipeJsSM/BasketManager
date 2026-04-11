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
        public ObservableCollection<Jugador> EquipoA { get; set; } = new();
        public ObservableCollection<Jugador> EquipoB { get; set; } = new();
        public ObservableCollection<Jugador> EquipoCampeonEspera { get; set; } = new();
        public bool HayJuegoActivo => EquipoA.Count == 5 && EquipoB.Count == 5;

        public ObservableCollection<Jugador> ListaDraft { get; set; } = new();

        [ObservableProperty] private string _nombreNuevo = string.Empty;
        [ObservableProperty] private bool _haPagadoNuevo;
        [ObservableProperty] private bool _isBusy;

        public MainViewModel(DatabaseService db)
        {
            _db = db;
            _ = CargarJugadores();
        }

        private async Task CargarJugadores()
        {
            var lista = await _db.GetJugadoresAsync();
            ListaEspera.Clear();

            var idsCampeon = EquipoCampeonEspera.Select(c => c.Id).ToList();

            var enEspera = lista.Where(j => !j.EstaEnCancha && !idsCampeon.Contains(j.Id))
                                .OrderBy(j => j.HoraRegistro).ToList(); // .ToList() para poder iterar seguro

            foreach (var j in enEspera) ListaEspera.Add(j);

            OnPropertyChanged(nameof(HayJuegoActivo));
        }

        [RelayCommand]
        public async Task PrepararDraft()
        {
            int cuposA = 5 - EquipoA.Count;
            int cuposB = 5 - EquipoB.Count;
            int totalRequeridos = Math.Max(0, cuposA) + Math.Max(0, cuposB);

            ListaDraft.Clear();

            var lista = await _db.GetJugadoresAsync();
            var idsCampeon = EquipoCampeonEspera.Select(c => c.Id).ToList();

            var seleccion = lista.Where(j => !j.EstaEnCancha && !idsCampeon.Contains(j.Id) && j.HaPagado)
                                 .OrderBy(j => j.HoraRegistro)
                                 .Take(totalRequeridos)
                                 .ToList();

            foreach (var j in seleccion) ListaDraft.Add(j);

            if (ListaDraft.Count == 0 && totalRequeridos > 0)
            {
                await App.Current.MainPage.DisplayAlert("Aviso", "No hay jugadores con el pago al día para completar el draft.", "OK");
            }
        }

        [RelayCommand]
        public async Task IniciarDraft()
        {
            ListaDraft.Clear();

            var lista = await _db.GetJugadoresAsync();
            var idsCampeon = EquipoCampeonEspera.Select(c => c.Id).ToList();

            var disponibles = lista.Where(j => !j.EstaEnCancha && !idsCampeon.Contains(j.Id))
                                   .OrderBy(j => j.HoraRegistro)
                                   .Take(10);

            foreach (var j in disponibles) ListaDraft.Add(j);

            await Shell.Current.GoToAsync("///DraftPage");
        }

        [RelayCommand]
        public async Task SaltarJugadorDraft(Jugador j)
        {
            j.HoraRegistro = DateTime.Now;
            await _db.SaveJugadorAsync(j);
            ListaDraft.Remove(j);

            var lista = await _db.GetJugadoresAsync();
            var idsCampeon = EquipoCampeonEspera.Select(c => c.Id).ToList();
            var enDraft = ListaDraft.Select(d => d.Id).ToList();

            var siguiente = lista.Where(p => !p.EstaEnCancha
                                          && !idsCampeon.Contains(p.Id)
                                          && !enDraft.Contains(p.Id)
                                          && p.Id != j.Id
                                          && p.HaPagado) 
                                 .OrderBy(p => p.HoraRegistro)
                                 .FirstOrDefault();

            if (siguiente != null) ListaDraft.Add(siguiente);
            await CargarJugadores();
        }

        [RelayCommand]
        public async Task AlternarPagoJugador(Jugador j)
        {
            if (j == null) return;

            j.HaPagado = !j.HaPagado;

            await _db.SaveJugadorAsync(j);
            await CargarJugadores();
        }

        [RelayCommand]
        public async Task MoverAEquipoA(Jugador j)
        {
            if (IsBusy || j == null || EquipoA.Count >= 5 || j.EstaEnCancha) return;

            j.EstaEnCancha = true;
            EquipoA.Add(j);
            await _db.SaveJugadorAsync(j);


            if (ListaDraft.Contains(j)) ListaDraft.Remove(j);

            OnPropertyChanged(nameof(HayJuegoActivo));
            await CargarJugadores(); 
        }

        [RelayCommand]
        public async Task MoverAEquipoB(Jugador j)
        {
            if (IsBusy || j == null || EquipoB.Count >= 5 || j.EstaEnCancha) return;

            j.EstaEnCancha = true;
            EquipoB.Add(j);
            await _db.SaveJugadorAsync(j);

            if (ListaDraft.Contains(j)) ListaDraft.Remove(j);

            OnPropertyChanged(nameof(HayJuegoActivo));
            await CargarJugadores();
        }

        [RelayCommand]
        public async Task FinalizarJuego(bool ganoEquipoA)
        {
            if (IsBusy) return;
            try
            {
                IsBusy = true;
                var perdedores = ganoEquipoA ? EquipoB : EquipoA;
                var ganadores = ganoEquipoA ? EquipoA : EquipoB;

                foreach (var p in perdedores)
                {
                    p.EstaEnCancha = false;
                    p.VictoriasConsecutivas = 0;
                    p.HoraRegistro = DateTime.Now;
                    await _db.SaveJugadorAsync(p);
                }

                var victorias = ganadores.FirstOrDefault()?.VictoriasConsecutivas ?? 0;
                if (victorias + 1 >= 2)
                {
                    EquipoCampeonEspera.Clear();
                    foreach (var g in ganadores)
                    {
                        g.EstaEnCancha = false;
                        g.VictoriasConsecutivas = 0;
                        await _db.SaveJugadorAsync(g);
                        EquipoCampeonEspera.Add(g);
                    }
                    // Cuando el campeón gana 2 y se sienta...
                    EquipoA.Clear();
                    EquipoB.Clear();

                    await App.Current.MainPage.DisplayAlert("Modo Campeón",
        "El campeón se sienta. Elige manualmente a los 10 que van a jugar ahora.", "OK");

                    // Lanza el Draft automáticamente
                    await IniciarDraft();
                }
                else
                {
                    foreach (var g in ganadores) g.VictoriasConsecutivas++;
                    if (ganoEquipoA) EquipoB.Clear(); else EquipoA.Clear();

                    if (EquipoCampeonEspera.Count == 5)
                    {
                        foreach (var c in EquipoCampeonEspera)
                        {
                            c.EstaEnCancha = true;
                            if (EquipoA.Count < 5) EquipoA.Add(c); else EquipoB.Add(c);
                            await _db.SaveJugadorAsync(c);
                        }
                        EquipoCampeonEspera.Clear();
                    }
                    else
                    {
                        await EntrarCincoNuevos();
                    }
                }
                await CargarJugadores();
            }
            finally { IsBusy = false; }
        }

        private async Task EntrarCincoNuevos()
        {
            var proximos = ListaEspera.Where(j => j.HaPagado).Take(5).ToList();

            bool rellenarA = EquipoA.Count == 0;

            foreach (var j in proximos)
            {
                j.EstaEnCancha = true;
                await _db.SaveJugadorAsync(j);
                if (rellenarA) EquipoA.Add(j); else EquipoB.Add(j);
            }
        }

        [RelayCommand]
        public async Task GenerarSeedData()
        {
            var nombres = new List<string> { "Felipe (P)", "Steven", "Carlos", "Roberto", "Isaac", "Juan", "Pedro", "Luis", "Miguel", "Jose", "Andres", "Diego", "Fernando", "Ricardo", "Javier", "Oscar", "Manuel", "Sergio", "Alex", "Darlin" };
            foreach (var n in nombres)
            {
                await _db.SaveJugadorAsync(new Jugador { Nombre = n, HaPagado = true, HoraRegistro = DateTime.Now.AddSeconds(nombres.IndexOf(n)) });
            }
            await CargarJugadores();
        }

        [RelayCommand]
        public async Task AgregarJugador()
        {
            if (IsBusy || string.IsNullOrWhiteSpace(NombreNuevo)) return;

            try
            {
                IsBusy = true;
                var nuevo = new Jugador
                {
                    Nombre = NombreNuevo.Trim(),
                    HaPagado = HaPagadoNuevo, 
                    HoraRegistro = DateTime.Now
                };
                await _db.SaveJugadorAsync(nuevo);

                NombreNuevo = string.Empty;
                HaPagadoNuevo = false;
                await CargarJugadores();
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task EliminarJugador(Jugador j)
        {
            await _db.DeleteJugadorAsync(j);
            await CargarJugadores();
        }

        [RelayCommand]
        public async Task ReiniciarCancha()
        {
            bool confirmar = await App.Current.MainPage.DisplayAlert("Reiniciar", "¿Quieres vaciar la cancha y mandar a todos a espera?", "Sí", "No");
            if (!confirmar) return;

            EquipoA.Clear();
            EquipoB.Clear();
            EquipoCampeonEspera.Clear();

            var todos = await _db.GetJugadoresAsync();
            foreach (var j in todos)
            {
                j.EstaEnCancha = false;
                j.VictoriasConsecutivas = 0;
                await _db.SaveJugadorAsync(j);
            }

            await CargarJugadores();
            await App.Current.MainPage.DisplayAlert("Listo", "Cancha vacía.", "OK");
        }

        [RelayCommand]
        public async Task IniciarSiguienteJuego()
        {
            EquipoA.Clear();
            EquipoB.Clear();

            var disponibles = ListaEspera.Where(j => j.HaPagado).Take(10).ToList();

            if (disponibles.Count < 5)
            {
                await App.Current.MainPage.DisplayAlert("Faltan jugadores", "Necesitas al menos 5 personas pagadas para que entre un equipo.", "OK");
                return;
            }

            EquipoA.Clear();
            EquipoB.Clear();

            for (int i = 0; i < disponibles.Count; i++)
            {
                var j = disponibles[i];
                j.EstaEnCancha = true;
                await _db.SaveJugadorAsync(j);

                if (i < 5) EquipoA.Add(j);
                else EquipoB.Add(j);
            }

            await CargarJugadores();
        }
    }
}