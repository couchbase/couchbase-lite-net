// 
//  Try.cs
// 
//  Copyright (c) 2018 Couchbase, Inc All rights reserved.
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
using System.Threading;

using FluentAssertions.Execution;

namespace Test.Util
{
    public abstract class Try
    {
        #region Variables

        protected int _count = 1;
        protected TimeSpan _delay = TimeSpan.FromMilliseconds(100);

        #endregion

        #region Constructors

        protected Try()
        {

        }

        #endregion

        #region Public Methods

        public Try Times(int count)
        {
            _count = count;
            return this;
        }

        public abstract bool Go();

        public static Try Assertion(Action assertion)
        {
            return new AssertionTry(assertion);
        }

        public static Try Condition(Func<bool> condition)
        {
            return new BooleanTry(condition);
        }

        public Try Delay(TimeSpan delay)
        {
            _delay = delay;
            return this;
        }

        #endregion

        private sealed class BooleanTry : Try
        {
            private readonly Func<bool> _condition;

            public BooleanTry(Func<bool> condition)
            {
                _condition = condition;;
            }

            public override bool Go()
            {
                var count = 0;
                while (count++ < _count) {
                    if (_condition()) {
                        return true;
                    }

                    Thread.Sleep(_delay);
                }

                return false;
            }
        }

        private sealed class AssertionTry : Try
        {
            private readonly Action _assertion;

            public AssertionTry(Action assertion)
            {
                _assertion = assertion;
            }

            public override bool Go()
            {
                var count = 0;
                while (count++ < _count) {
                    try {
                        _assertion();
                        return true;
                    } catch (AssertionFailedException e) {
                        if (count == _count) {
                            throw;
                        }
                    }

                    Thread.Sleep(_delay);
                }

                return false;
            }
        }
    }
}
