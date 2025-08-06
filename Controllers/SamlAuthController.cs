using awsdummy.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace awsdummy.Controllers
{

    [ApiController]
    [Route("api/auth")]
    public class SamlAuthController : Controller //Base
    {
        private readonly ILogger<SamlAuthController> _logger;
        private readonly JwtService _jwtService;



        public SamlAuthController(
            ILogger<SamlAuthController> logger,
            JwtService jwtService)
        {
            _logger = logger;
            _jwtService = jwtService;

        }
        [AllowAnonymous]
        [HttpGet("loginusingAws")]
        public IActionResult LoginWithAws()
        {
            return Challenge(new AuthenticationProperties
            {
                RedirectUri = "https://localhost:7191/api/auth/callbackaws" // AWS SAML callback
            }, "Saml2Aws"); // 🔑 Use a separate scheme for AWS (configured in Program.cs)
        }
        [AllowAnonymous]
        [HttpGet("callbackaws")]
        public async Task<IActionResult> AwsCallback()
        {
            _logger.LogInformation("Authenticating SAML callback (AWS)...");
            var principal = HttpContext.User;
            var subject = principal.Claims;

            var email = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? principal.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value;

            


            var firstname = principal.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname")?.Value
                          ?? principal.FindFirst("Given Name")?.Value;

            _logger.LogInformation("AWS SAML claims received: {Claims}", string.Join(", ", principal.Claims.Select(c => $"{c.Type} = {c.Value}")));
            _logger.LogInformation($"Principal has claims: {principal.Claims.Any()}");
            foreach (var claim in principal.Claims)
            {
                _logger.LogInformation($"  ↳ {claim.Type} : {claim.Value}");
            }

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(firstname))
            {
                _logger.LogWarning("Missing required claims (email or firstname).");
                return BadRequest("Missing required claims.");
            }
            var jwtClaims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, firstname),
        new Claim(ClaimTypes.Email, email)
      
    };

            var token = _jwtService.GenerateToken(jwtClaims);

            return Ok(new
            {
                Token = token,
                Message = $"Welcome {firstname} {email}!"
            });
        }
    }
}
