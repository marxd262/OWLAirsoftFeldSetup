using System.ComponentModel.DataAnnotations.Schema;
using System.Drawing;
using System.Net.Http.Headers;
using OWLServer.Services;

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
        public bool IsLocked { get; set; }

        [NotMapped]
        public bool IsControlled { get; set; }

        [NotMapped]
        public string? IsControlledByID { get; set; }

        [NotMapped]
        public bool IsForControlling => ControllsTowerID.Any();

        [NotMapped]
        public List<string> ControllsTowerID { get; set; } = new();

        [NotMapped]
        public DateTime? CapturedAt { get; set; }

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

        private HttpClient _client = new HttpClient();


        public Tower() { } // Braucht man für DB

        public Tower(string id, string ip)
        {
            MacAddress = id;
            IP = ip;
            Name = string.Empty;

            var builder = new UriBuilder(ip);
            _client.BaseAddress = builder.Uri;
            _client.DefaultRequestHeaders.Accept.Clear();
            _client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
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
            try
            {
                string callURL = $"/api/ping";
                HttpResponseMessage response = await _client.PostAsync(callURL, null);

                TowerOnline = response.IsSuccessStatusCode;
                LastPing = DateTime.Now;
            }
            catch { }
        }


        public void Reset()
        {
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