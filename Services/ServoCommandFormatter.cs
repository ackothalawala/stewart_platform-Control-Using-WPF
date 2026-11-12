namespace stewart_platform.Services
{
    // Preserves the existing six-value protocol: integer hundredths of degrees.
    public static class ServoCommandFormatter
    {
        public static string Format(StewartPlatform platform)
        {
            string dataStr = "";
            for (int i = 0; i < 6; i++)
            {
                int val = (int)(platform.GetAlphaDegree(i) * 100);
                dataStr += val.ToString();
                if (i < 5) dataStr += ",";
            }
            return dataStr;
        }
    }
}
