using Microsoft.AspNetCore.Mvc;
using OWLServer.Services;
using System.Threading.Tasks;

namespace OWLServer.Controllers
{
    [ApiController]
    [Route("api")]
    public class OWLAPI : Controller
    {

        protected GameStateService GSS {get; set;}

        public OWLAPI(GameStateService gss)
        {
            GSS = gss;
        }

        [HttpGet("ping")]
        public string Get()
        {
            return "pong";
        }

        [HttpPost("SetTeam")]
        public int SetCurrentTeam()
        {
            GSS.AddPoints();
            return GSS.PointsTeamGreen;
        }
    }
}
