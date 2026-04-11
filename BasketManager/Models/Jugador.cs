using SQLite;

namespace BasketManager.Models
{
    public class Jugador
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [MaxLength(100)]
        public string Nombre { get; set; }

        public bool HaPagado { get; set; }

        public DateTime HoraRegistro { get; set; }

        public int VictoriasConsecutivas { get; set; }

        public bool EstaEnCancha { get; set; }
        public bool EsGanador { get; set; }

        [Ignore]
        public string StatusColor => HaPagado ? "#1DB954" : "#FF0000";
    }
}
