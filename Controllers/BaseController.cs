using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Authentication;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace ExperienceService.Controllers
{
    public class BaseController : Controller
    {
        protected string AccessToken { get; private set; }
        protected DateTime AccessTokenExpiresAt { get; private set; }
        protected string IdToken { get; private set; }
        protected string UserId { get; private set; }
        protected string TenantId { get; private set; }

        private readonly IConfiguration _configuration;
        public BaseController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (this.HttpContext.User.Identity.IsAuthenticated)
            {
                this.AccessToken = this.HttpContext.GetTokenAsync("access_token").Result;

                // if you need to check the access token expiration time, use this value
                // provided on the authorization response and stored.
                // do not attempt to inspect/decode the access token
                this.AccessTokenExpiresAt = DateTime.Parse(
                    this.HttpContext.GetTokenAsync("expires_at").Result,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind);
                var handler = new JwtSecurityTokenHandler();
                var tokenS = handler.ReadToken(this.AccessToken) as JwtSecurityToken;
                this.TenantId = tokenS.Claims.First(claim => claim.Type == _configuration["Auth0:TenantId"]).Value;                
                this.IdToken = this.HttpContext.GetTokenAsync("id_token").Result;
                this.UserId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value;
                // Now you can use them. For more info on when and how to use the
                // access_token and id_token, see https://auth0.com/docs/tokens
            }
            base.OnActionExecuting(context);
        }
    }
}