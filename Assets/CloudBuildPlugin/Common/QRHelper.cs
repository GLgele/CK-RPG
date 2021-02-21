using System;
using UnityEngine;
using ZXing;
using ZXing.QrCode;

namespace UcbUtils
{
    public class QRHelper
    {
        public QRHelper()
        {

        }

        private static Color32[] Encode(string textForEncoding, int width, int height)
        {
            var writer = new BarcodeWriter
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Margin = 3,
                    Height = height,
                    Width = width
                }
            };
            return writer.Write(textForEncoding);
        }

        public static Texture2D generateQR(string text)
        {
            var encoded = new Texture2D(256, 256);
            var color32 = Encode(text, encoded.width, encoded.height);
            encoded.SetPixels32(color32);
            encoded.Apply();
            return encoded;
        }
    }
}
