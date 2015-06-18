//
//  BuiltinReduceFunctions.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Views
{

    /// <summary>
    /// Class containing the built in Reduce functions (e.g. count, sum, min) for use in creating
    /// views in Couchbase Lite
    /// </summary>
    public static class BuiltinReduceFunctions
    {

        #region Member Variables

        //For JSViewCompiler
        private static readonly Dictionary<string, ReduceDelegate> MAP = new Dictionary<string, ReduceDelegate>
        {
            //NOTE: None of these support rereduce! They'll need to be reimplemented when we add
            // rereduce support to View.
            { "count", Count },
            { "sum", Sum },
            { "min", Min },
            { "max", Max },
            { "average", Average },
            { "median", Median},
            { "stddev", StdDev },
            { "stats", Stats }
        };

        /// <summary>
        /// A function that counts the number of documents contained in the map
        /// </summary>
        public static readonly ReduceDelegate Count = (k, v, r) => v.Count();

        /// <summary>
        /// A function that adds all of the items contained in the map
        /// </summary>
        public static readonly ReduceDelegate Sum = (k, v, r) => View.TotalValues(v.ToList());

        /// <summary>
        /// A function that retrieves the minimum value in the map
        /// </summary>
        public static readonly ReduceDelegate Min = (k, v, r) => v.Min(x => DoubleValue(x));

        /// <summary>
        /// A function that retrieves the maximum value in the map
        /// </summary>
        public static readonly ReduceDelegate Max = (k, v, r) => v.Max(x => DoubleValue(x));

        /// <summary>
        /// A function that calculates the average of all the values in the map
        /// </summary>
        public static readonly ReduceDelegate Average = (k, v, r) => v.Average(x => DoubleValue(x));

        /// <summary>
        /// A function that calculates the median of all the values in the map
        /// </summary>
        public static readonly ReduceDelegate Median = (k, v, r) => CalculateMedian(v);

        /// <summary>
        /// A function that calculates the standard deviation for all the values in the map
        /// </summary>
        public static readonly ReduceDelegate StdDev = (k, v, r) => CalculateStdDev(v);

        /// <summary>
        /// A function that outputs various statistics about the map (count, sum, squared sum, min, and max)
        /// </summary>
        public static readonly ReduceDelegate Stats = (k, v, r) => CalcuateStats(v);

        #endregion

        #region Internal Methods

        //For JSViewCompiler
        internal static ReduceDelegate Get(string name) 
        {
            ReduceDelegate retVal = null;
            if (!MAP.TryGetValue(name, out retVal)) {
                return null;
            }

            return retVal;
        }

        internal static double TotalValues(IList<object> values)
        {
            double total = 0;
            foreach (object o in values)
            {
                try {
                    double number = Convert.ToDouble(o);
                    total += number;
                } catch (Exception e) {
                    Log.W(Database.TAG, "Warning non-numeric value found in totalValues: " + o, e);
                }
            }
            return total;
        }

        #endregion

        #region Private Methods

        private static double? DoubleValue(object o)
        {
            try {
                return Convert.ToDouble(o);
            } catch(Exception) {
                return null;
            }
        }

        private static double CalculateMedian(IEnumerable<object> input)
        {
            int length = input.Count();
            if (length == 0) {
                return 0.0;
            }

            double m = QuickSelect(input.Cast<double>().ToList(), 0, length - 1, length / 2);
            if ((length % 2) == 0) {
                m = (m + QuickSelect(input.Cast<double>().ToList(), 0, length - 1, length / 2 + 1)) / 2.0;
            }

            return m;
        }

        private static double CalculateStdDev(IEnumerable<object> vals)
        {
            var input = vals.Cast<double>();
            // Via <https://en.wikipedia.org/wiki/Standard_deviation#Rapid_calculation_methods>
            int length = input.Count();
            double a = 0.0, q = 0.0;
            for (int k = 1; k <= input.Count(); k++) {
                double x = input.ElementAt(k-1);
                double aOld = a;
                a += (x - a) / k;
                q += (x - aOld) * (x - a);
            }
            return Math.Sqrt(q / (length - 1));
        }

        // https://wiki.apache.org/couchdb/Built-In_Reduce_Functions#A_stats
        private static IDictionary<string, object> CalcuateStats(IEnumerable<object> values) {
            double sum=0, sumsqr=0, min=double.PositiveInfinity, max=double.NegativeInfinity;
            foreach(var value in values) {
                double? n = DoubleValue(value);
                if (n != null) {
                    sum += n.Value;
                    sumsqr += n.Value * n.Value;
                    min = Math.Min(min, n.Value);
                    max = Math.Max(max, n.Value);
                }
            }
            return new Dictionary<string, object> { 
                { "count", values.Count() },
                { "sum", sum },
                { "sumsqr", sumsqr },
                { "min", min }, 
                { "max", max }
            };
        }

        // Quickselect impl copied from <http://www.sourcetricks.com/2011/06/quick-select.html>
        // Returns the k'th smallest element of input[p...r] (inclusive)
        private static double QuickSelect(IList<double> input, int p, int r, int k) {
            if ( p == r )
                return input[p];
            int j = Partition(input, p, r);
            int length = j - p + 1;
            if ( length == k )
                return input[j];
            else if ( k < length )
                return QuickSelect(input, p, j - 1, k);
            else
                return QuickSelect(input, j + 1, r, k - length);
        }

        private static int Partition(IList<double> input, int p, int r) {
            double pivot = input[r];
            while ( p < r ) {
                while ( input[p] < pivot )
                    p++;
                while ( input[r] > pivot )
                    r--;
                if ( input[p] == input[r] )
                    p++;
                else if ( p < r ) {
                    double tmp = input[p];
                    input[p] = input[r];
                    input[r] = tmp;
                }
            }
            return r;
        }

        #endregion
    }
}

