using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;

namespace BiometricService.Controllers
{
    [ApiController]
    [Route("apiservice/")]
    public class APIController : ControllerBase
    {
        private readonly Biometric _biometric;

        public APIController(Biometric biometric)
        {
            _biometric = biometric;
        }

        [HttpGet("capture-hash")]
        public IActionResult Capture()
        {
            return _biometric.CaptureHash();
        }

        [HttpPost("match-one-on-one")]
        public IActionResult MatchOneOnOne([FromBody] JsonObject template)
        {
            return _biometric.IdentifyOneOnOne(template);
        }

        [HttpGet("identification")]
        public IActionResult Identification()
        {
            return _biometric.Identification();
        }

        [HttpPost("load-to-memory")]
        public IActionResult LoadToMemory([FromBody] JsonArray fingers)
        {
            return _biometric.LoadToMemory(fingers);
        }

        [HttpGet("delete-all-from-memory")]
        public IActionResult DeleteAllFromMemory()
        {
            return _biometric.DeleteAllFromMemory();
        }

        [HttpGet("total-in-memory")]
        public IActionResult TotalIdsInMemory()
        {
            return _biometric.TotalIdsInMemory();
        }

        [HttpGet("device-unique-id")]
        public IActionResult DeviceUniqueSerialID()
        {
            return _biometric.DeviceUniqueSerialID();
        }

        [HttpGet("capture")]
        public IActionResult Snapshot()
        {
            return _biometric.Snapshot();
        }
    }
    }
}