using System;
using System.IO;

namespace SmashNxTools
{
    public static class Util
    {
        public static void CopyStream(this Stream input, Stream output, long length)
        {
            const int bufferSize = 0x8000;
            long remaining = length;
            var buffer = new byte[bufferSize];

            int read;
            while ((read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining))) > 0)
            {
                output.Write(buffer, 0, read);
                remaining -= read;
            }
        }
    }
}
