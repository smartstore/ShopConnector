
namespace SmartStore.ShopConnector.Services
{
    public static class ShopConnectorCore
    {
        public static int ConnectorVersion => 3;
        public static int DefaultTimePeriodMinutes => 15;

        /// <remarks>see http://tools.ietf.org/html/rfc6648</remarks>
        public static class Header
        {
            private static string Prefix => "Sm-ShopConnector-";

            public static string Date => Prefix + "Date";
            public static string PublicKey => Prefix + "PublicKey";
            public static string Version => Prefix + "Version";
            public static string AuthResultId => Prefix + "AuthResultId";
            public static string AuthResultDescription => Prefix + "AuthResultDesc";
            public static string WwwAuthenticate => "SmNetShopConnectorHmac1";
        }
    }


    //public enum ConnectionLabelNotes
    //{
    //	NewData = 0
    //}

    public enum ShopConnectorAuthResult
    {
        Success = 0,
        FailedForUnknownReason,
        ConnectorUnavailable,
        ExportDeactivated,
        InvalidAuthorizationHeader,
        InvalidSignature,
        InvalidTimestamp,
        TimestampOutOfPeriod,
        TimestampOlderThanLastRequest,
        MissingMessageRepresentationParameter,
        ContentMd5NotMatching,
        ConnectionUnknown,
        ConnectionDisabled,
        ConnectionInvalid,
        IncompatibleVersion
    }
}
