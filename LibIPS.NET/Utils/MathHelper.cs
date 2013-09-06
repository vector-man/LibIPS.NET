using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodeIsle.Utils
{
    class MathHelper
    {
        public static long Clamp(long value, long minimum, long maximum)
        {
            return (value < minimum) ? minimum : (value > maximum) ? maximum : value;
        }
    }
}
