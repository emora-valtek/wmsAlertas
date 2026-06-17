namespace WMS.Alertas.Global
{
    public static class Encryption
    {
        public static string Encrypt(string texto)
        {
            try
            {
                string claveEncriptada = "";
                const string aux1 = @"ABCDEFGHIJKLMNÑOPQRSTUVWXYZabcdefghijklmnñopqrstuvwxyz1234567890/;=.\<>_ " + "\"" + "'-";
                const string aux2 = @"zaq1xsw2cde3vfr4bgt5nhy6mju7ki8lo9ñp0ZAQXSWCDEVFRBGTNHYMJUKILOÑP&?$#!%(¿_" + ")" + "@*";
                int x = 0;

                for (x = 0; x < texto.Length; x++)
                {
                    claveEncriptada += aux2[aux1.IndexOf(texto[x])].ToString();
                }

                return claveEncriptada;
            }
            catch (Exception error)
            {
                throw new Exception("Error - " + error.Message, error);
            }
        }

        public static string Decrypt(string texto)
        {
            try
            {
                string claveEncriptada = "";
                const string aux1 = @"ABCDEFGHIJKLMNÑOPQRSTUVWXYZabcdefghijklmnñopqrstuvwxyz1234567890/;=.\<>_ ," + "\"" + "'-";
                const string aux2 = @"zaq1xsw2cde3vfr4bgt5nhy6mju7ki8lo9ñp0ZAQXSWCDEVFRBGTNHYMJUKILOÑP&?$#!%(¿_," + ")" + "@*";
                int x = 0;

                for (x = 0; x < texto.Length; x++)
                {
                    claveEncriptada += aux1[aux2.IndexOf(texto[x])].ToString();
                }

                return claveEncriptada;
            }
            catch (Exception error)
            {
                throw new Exception("Error - " + error.Message, error);
            }
        }

        private class FirmaValtek
        {
            public static string PlainText = string.Empty;
            public static string PassPhrase = "Valtek2";
            public static string SaltValue = "1943";
            public static string HashAlgorithm = "SHA1";
            public static int PasswordIterations = 3491;
            public static string InitVector = "1234567890ABCDEF";
            public static int KeySize = 256;
        }
    }
}
