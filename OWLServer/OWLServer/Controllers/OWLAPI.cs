using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc;
using OWLServer.Models;
using OWLServer.Services;

namespace OWLServer.Controllers
{
    [ApiController]
    [Microsoft.AspNetCore.Mvc.Route("api")]
    public class OWLAPI : Controller
    {
        [Inject]
        GameStateService GameStateService {get; set;} = null!;

        [Inject]
        ExternalTriggerService ExternalTriggerService {get; set;} = null!;


        [HttpGet("ping")]
        public string Get()
        {
            return "pong";
        }

        [HttpPost("KlickerClicked")]
        public void KlickerClicked(TeamColor color)
        {
            ExternalTriggerService.InvokeKlickerPressed(color);
        }
    }
}
