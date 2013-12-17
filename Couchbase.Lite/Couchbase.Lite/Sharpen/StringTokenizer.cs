using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Sharpen {

    public class StringTokenizer {

        IEnumerable<String> Tokens { get; set; }

        int CurrentTokenIndex { get; set; }

        public StringTokenizer (String source, String tokenSeparator)
        {
            Debug.Assert(source != null);
            Debug.Assert(tokenSeparator != null);

            Tokens = source.Split(new[] { tokenSeparator }, StringSplitOptions.None);
        }

        public String NextToken() {
            var token = Tokens.ElementAt(CurrentTokenIndex);
            CurrentTokenIndex++;
            return token;
        }
    }
}

