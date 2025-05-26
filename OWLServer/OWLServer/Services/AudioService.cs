using OWLServer.Models;

namespace OWLServer.Services;

public class AudioService
{
    private string GetFile(Sounds sound)
    {
        switch (sound)
        {
            case Sounds.Start:
                return "/wwwroot/sounds/GameStart.mp3";
            case Sounds.Stop:
                return "/wwwroot/sounds/GameOver.mp3";
            case Sounds.Countdown:
                return "";
            case Sounds.Freeze:
                return "";
            default:
                return "";
        }
    }

    public void PlaySound(Sounds sound)
    {
        string file = GetFile(sound);

        if (file != "")
        {
            // Add Play Audio
        }
    }
}