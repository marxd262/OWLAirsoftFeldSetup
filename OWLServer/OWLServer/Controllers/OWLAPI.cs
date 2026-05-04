using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc;
using OWLServer.Models;
using OWLServer.Services.Interfaces;

namespace OWLServer.Controllers
{
    [ApiController]
    [Microsoft.AspNetCore.Mvc.Route("api")]
    public class OWLAPI : Controller
    {
        IGameStateService GameStateService { get; set; }

        IExternalTriggerService ExternalTriggerService { get; set; }

        public OWLAPI(IExternalTriggerService externalTriggerService, IGameStateService gameStateService)
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
            ExternalTriggerService.InvokeKlickerPressed(color);
            return Ok();
        }

        [HttpPost("RegisterTower")]
        public ActionResult RegisterTower(string id, string ip)
        {
            GameStateService.TowerManagerService.RegisterTower(id, ip);
            return Ok();
        }

        [HttpPost("CaptureTower")]
        public ActionResult CaptureTower(string id, TeamColor color)
        {
            GameStateService.TowerManagerService.TowerChangeColor(id, color);
            return Ok();
        }

        [HttpPost("TowerButtonPressed")]
        public ActionResult TowerButtonPressed(string id, TeamColor color)
        {
            GameStateService.TowerManagerService.HandleTowerButtonPressed(id, color);
            return Ok();
        }
        
        [HttpPost("TowerButtonReleased")]
        public ActionResult TowerButtonReleased(string id)
        {
            GameStateService.TowerManagerService.HandleTowerButtonReleased(id);
            return Ok();
        }
    }
}