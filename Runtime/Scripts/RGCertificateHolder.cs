using UnityEngine;

using System.Security.Cryptography.X509Certificates;
using UnityEngine.Networking;
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
                RGDebug.LogVerbose("RG Cert loaded"+certFile);
                RG_CERT = new X509Certificate2(certFile);
            }
        }

        protected override bool ValidateCertificate(byte[] certificateData)
        {
            X509Certificate2 certificate = new X509Certificate2(certificateData);
            string pk = certificate.GetPublicKeyString();
            RGDebug.LogVerbose("Comparing Cert: " + certificate.ToString());
            return pk.Equals(RG_CERT.GetPublicKeyString());
        }
    }
}