using UnityEngine;

using System.Security.Cryptography.X509Certificates;
using UnityEngine.Networking;
using System.IO;
using JetBrains.Annotations;

namespace RegressionGames
{
    // tls/ssl seems to not be working correctly on some versions, so use this instead for now.
    class RGCertOnlyPublicKey : CertificateHandler
    {
        private X509Certificate2 RG_CERT;

        [CanBeNull] private static RGCertOnlyPublicKey _this = null;
        
        public static RGCertOnlyPublicKey GetInstance()
        {
            if (_this == null)
            {
                _this = new RGCertOnlyPublicKey();
            }

            return _this;
        }

        private RGCertOnlyPublicKey()
        {
            var certFile = Path.GetFullPath("Packages/gg.regression.unity.bots/Runtime/Resources/regression_cert.cer");
            RG_CERT = new X509Certificate2(certFile);
        }

        protected override bool ValidateCertificate(byte[] certificateData)
        {
            X509Certificate2 certificate = new X509Certificate2(certificateData);
            string pk = RG_CERT.GetPublicKeyString();
            string ck = certificate.GetPublicKeyString();
            return pk.Equals(ck);
        }
    }
}