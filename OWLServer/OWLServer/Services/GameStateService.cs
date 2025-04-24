namespace OWLServer.Services
{
    public class GameStateService
    {
        public int PointsTeamGreen { get; set; } = 0;

        public void AddPoints()
        {
            PointsTeamGreen++;
            
        }
    }
}
