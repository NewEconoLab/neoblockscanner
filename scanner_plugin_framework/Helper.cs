using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public class Helper
{
    static public string ToHexString(byte[] data)
    {
        StringBuilder sb = new StringBuilder();
        foreach (var d in data)
        {
            sb.Append(d.ToString("x02"));
        }
        return sb.ToString();
    }
    static public byte[] HexToString(string text)
    {
        if (text.IndexOf("0x") == 0)
            text = text.Substring(2);
        var bytes = new byte[text.Length / 2];
        for(var i=0;i<text.Length/2;i++)
        {
            bytes[i] = byte.Parse(text.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber);
        }
        return bytes;
    }
    static public bool BytesEqual(byte[] a,byte[] b)
    {
        if (a.Length != b.Length)
            return false;
        for(var i=0;i<a.Length;i++)
        {
            if (a[i] != b[i])
                return false;
        }
        return true;
    }
}
