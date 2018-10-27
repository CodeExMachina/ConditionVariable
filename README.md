# ConditionVariable
The Monitor class Wait method does not allow for a proper condition variable implementation (i.e. it does not allow for waiting on multiple conditions per shared lock).

The good news is that the other synchronization primitives (e.g. SemaphoreSlim, lock statement, Monitor.Enter/Exit) in .NET can be used to implement a proper condition variable.
## Overview
The ConditionVariable class is a synchronization primitive that can be used to block a thread, or multiple threads at the same time, until another thread both modifies a shared variable (the condition), and notifies the ConditionVariable.

The thread that intends to modify the variable has to
- Acquire a lock (typically via lock statement)
- Perform the modification while the lock is held
- Execute Pulse or PulseAll on the ConditionVariable (the lock does NEED to be held for notification)

Even if the shared variable is atomic, it must be modified under the lock in order to correctly publish the modification to the waiting thread.

Any thread that intends to wait on ConditionVariable has to

- Acquire same lock as used to protect the shared variable
- Execute Wait. The Wait operations release the lock and suspend the execution of the thread.
- When the ConditionVariable is notified, a Wait timeout expires, Wait is cancelled, or a spurious wakeup occurs, the thread is awakened, and the lock is reacquired. The thread should then check the condition and resume waiting if the wake up was spurious.
## Usage
All you need to do is create an instance of the ConditionVariable class for each condition you want to be able to wait on.
```
object queueLock = new object();

private ConditionVariable notFullCondition = new ConditionVariable();
private ConditionVariable notEmptyCondition = new ConditionVariable();
```
And then just like in the Monitor class, the ConditionVariable's Pulse and Wait methods must be invoked from within a synchronized block of code.
```
T Take() {

  lock(queueLock) {

    while(queue.Count == 0) {

      // wait for queue to be not empty
      notEmptyCondition.Wait(queueLock);
    }

    T item = queue.Dequeue();

    if(queue.Count < 100) {

      // notify producer queue not full anymore
      notFullCondition.Pulse();
    }

    return item;
  }
}

void Add(T item) {

  lock(queueLock) {

    while(queue.Count >= 100) {

      // wait for queue to be not full
      notFullCondition.Wait(queueLock);
    }

    queue.Enqueue(item);

    // notify consumer queue not empty anymore
    notEmptyCondition.Pulse();
  }
}
```
