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
            Console.WriteLine(color);
            Task.Run(() => ExternalTriggerService.InvokeKlickerPressed(color));
            return Ok();
        }

        [HttpPost("RegisterTower")]
        public ActionResult RegisterTower(string id, string ip)
        {
            Task.Run(() => GameStateService.TowerManagerService.RegisterTower(id, ip));
            return Ok();
        }

        [HttpPost("CaptureTower")]
        public ActionResult CaptureTower(string id, TeamColor color)
        {
            Task.Run(() => GameStateService.TowerManagerService.TowerChangeColor(id, color));
            return Ok();
        }

        [HttpPost("TowerButtonPressed")]
        public ActionResult TowerButtonPressed(string id, TeamColor color)
        {
            Task.Run(() => GameStateService.TowerManagerService.HandleTowerButtonPressed(id, color));
            return Ok();
        }
        
        [HttpPost("TowerButtonReleased")]
        public ActionResult TowerButtonReleased(string id)
        {
            Task.Run(() => GameStateService.TowerManagerService.HandleTowerButtonReleased(id));
            return Ok();
        }
    }
}