using BasketManager.Models;
using SQLite;

namespace BasketManager.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection _database;

        async Task Init()
        {
            if (_database is not null)
                return;

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "Prueba20.db3");

            _database = new SQLiteAsyncConnection(dbPath);

            await _database.CreateTableAsync<Jugador>();
        }

        public async Task<List<Jugador>> GetJugadoresAsync()
        {
            await Init();
            return await _database.Table<Jugador>()
                                  .OrderBy(j => j.HoraRegistro)
                                  .ToListAsync();
        }

        public async Task<int> SaveJugadorAsync(Jugador jugador)
        {
            await Init();
            if (jugador.Id != 0)
                return await _database.UpdateAsync(jugador); 
            else
                return await _database.InsertAsync(jugador); 
        }

        public async Task<int> DeleteJugadorAsync(Jugador jugador)
        {
            await Init();
            return await _database.DeleteAsync(jugador);
        }

        public async Task ClearAllAsync()
        {
            await Init();
            await _database.DeleteAllAsync<Jugador>();
        }
    }
}
