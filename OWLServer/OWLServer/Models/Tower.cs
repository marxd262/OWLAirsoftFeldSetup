namespace OWLServer.Models
{
    public class Tower
    {
        public string ID { get; set; }
        public string IP { get; set; }
        public string Name { get; set; }
        public TeamColor CurrentColor {  get; set; }

        public double Multiplier { get; set; } = 1.0;
        
        public bool IsLocked { get; set; }
        public bool IsControlled { get; set; }
        public bool IsForControlling { get; set; }
        public string ControllingTowerId { get; set; }
        public bool IsPressed { get; set; }
        public TeamColor PressedByColor { get; set; } = TeamColor.NONE;
        public double CaptureProgress { get; set; } = 0.0;
        
        public DateTime? LastPressed { get; set; } 
        
        public Tower(string id, string ip)
        {
            ID = id;
            IP = ip;
            Name = string.Empty;
        }

        public void Reset()
        {
            CurrentColor = TeamColor.NONE;
        }
    }
}
