using System;
using System.Globalization;

namespace Sharpen {

    public static class StringExtensions {

        public static int CompareToIgnoreCase(this String a, String b) {
            return String.Compare(a, b, true, CultureInfo.InvariantCulture);
        }
    }
}

