﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.Util
{
    public static class Extensions
    {
        public static string ToHexString(this byte[] ba)
        {
            return BitConverter.ToString(ba).Replace("-", "");
        }
    }
}
