using UnityEngine;

using System.Security.Cryptography.X509Certificates;
using UnityEngine.Networking;
using RegressionGames;
using System.Runtime.ConstrainedExecution;
using static Codice.Client.Common.Servers.RecentlyUsedServers;
using System.IO;

namespace RegressionGames
{
    // tls/ssl seems to not be working correctly on some versions, so use this instead for now.
    class RGCertOnlyPublicKey : CertificateHandler
    {
        private readonly static X509Certificate2 RG_CERT;

        static RGCertOnlyPublicKey()
        {
            if (RG_CERT == null) {
                var certFile = Path.GetFullPath("Packages/gg.regression.unity.bots/Runtime/Resources/regression_cert.cer");
                Debug.Log(certFile);
                RG_CERT = new X509Certificate2(certFile);
            }
        }

        protected override bool ValidateCertificate(byte[] certificateData)
        {
            X509Certificate2 certificate = new X509Certificate2(certificateData);
            string pk = certificate.GetPublicKeyString();
            Debug.Log("Validating Cert: "+ certificate.ToString());
            return pk.Equals(RG_CERT.GetPublicKeyString());
        }
    }
}