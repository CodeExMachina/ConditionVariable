# ConditionVariable
Condition Variable class using 100% managed code in C#

The Monitor's Wait interface does not allow for a proper condition variable implementation (i.e. it does not allow for waiting on multiple conditions per shared lock).

The good news is that the other synchronization primitives (e.g. SemaphoreSlim, lock keyword, Monitor.Enter/Exit) in .NET can be used to implement a proper condition variable.

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
