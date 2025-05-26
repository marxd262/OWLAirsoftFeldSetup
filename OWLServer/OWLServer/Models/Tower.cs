using System.Net.Http.Headers;

namespace OWLServer.Models
{
    public class Tower
    {
        public string ID { get; set; }
        public string IP { get; set; }
        public string Name { get; set; }
        public TeamColor CurrentColor {  get; set; }
        
        private HttpClient _client = new HttpClient();

        public double Multiplier { get; set; } = 1.0;

        public bool TowerOnline = false;
        public DateTime LastPing { get; set; }
        
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
            
            _client.BaseAddress = new Uri(ip);
            _client.DefaultRequestHeaders.Accept.Clear();
            _client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async void SetTowerColer(TeamColor color)
        {
            CurrentColor = color;

            string callURL = $"/api/setcolor?color={color.ToString()}";
            HttpResponseMessage response = await _client.PostAsync(callURL, null);
        }
        
        public async void PingTower()
        {

            string callURL = $"/api/ping";
            HttpResponseMessage response = await _client.PostAsync(callURL, null);

            TowerOnline = response.IsSuccessStatusCode;
            LastPing = DateTime.Now;
        }

        public void Reset()
        {
            SetTowerColer(TeamColor.NONE);
        }
    }
}
