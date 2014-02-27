package com.couchbase.lite.support;

import com.couchbase.lite.Database;
import com.couchbase.lite.util.Log;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.ScheduledFuture;
import java.util.concurrent.TimeUnit;

/**
 * Utility that queues up objects until the queue fills up or a time interval elapses,
 * then passes objects, in groups of its capacity, to a client-supplied processor block.
 */
public class Batcher<T> {

    private ScheduledExecutorService workExecutor;
    private ScheduledFuture<?> flushFuture;
    private int capacity;
    private int delay;
    private int scheduledDelay;
    private List<T> inbox;
    private BatchProcessor<T> processor;
    private boolean scheduled = false;
    private long lastProcessedTime;

    private Runnable processNowRunnable = new Runnable() {

        @Override
        public void run() {
            try {
                processNow();
            } catch (Exception e) {
                // we don't want this to crash the batcher
                com.couchbase.lite.util.Log.e(Database.TAG, this + ": BatchProcessor throw exception", e);
            }
        }
    };


    /**
     * Initializes a batcher.
     *
     * @param workExecutor the work executor that performs actual work
     * @param capacity The maximum number of objects to batch up. If the queue reaches this size, the queued objects will be sent to the processor immediately.
     * @param delay The maximum waiting time to collect objects before processing them. In some circumstances objects will be processed sooner.
     * @param processor The callback/block that will be called to process the objects.
     */
    public Batcher(ScheduledExecutorService workExecutor, int capacity, int delay, BatchProcessor<T> processor) {
        this.workExecutor = workExecutor;
        this.capacity = capacity;
        this.delay = delay;
        this.processor = processor;
    }

    /**
     * Adds multiple objects to the queue.
     */
    public synchronized void queueObjects(List<T> objects) {

        Log.d(Database.TAG, "queuObjects called with " + objects.size() + " objects. ");
        if (objects.size() == 0) {
            return;
        }
        if (inbox == null) {
            inbox = new ArrayList<T>();
        }

        Log.d(Database.TAG, "inbox size before adding objects: " + inbox.size());
        inbox.addAll(objects);
        Log.d(Database.TAG, objects.size() + " objects added to inbox.  inbox size: " + inbox.size());

        if (inbox.size() < capacity) {
            // Schedule the processing. To improve latency, if we haven't processed anything
            // in at least our delay time, rush these object(s) through ASAP:
            int delayToUse = delay;
            long delta = (System.currentTimeMillis() - lastProcessedTime);
            if (delta >= delay) {
                Log.d(Database.TAG, "delta " + delta + " >= delay " + delay + " --> using delay 0");
                delayToUse = 0;
            } else {
                Log.d(Database.TAG, "delta " + delta + " < delay " + delay + " --> using delay " + delayToUse);
            }
            scheduleWithDelay(delayToUse);
        } else {
            // If inbox fills up, process it immediately:
            Log.d(Database.TAG, "inbox.size() >= capacity, process immediately");
            unschedule();
            processNow();
        }

    }

    /**
     * Adds an object to the queue.
     */
    public void queueObject(T object) {
        List<T> objects = Arrays.asList(object);
        queueObjects(objects);
    }

    /**
     * Sends queued objects to the processor block (up to the capacity).
     */
    public void flush() {
        unschedule();
        processNow();
    }

    /**
     * Sends _all_ the queued objects at once to the processor block.
     * After this method returns, the queue is guaranteed to be empty.
     */
    public void flushAll() {
        while (inbox.size() > 0) {
            unschedule();
            List<T> toProcess = new ArrayList<T>();
            toProcess.addAll(inbox);
            processor.process(toProcess);
            lastProcessedTime = System.currentTimeMillis();
        }
    }

    /**
     * Empties the queue without processing any of the objects in it.
     */
    public void clear() {
        unschedule();
        inbox = null;
    }

    public int count() {
        synchronized(this) {
            if(inbox == null) {
                return 0;
            }
            return inbox.size();
        }
    }

    private void processNow() {

        scheduled = false;
        List<T> toProcess = new ArrayList<T>();

        synchronized (this) {
            if (inbox == null || inbox.size() == 0) {
                Log.d(Database.TAG, "processNow() called, but inbox is empty");
                return;
            } else if (inbox.size() <= capacity) {
                Log.d(Database.TAG, "processNow() called, inbox size: " + inbox.size());
                Log.d(Database.TAG, "inbox.size() <= capacity, adding " + inbox.size() + " items to toProcess array");
                toProcess.addAll(inbox);
                inbox = null;
            } else {
                Log.d(Database.TAG, "processNow() called, inbox size: " + inbox.size());
                for (int i=0; i<capacity; i++) {
                    T item = inbox.get(i);
                    toProcess.add(item);
                }

                for (T item : toProcess) {
                    inbox.remove(item);
                }

                Log.d(Database.TAG, "inbox.size() > capacity, moving " + toProcess.size() + " items from inbox -> toProcess array");

                // There are more objects left, so schedule them Real Soon:
                scheduleWithDelay(0);

            }

        }
        if(toProcess != null && toProcess.size() > 0) {
            Log.d(Database.TAG, "invoking processor with " + toProcess.size() + " items ");
            processor.process(toProcess);
        } else {
            Log.d(Database.TAG, "nothing to process");
        }
        lastProcessedTime = System.currentTimeMillis();

    }

    private void scheduleWithDelay(int suggestedDelay) {
        Log.d(Database.TAG, "scheduleWithDelay called with delay: " + suggestedDelay + " ms");
        if (scheduled && (suggestedDelay < scheduledDelay)) {
            Log.d(Database.TAG, "already scheduled and : " + suggestedDelay + " < " + scheduledDelay + " --> unscheduling");
            unschedule();
        }
        if (!scheduled) {
            Log.d(Database.TAG, "not already scheduled");
            scheduled = true;
            scheduledDelay = suggestedDelay;
            Log.d(Database.TAG, "workExecutor.schedule() with delay: " + suggestedDelay + " ms");
            flushFuture = workExecutor.schedule(processNowRunnable, suggestedDelay, TimeUnit.MILLISECONDS);
        }
    }

    private void unschedule() {
        Log.d(Database.TAG, "unschedule called");
        scheduled = false;
        if(flushFuture != null) {
            boolean didCancel = flushFuture.cancel(false);
            Log.d(Database.TAG, "tried to cancel flushFuture, result: " + didCancel);

        } else {
            Log.d(Database.TAG, "flushFuture was null, doing nothing");
        }
    }

}
