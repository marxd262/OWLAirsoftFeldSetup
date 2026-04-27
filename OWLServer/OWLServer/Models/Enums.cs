namespace OWLServer.Models
{
    public enum TeamColor
    {
        NONE = -1,
        RED,
        BLUE,
        OFF,
        LOCKED
    }

    public enum GameMode
    {
        None,
        TeamDeathMatch,
        Conquest,
        Timer,
        ChainBreak,
        CaptureTheFlag,
        Bomb,
    }
    
    public enum Sounds{
        Start,
        Stop,
        Countdown,
        Freeze
    }
}
