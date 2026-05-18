using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace WebAPI
{
    public class AuthOptions
    {
        public const string ISSUER = "WebAPI_App_Server";
        public const string AUDIENCE = "WebAPI_App_Client";
        public const string KEY = "91029D46-1F6A-4A95-800B-DC95C2960D06";
        public const int LIFETIME = 480;

        public static SymmetricSecurityKey GetSymmetricSecurityKey()
        {
            return new SymmetricSecurityKey(Encoding.ASCII.GetBytes(KEY));
        }
    }
}