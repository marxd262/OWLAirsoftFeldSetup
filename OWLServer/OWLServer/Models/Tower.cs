namespace OWLServer.Models
{
    public class Tower
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public enums.TeamColor CurrentColor {  get; set; }

        public Tower(int id)
        {
            ID = id;
        }

        public void Reset()
        {
            CurrentColor = enums.TeamColor.none;
        }
    }
}
