package com.couchbase.lite.replicator;

import com.couchbase.lite.Database;
import com.couchbase.lite.util.Log;

public class ChangeTrackerBackoff {

    private static int MAX_SLEEP_MILLISECONDS = 5 * 60 * 1000;  // 5 mins
    private int numAttempts = 0;

    public void resetBackoff() {
        numAttempts = 0;
    }

    public int getSleepMilliseconds() {

        int result =  (int) (Math.pow(numAttempts, 2) - 1) / 2;

        result *= 100;

        if (result < MAX_SLEEP_MILLISECONDS) {
            increaseBackoff();
        }

        result = Math.abs(result);

        return result;

    }

    public void sleepAppropriateAmountOfTime() {
        try {
            int sleepMilliseconds = getSleepMilliseconds();
            if (sleepMilliseconds > 0) {
                Log.d(Database.TAG, this.getClass().getSimpleName() + " sleeping for " + sleepMilliseconds);
                Thread.sleep(sleepMilliseconds);
            }
        } catch (InterruptedException e1) {
        }
    }

    private void increaseBackoff() {
        numAttempts += 1;
    }

}
