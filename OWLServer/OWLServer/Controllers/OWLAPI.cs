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
        GameStateService GameStateService { get; set; }

        ExternalTriggerService ExternalTriggerService { get; set; }

        public OWLAPI(ExternalTriggerService externalTriggerService, GameStateService gameStateService)
        {
            GameStateService = gameStateService;
            ExternalTriggerService = externalTriggerService;
        }

        [HttpGet("ping")]
        public string Get()
        {
            return "pong";
        }

        [HttpPost("KlickerClicked")]
        public ActionResult KlickerClicked(TeamColor color)
        {
            ExternalTriggerService.InvokeKlickerPressed(color);
            return Ok();
        }

        [HttpPost("RegisterTower")]
        public ActionResult RegisterTower(int id)
        {
            GameStateService.TowerManagerService.RegisterTower(id);
            return Ok();
        }

        [HttpPost("CaptureTower")]
        public ActionResult CaptureTower(int id, TeamColor color)
        {
            GameStateService.TowerManagerService.TowerChangeColor(id, color);
            return Ok();
        }
    }
}