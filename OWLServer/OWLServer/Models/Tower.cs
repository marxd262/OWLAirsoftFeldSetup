using System.Drawing;
using System.Net.Http.Headers;
using OWLServer.Services;

namespace OWLServer.Models
{
    public class Tower
    {
        public string ID { get; set; }
        public string IP { get; set; }
        public string Name { get; set; }
        public string DisplayLetter { get; set; }
        public TeamColor CurrentColor { get; set; }

        private HttpClient _client = new HttpClient();

        public int TimeToCaptureInSeconds { get; set; } = 5;

        public double Multiplier { get; set; } = 1.0;

        public bool TowerOnline = false;
        public DateTime LastPing { get; set; }

        public bool IsLocked { get; set; }
        public bool IsControlled { get; set; }
        public string? IsControlledByID { get; set; }
        public bool IsForControlling => ControllsTowerID.Any();
        public List<string> ControllsTowerID { get; set; } = new();
        public int ResetsAfterInSeconds { get; set; } = 60;
        public DateTime? CapturedAt { get; set; }
        public bool IsPressed { get; set; }
        public TeamColor PressedByColor { get; set; } = TeamColor.NONE;
        public double CaptureProgress { get; set; } = 0.0;

        public DateTime? LastPressed { get; set; }

        public String GetHTMLColor => ColorTranslator.ToHtml(Util.TeamColorToColorTranslator(CurrentColor));

        public Tower(string id, string ip)
        {
            ID = id;
            IP = ip;
            Name = string.Empty;

            var builder = new UriBuilder(ip);
            _client.BaseAddress = builder.Uri;
            _client.DefaultRequestHeaders.Accept.Clear();
            _client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async void SetTowerColor(TeamColor color)
        {
            CurrentColor = color;
            SendColorToTower(Util.TeamColorToColorTranslator(color));
        }

        public void SetToStartColor()
        {
            if (IsControlled)
            {
                SetTowerColor(TeamColor.LOCKED);
            }
            else
            {
                SetTowerColor(TeamColor.NONE);
            }
        }

        public Color MapColor()
        {
            if (CaptureProgress >= .99)
                return ColorTranslator.FromHtml(CurrentColor.ToString());

            return ColorTranslator.FromHtml(CurrentColor.ToString());
        }

        public async void SendColorToTower(Color color)
        {
            try
            {
                string c = $"{color.R.ToString()}/{color.G.ToString()}/{color.B.ToString()}";
                string callURL = $"/api/setcolor/{c}";
                HttpResponseMessage response = await _client.PostAsync(callURL, null);
            }
            catch
            {
            }
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
            SetTowerColor(TeamColor.NONE);
        }
    }
}