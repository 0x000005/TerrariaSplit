namespace TerrariaSplit.WorldGuard.Payload
{
    internal static class RaceUiRuntimeFailure
    {
        public const int ErrorCode = 70;

        public static bool TryResolve(
            string uiFailure,
            string runtimeFailure,
            out string message)
        {
            message = !string.IsNullOrWhiteSpace(uiFailure)
                ? uiFailure
                : runtimeFailure;
            if (string.IsNullOrWhiteSpace(message))
            {
                message = string.Empty;
                return false;
            }

            return true;
        }
    }
}
