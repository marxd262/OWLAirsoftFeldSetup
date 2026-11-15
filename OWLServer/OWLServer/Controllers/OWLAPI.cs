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
        public ActionResult KlickerClicked(TeamColor? color, string? spawn)
        {
            // a clicker is mounted in a spawn, so prefer its spawn: the team there changes when sides are swapped
            var team = spawn != null ? GameStateService.TeamAtSpawn(spawn) : color ?? TeamColor.NONE;
            if (team is not (TeamColor.RED or TeamColor.BLUE)) return BadRequest();

            Console.WriteLine(team);
            ExternalTriggerService.InvokeKlickerPressed(team);
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