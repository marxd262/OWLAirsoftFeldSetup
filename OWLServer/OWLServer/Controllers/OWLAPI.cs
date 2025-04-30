using Microsoft.AspNetCore.Mvc;
using OWLServer.Models;
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
        public int SetCurrentTeam(TeamColor color)
        {
            return GSS.AddPoints(color, 1);
        }
    }
}
