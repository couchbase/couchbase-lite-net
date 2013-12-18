using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Sharpen {

    public class StringTokenizer
    {

        String[] Tokens { get; set; }

        int CurrentTokenIndex { get; set; }

        public StringTokenizer (String source, String tokenSeparator)
        {
            Debug.Assert(source != null);
            Debug.Assert(tokenSeparator != null);

            Tokens = source.Split(new[] { tokenSeparator }, StringSplitOptions.None);
        }

        public String NextToken()
        {
            var token = Tokens[CurrentTokenIndex];

            if (CurrentTokenIndex < Tokens.GetUpperBound(0))
                CurrentTokenIndex++;

            return token;
        }
    }
}

