public static class GuidV7
{
    public static Guid NewGuid()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var randomBytes = new byte[10];
        new Random().NextBytes(randomBytes);

        var guidBytes = new byte[16];
        var timestampBytes = BitConverter.GetBytes(timestamp);

        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(timestampBytes);
        }

        Array.Copy(timestampBytes, 2, guidBytes, 0, 6);
        Array.Copy(randomBytes, 0, guidBytes, 6, 10);

        // Set the version to 7
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x70);

        // Set the variant to RFC 4122
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);

        return new Guid(guidBytes);
    }
}
