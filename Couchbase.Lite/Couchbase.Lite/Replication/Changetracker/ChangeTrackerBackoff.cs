/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013 Xamarin, Inc. All rights reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
 * except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under the
 * License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
 * either express or implied. See the License for the specific language governing permissions
 * and limitations under the License.
 */

using System;
using Couchbase.Lite;
using Couchbase.Lite.Replicator.Changetracker;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Replicator.Changetracker
{
	public class ChangeTrackerBackoff
	{
		private static int MaxSleepMilliseconds = 5 * 60 * 1000;

		private int numAttempts = 0;

		// 5 mins
		public virtual void ResetBackoff()
		{
			numAttempts = 0;
		}

		public virtual int GetSleepMilliseconds()
		{
			int result = (int)(Math.Pow(numAttempts, 2) - 1) / 2;
			result *= 100;
			if (result < MaxSleepMilliseconds)
			{
				IncreaseBackoff();
			}
			result = Math.Abs(result);
			return result;
		}

		public virtual void SleepAppropriateAmountOfTime()
		{
			try
			{
				int sleepMilliseconds = GetSleepMilliseconds();
				if (sleepMilliseconds > 0)
				{
					Log.D(Database.Tag, this.GetType().Name + " sleeping for " + sleepMilliseconds);
					Sharpen.Thread.Sleep(sleepMilliseconds);
				}
			}
			catch (Exception)
			{
			}
		}

		private void IncreaseBackoff()
		{
			numAttempts += 1;
		}
	}
}
