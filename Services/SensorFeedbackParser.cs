namespace stewart_platform.Services
{
    public static class SensorFeedbackParser
    {
        // Return the four original raw fields so each value retains independent TryParse behavior.
        public static bool TryGetFields(string? line, out string[] parts)
        {
            parts = new string[0];
            if (string.IsNullOrEmpty(line) || !line.StartsWith("FB:")) return false;
            string cleanData = line.Substring(3).Trim();
            parts = cleanData.Split(',');
            return parts.Length == 4;
        }
    }
}
