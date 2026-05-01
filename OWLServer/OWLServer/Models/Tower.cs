using System.ComponentModel.DataAnnotations.Schema;
using System.Drawing;
using OWLServer.Services;
using OWLServer.Services.Interfaces;

namespace OWLServer.Models
{
    public class Tower
    {
        public int Id { get; set; }
        
        public string MacAddress { get; set; }
        
        public string IP { get; set; }
        
        public string Name { get; set; }
       
        public string DisplayLetter { get; set; }

        public int TimeToCaptureInSeconds { get; set; } = 5;

        public double Multiplier { get; set; } = 1.0;
        
        public int ResetsAfterInSeconds { get; set; } = 60;
        
        public ElementLocation? Location { get; set; }

        [NotMapped]
        public bool TowerOnline = false;

        [NotMapped]
        public DateTime LastPing { get; set; }

        [NotMapped]
        public bool IsLocked => CurrentColor == TeamColor.LOCKED;

        [NotMapped]
        public DateTime? CapturedAt { get; set; }

        [NotMapped]
        public int ResetSecondsRemaining =>
            CapturedAt.HasValue
                ? Math.Max(0, ResetsAfterInSeconds - (int)(DateTime.Now - CapturedAt.Value).TotalSeconds)
                : -1;

        [NotMapped]
        public bool IsPressed { get; set; }

        [NotMapped]
        public TeamColor PressedByColor { get; set; } = TeamColor.NONE;

        [NotMapped]
        public double CaptureProgress { get; set; } = 0.0;

        [NotMapped]
        public DateTime? LastPressed { get; set; }

        [NotMapped]
        public TeamColor CurrentColor { get; set; }

        [NotMapped]
        public String GetHTMLColor => ColorTranslator.ToHtml(Util.TeamColorToColorTranslator(CurrentColor));

        private ITowerHttpClient? _httpClient;


        public Tower() { } // Braucht man für DB

        public Tower(string id, string ip, ITowerHttpClient client)
        {
            MacAddress = id;
            IP = ip;
            Name = string.Empty;
            _httpClient = client;
        }

        public Tower(string id, string ip)
        {
            MacAddress = id;
            IP = ip;
            Name = string.Empty;
        }

        public string DisplaycolorAsHTML()
        {
            TeamColor c;

            if (IsPressed) c = PressedByColor;
            else c = CurrentColor;

            if (c == TeamColor.BLUE || c == TeamColor.RED)
                return Util.HTMLColorForTeam(c);
            return ColorTranslator.ToHtml(Util.TeamColorToColorTranslator(c));
        }

        public int GetDisplayProgress()
        {
            if (IsPressed)
                return (int)(CaptureProgress * 100);
            else
                return 100;
        }


        public void SetTowerColor(TeamColor color)
        {
            CurrentColor = color;
            SendColorToTower(Util.TeamColorToColorTranslator(color));
        }

        public void SetToStartColor()
        {
            SetTowerColor(TeamColor.NONE);
        }

        public Color MapColor()
        {
            return ColorTranslator.FromHtml(CurrentColor.ToString());
        }

        public async Task SendColorToTower(Color color)
        {
            if (_httpClient == null) return;
            try
            {
                string c = $"{color.R}/{color.G}/{color.B}";
                await _httpClient.PostAsync($"/api/setcolor/{c}", null);
            }
            catch { }
        }

        public async Task PingTower()
        {
            if (_httpClient == null) return;
            try
            {
                var response = await _httpClient.PingAsync("/api/ping");
                TowerOnline = response.IsSuccessStatusCode;
                LastPing = DateTime.Now;
            }
            catch { }
        }


        public void Reset()
        {
            CapturedAt = null;
            IsPressed = false;
            PressedByColor = TeamColor.NONE;
            CaptureProgress = 0;
            LastPressed = null;
            SetToStartColor();
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(DisplayLetter))
            {
                return $"Tower: {DisplayLetter}";
            }
            return $"Tower: {MacAddress}";
        }
    }
}