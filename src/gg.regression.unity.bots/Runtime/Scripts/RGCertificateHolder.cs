﻿using System.IO;
using System.Security.Cryptography.X509Certificates;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Networking;

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
            var certAsset = Resources.Load<TextAsset>("regression_cert");
            RG_CERT = new X509Certificate2(certAsset.bytes);
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