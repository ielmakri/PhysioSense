using System;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using System.Globalization;
using System.Timers;

public static class FileUtility
{
    public static void ReplaceInFile(string filename, int numberOfLinesToCheck, string oldString, string newString)
    {

        using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.ReadWrite))
        {
            long endOfTimePosition = FindLineWithOldString(fs, oldString, numberOfLinesToCheck);

            if (endOfTimePosition != -1)
            {
                fs.Seek(endOfTimePosition, 0);
                byte[] b = null;
                b = ReadNextLine(fs);
                string line = new string(b.Select(x => (char)x).ToArray());
                line = line.Replace(oldString, newString);
                b = line.ToCharArray().Select(x => (byte)x).ToArray();

                fs.Seek(endOfTimePosition, 0);
                fs.Write(b, 0, b.Length);
            }
        }
    }

    private static string ConvertByteArrayToString(byte[] byteArray)
    {
        return new string(byteArray.Select(x => (char)x).ToArray());
    }

    private static long FindLineWithOldString(FileStream fs, string oldString, int numberOfLinesToCheck)
    {
        int lineCount = 1;
        bool found = false;
        long previousPosition = 0;

        fs.Seek(0, 0);

        while (found == false && lineCount < numberOfLinesToCheck)
        {
            string line = ConvertByteArrayToString(ReadNextLine(fs));

            if (line.StartsWith(oldString))
            {
                // Found the line, return the start position.
                return previousPosition;
            }

            lineCount++;
            previousPosition = fs.Position;
        }

        // Nothing found.
        return -1;
    }


    private static byte[] ReadNextLine(FileStream fs)
    {
        byte[] nl = new byte[] { (byte)Environment.NewLine[0], (byte)Environment.NewLine[1] };
        List<byte> ll = new List<byte>();
        bool lineFound = false;

        while (!lineFound && fs.Position < fs.Length)
        {
            byte b = (byte)fs.ReadByte();
            if ((int)b == -1) break;

            ll.Add(b);
            if (b == nl[0])
            {
                b = (byte)fs.ReadByte();
                ll.Add(b);

                if (b == nl[1])
                {
                    lineFound = true;
                }
            }
        }
        return ll.Count == 0 ? null : ll.ToArray();
    }

}