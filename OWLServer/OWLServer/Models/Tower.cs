namespace OWLServer.Models
{
    public class Tower
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public TeamColor CurrentColor {  get; set; }

        public double Multiplier { get; set; } = 1.0;

        public Tower(int id)
        {
            ID = id;
            Name = string.Empty;
        }

        public void Reset()
        {
            CurrentColor = TeamColor.NONE;
        }
    }
}
