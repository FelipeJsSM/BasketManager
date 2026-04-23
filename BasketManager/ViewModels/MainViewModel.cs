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
        private readonly IDialogService _dialog;
        public bool HayJuegoActivo => EquipoA.Count == 5 && EquipoB.Count == 5;

        public ObservableCollection<Jugador> ListaDraft { get; set; } = new();

        [ObservableProperty] private string _nombreNuevo = string.Empty;
        [ObservableProperty] private bool _haPagadoNuevo;
        [ObservableProperty] private bool _isBusy;

        public MainViewModel(DatabaseService db, IDialogService dialog)
        {
            _db = db;
            _dialog = dialog;

            EquipoA.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HayJuegoActivo));
            EquipoB.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HayJuegoActivo));

            _ = CargarJugadores();
        }

        [RelayCommand]
        public async Task LimpiarLigaCompleta()
        {
            bool confirmar = await _dialog.ShowConfirmAsync(
                "Confirmar limpieza",
                "¿Estás seguro de que quieres borrar todos los jugadores y resultados? Esta acción no se puede deshacer.",
                "Sí, borrar todo",
                "No, cancelar"
            );

            if (!confirmar) return;

            await _db.ClearAllAsync();

            EquipoA.Clear();
            EquipoB.Clear();
            EquipoCampeonEspera.Clear();
            ListaDraft.Clear();
            ListaEspera.Clear();

            await CargarJugadores();
            await _dialog.ShowAlertAsync("Listo", "Liga completamente reiniciada.", "OK");
        }

        private async Task CargarJugadores()
        {
            var lista = await _db.GetJugadoresAsync();
            ListaEspera.Clear();

            var idsCampeon = EquipoCampeonEspera.Select(c => c.Id).ToList();

            var enEspera = lista.Where(j => !j.EstaEnCancha && !idsCampeon.Contains(j.Id))
                                .OrderBy(j => j.HoraRegistro).ToList();

            foreach (var j in enEspera) ListaEspera.Add(j);
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
                await _dialog.ShowAlertAsync("Aviso", "No hay jugadores con el pago al día para completar el draft.", "OK");
            }
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

            await CargarJugadores();
        }

        [RelayCommand]
        public async Task FinalizarJuego(bool ganoEquipoA)
        {
            if (IsBusy) return;
            try
            {
                IsBusy = true;

                var ganadores = ganoEquipoA ? EquipoA : EquipoB;
                var perdedores = ganoEquipoA ? EquipoB : EquipoA;

                if (ganadores.Count == 0) return;

                var victoriasActuales = ganadores.FirstOrDefault()?.VictoriasConsecutivas ?? 0;
                string decision = await EvaluarCampeon(victoriasActuales);

                if (decision == "Abortar") return;

                await ProcesarPerdedores(perdedores);

                if (decision == "Sentar")
                {
                    await ProcesarGanadoresCampeon(ganadores);
                }
                else
                {
                    bool ignorarRegla = decision == "Ignorar";
                    await ProcesarGanadoresNormales(ganadores, ganoEquipoA, ignorarRegla);
                }

                await CargarJugadores();
            }
            finally { IsBusy = false; }
        }

        private async Task<string> EvaluarCampeon(int victoriasActuales)
        {
            if (victoriasActuales + 1 < 2) return "Normal";

            var todos = await _db.GetJugadoresAsync();
            int totalPagados = todos.Count(j => j.HaPagado);

            if (totalPagados >= 15) return "Sentar";

            int faltantes = 15 - totalPagados;
            string accion = await _dialog.ShowActionSheetAsync(
                $"¡Atención! Faltan {faltantes} jugador(es) con pago al día para poder sentar al campeón (se necesitan 15 en total).",
                null,
                null,
                "Cancelar — Voy a anotar más jugadores",
                "Ignorar regla — Subir los 5 disponibles"
            );

            if (accion == "Cancelar — Voy a anotar más jugadores" || string.IsNullOrEmpty(accion))
            {
                await Shell.Current.GoToAsync("///MainPage");
                return "Abortar"; 
            }

            if (accion == "Ignorar regla — Subir los 5 disponibles")
            {
                await _dialog.ShowAlertAsync(
                    "Regla ignorada",
                    "Las victorias del campeón se reinician a 0. El perdedor va a la fila y suben los 5 disponibles.",
                    "Entendido"
                );
                return "Ignorar";
            }

            return "Normal";
        }

        private async Task ProcesarGanadoresNormales(IEnumerable<Jugador> ganadores, bool ganoEquipoA, bool ignorarRegla)
        {
            foreach (var g in ganadores)
            {
                if (ignorarRegla) g.VictoriasConsecutivas = 0;
                else g.VictoriasConsecutivas++;

                await _db.SaveJugadorAsync(g);
            }

            if (ganoEquipoA) EquipoB.Clear();
            else EquipoA.Clear();

            if (EquipoCampeonEspera.Count == 5)
            {
                foreach (var c in EquipoCampeonEspera)
                {
                    c.EstaEnCancha = true;
                    if (EquipoA.Count < 5) EquipoA.Add(c);
                    else EquipoB.Add(c);
                    await _db.SaveJugadorAsync(c);
                }
                EquipoCampeonEspera.Clear();
            }
            else
            {
                await PrepararDraft();
                await Shell.Current.GoToAsync("///DraftPage");
            }
        }

        private async Task ProcesarGanadoresCampeon(IEnumerable<Jugador> ganadores)
        {
            EquipoCampeonEspera.Clear();
            foreach (var g in ganadores)
            {
                g.EstaEnCancha = false;
                g.VictoriasConsecutivas = 0;
                await _db.SaveJugadorAsync(g);
                EquipoCampeonEspera.Add(g);
            }

            EquipoA.Clear();
            EquipoB.Clear();

            await IniciarDraft();
        }

        [RelayCommand]
        public async Task IniciarDraft()
        {
            ListaDraft.Clear();

            var lista = await _db.GetJugadoresAsync();
            var idsCampeon = EquipoCampeonEspera.Select(c => c.Id).ToList();

            var disponibles = lista.Where(j => !j.EstaEnCancha && !idsCampeon.Contains(j.Id) && j.HaPagado) 
                                   .OrderBy(j => j.HoraRegistro)
                                   .Take(10);

            foreach (var j in disponibles) ListaDraft.Add(j);

            await Shell.Current.GoToAsync("///DraftPage");
        }

        private async Task ProcesarPerdedores(IEnumerable<Jugador> perdedores)
        {
            DateTime ahora = DateTime.Now;
            var lista = perdedores.OrderBy(p => p.HoraRegistro).ToList();

            for (int i = 0; i < lista.Count; i++)
            {
                var p = lista[i];
                p.EstaEnCancha = false;
                p.VictoriasConsecutivas = 0;
                p.HoraRegistro = ahora.AddMilliseconds(i);
                await _db.SaveJugadorAsync(p);
            }
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
            bool confirmar = await _dialog.ShowConfirmAsync(
                "Eliminar jugador",
                $"¿Seguro que quieres eliminar a {j.Nombre} de la lista?",
                "Sí, eliminar",
                "Cancelar"
            );

            if (!confirmar) return;

            await _db.DeleteJugadorAsync(j);
            await CargarJugadores();
        }

        [RelayCommand]
        public async Task ReiniciarCancha()
        {
            bool confirmar = await _dialog.ShowConfirmAsync("Reiniciar", "¿Quieres vaciar la cancha y mandar a todos a espera?", "Sí", "No");

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
            await _dialog.ShowAlertAsync("Listo", "Cancha vacía.", "OK");
        }
    }
}