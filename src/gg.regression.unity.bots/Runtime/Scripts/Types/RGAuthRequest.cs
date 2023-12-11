using System;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGAuthRequest
    {
        public string email;
        public string password;

        public RGAuthRequest( string email, string password)
        {
            this.email = email;
            this.password = password;
        }
    }
}

