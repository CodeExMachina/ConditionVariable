/*
    Copyright (c) 2015 - 2018, Code Ex Machina, LLC.
    All rights reserved.
    
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Threading;

namespace CodeExMachina
{
    /// <summary>
    /// Condition variables are synchronization primitives that enable threads to 
    /// wait until a particular condition occurs. Condition variables are user-mode 
    /// objects that cannot be shared across processes.
    /// 
    /// Condition variables enable threads to release a lock and enter the waiting state.
    /// 
    /// Condition variables support operations that "wake one" or "wake all" waiting 
    /// threads. After a thread is woken, it re-acquires the lock it released when the 
    /// thread entered the waiting state.
    /// </summary>    
    public class ConditionVariable : IDisposable
    {
        private int _waiters = 0;
        private Object _waiters_lock = new object();
        
        private SemaphoreSlim _sema = new SemaphoreSlim(0, Int32.MaxValue);       

        private bool _was_pulse_all = false;
        private AutoResetEvent _waiters_done = new AutoResetEvent(false);

        private bool _is_disposed = false;

        /// <summary>
        /// Initializes a new instance of the ConditionVariable class.
        /// </summary>
        public ConditionVariable()
        { }

        /// <summary>
        /// Wakes all threads waiting on this condition variable.
        /// </summary>
        /// <exception cref="System.ObjectDisposedException">
        /// The current instance has already been disposed.
        /// </exception>
        /// <remarks>The PulseAll wakes all waiting threads while the Pulse wakes 
        /// only a single thread. Waking one thread is similar to setting an auto-reset 
        /// event, while waking all threads is similar to pulsing a manual reset event 
        /// but more reliable. 
        /// 
        /// NOTE: The critical section lock must be held before this call is made.
        /// </remarks>
        /// <example>
        /// This examples shows how to call PulseAll.
        /// <code>
        /// ConditionVariable cond = new ConditionVariable();
        /// object cs = new object();
        /// 
        /// lock(cs)
        /// {
        ///     cond.PulseAll();
        /// }
        /// </code>
        /// </example>
        public void PulseAll()
        {
            CheckDisposed();                   

            bool have_waiters = false;

            lock (_waiters_lock)
            {
                if (_waiters > 0)
                {
                    // broadcasting even if there is just one waiter
                    _was_pulse_all = have_waiters = true;
                }

                if (have_waiters)
                {
                    // wake up all the waiters
                    _sema.Release(_waiters);                    
                }
            }

            if(have_waiters)
            {
                // wait for all woken threads to acquire their part of semaphore.
                _waiters_done.WaitOne();

                _was_pulse_all = false;
            }
        }

        /// <summary>
        /// Wakes a single thread waiting on this condition variable.
        /// </summary>
        /// <exception cref="System.ObjectDisposedException">
        /// The current instance has already been disposed.
        /// </exception>
        /// <remarks>The PulseAll wakes all waiting threads while the Pulse wakes 
        /// only a single thread. Waking one thread is similar to setting an auto-reset 
        /// event, while waking all threads is similar to pulsing a manual reset event 
        /// but more reliable.
        /// 
        /// NOTE: The critical section lock must be held before this call is made.
        /// </remarks>
        /// <example>
        /// This examples shows how to call Pulse.
        /// <code>
        /// ConditionVariable cond = new ConditionVariable();
        /// object cs = new object();
        /// 
        /// lock(cs)
        /// {
        ///     cond.Pulse();
        /// }
        /// </code>
        /// </example>
        public void Pulse()
        {
            CheckDisposed();            

            bool have_waiters;

            lock (_waiters_lock)
            {
                have_waiters = _waiters > 0;
            }

            if (have_waiters)
                _sema.Release();
        }

        /// <summary>
        /// Waits on this condition variable and releases the specified critical section.
        /// </summary>        
        /// <param name="obj">The critical section to release.</param>        
        /// <exception cref="System.ObjectDisposedException">
        /// The current instance has already been disposed.
        /// </exception>
        /// <exception cref="System.Threading.SynchronizationLockException">
        /// The critical section is not owned by the caller at the time this method is called.
        /// </exception>
        /// <remarks>
        /// A thread that is waiting on a condition variable can be woken before the 
        /// a time-out interval has elapsed using the Pulse or PulseAll function. 
        /// 
        /// In this case, the thread wakes when the wake processing is complete, and not 
        /// when its time-out interval elapses. After the thread is woken, it re-acquires 
        /// the critical section it released when the thread entered the waiting state.
        /// 
        /// Condition variables are subject to spurious wakeups (those not associated with 
        /// an explicit wake) and stolen wakeups (another thread manages to run before the 
        /// woken thread). Therefore, you should recheck a predicate (typically in a while 
        /// loop) after a wait operation returns.
        /// </remarks>
        /// <example>
        /// This examples shows how to call Wait.
        /// <code>
        /// bool empty = true;
        /// ConditionVariable cond = new ConditionVariable();
        /// object obj = new object();
        /// 
        /// lock(obj)
        /// {
        ///     while(empty)
        ///     {
        ///         cond.Wait(obj);
        ///     }
        /// }
        /// </code>
        /// </example>
        public void Wait(object obj)
        {
            Wait_i(obj, Timeout.Infinite, CancellationToken.None);
        }

        /// <summary>
        /// Waits on this condition variable and releases the specified critical section 
        /// while observing a cancellation token.         
        /// </summary>
        /// <param name="obj">The critical section to release.</param>
        /// <param name="token">The CancellationToken token to observe.</param>
        /// <exception cref="System.ObjectDisposedException">
        /// The current instance has already been disposed.
        /// </exception>
        /// <exception cref="System.Threading.SynchronizationLockException">
        /// The critical section is not owned by the caller at the time this method is called.
        /// </exception>
        /// <exception cref="System.OperationCanceledException">
        /// The specified token was cancelled.
        /// </exception>
        /// <remarks>
        /// A thread that is waiting on a condition variable can be woken before the 
        /// a time-out interval has elapsed using the Pulse or PulseAll function. 
        /// In this case, the thread wakes when the wake processing is complete, and not 
        /// when its time-out interval elapses. After the thread is woken, it re-acquires 
        /// the critical section it released when the thread entered the waiting state.
        /// 
        /// Condition variables are subject to spurious wakeups (those not associated with 
        /// an explicit wake) and stolen wakeups (another thread manages to run before the 
        /// woken thread). Therefore, you should recheck a predicate (typically in a while 
        /// loop) after a wait operation returns.
        /// 
        /// If the token is cancelled, the method throws a OperationCancelledException.
        /// </remarks>
        /// <example>
        /// This examples shows how to call Wait using a CancellationToken.
        /// <code>
        /// bool empty = true;
        /// ConditionVariable cond = new ConditionVariable();
        /// object obj = new object();
        /// CancellationTokenSource cts = new CancellationTokenSource();
        /// 
        /// lock(obj)
        /// {
        ///     while(empty)
        ///     {
        ///         cond.Wait(obj, cts.Token);
        ///     }
        /// }
        /// </code>
        /// </example>
        public void Wait(object obj, CancellationToken token)
        {
            Wait_i(obj, Timeout.Infinite, token);
        }

        /// <summary>
        /// Waits on this condition variable for a specified time interval and releases the specified critical section. 
        /// </summary>
        /// <param name="obj">The critical section to release.</param>
        /// <param name="millisecondsTimeout">The number of milliseconds to wait, or Infinite(-1) to wait indefinitely.</param>
        /// <returns>True if condition variable was successfully waited on. Or false if time out occurs while waiting for condition variable.</returns>
        /// <exception cref="System.ObjectDisposedException">
        /// The current instance has already been disposed.
        /// </exception>
        /// <exception cref="System.Threading.SynchronizationLockException">
        /// The critical section is not owned by the caller at the time this method is called.
        /// </exception>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// millisecondsTimeout is a negative number other than -1, which represents an infinite time-out.
        /// </exception>
        /// <remarks>
        /// A thread that is waiting on a condition variable can be woken before the 
        /// a time-out interval has elapsed using the Pulse or PulseAll function. 
        /// In this case, the thread wakes when the wake processing is complete, and not 
        /// when its time-out interval elapses. After the thread is woken, it re-acquires 
        /// the critical section it released when the thread entered the waiting state.
        /// 
        /// Condition variables are subject to spurious wakeups (those not associated with 
        /// an explicit wake) and stolen wakeups (another thread manages to run before the 
        /// woken thread). Therefore, you should recheck a predicate (typically in a while 
        /// loop) after a wait operation returns.
        /// </remarks>
        /// <example>
        /// This examples shows how to call Wait using a time out.
        /// <code>
        /// bool empty = true;
        /// ConditionVariable cond = new ConditionVariable();
        /// object obj = new object();        
        /// 
        /// lock(obj)
        /// {
        ///     while(empty)
        ///     {
        ///         bool timed_out = !cond.Wait(obj, 100);
        ///     }
        /// }
        /// </code>
        /// </example>
        public bool Wait(object obj, int millisecondsTimeout)
        {
            ValidateMillisecondsTimeout(millisecondsTimeout);
            return Wait_i(obj, millisecondsTimeout, CancellationToken.None);
        }

        /// <summary>
        /// Waits on this condition variable for a specified time interval and releases the specified critical section 
        /// while observing a cancellation token.
        /// </summary>
        /// <param name="obj">The critical section to release.</param>
        /// <param name="millisecondsTimeout">The number of milliseconds to wait, or Infinite(-1) to wait indefinitely.</param>
        /// <param name="token">The CancellationToken token to observe.</param>
        /// <returns>True if condition variable was successfully waited on. Or false if time out occurs while waiting for condition variable.</returns>
        /// <exception cref="System.ObjectDisposedException">
        /// The current instance has already been disposed.
        /// </exception>
        /// <exception cref="System.Threading.SynchronizationLockException">
        /// The critical section is not owned by the caller at the time this method is called.
        /// </exception>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// millisecondsTimeout is a negative number other than -1, which represents an infinite time-out.
        /// </exception>
        /// <exception cref="System.OperationCanceledException">
        /// The specified token was cancelled.
        /// </exception>
        /// <remarks>
        /// A thread that is waiting on a condition variable can be woken before the 
        /// a time-out interval has elapsed using the Pulse or PulseAll function. 
        /// In this case, the thread wakes when the wake processing is complete, and not 
        /// when its time-out interval elapses. After the thread is woken, it re-acquires 
        /// the critical section it released when the thread entered the waiting state.
        /// 
        /// Condition variables are subject to spurious wakeups (those not associated with 
        /// an explicit wake) and stolen wakeups (another thread manages to run before the 
        /// woken thread). Therefore, you should recheck a predicate (typically in a while 
        /// loop) after a wait operation returns.
        /// 
        /// If the token is cancelled, the method throws a OperationCancelledException.
        /// </remarks>
        /// <example>
        /// This examples shows how to call Wait using a time out and cancellation token.
        /// <code>
        /// bool empty = true;
        /// ConditionVariable cond = new ConditionVariable();
        /// object obj = new object();        
        /// CancellationTokenSource cts = new CancellationTokenSource();
        /// 
        /// lock(obj)
        /// {
        ///     while(empty)
        ///     {
        ///         bool timed_out = !cond.Wait(obj, 100, cts.Token);
        ///     }
        /// }
        /// </code>
        /// </example>
        public bool Wait(object obj, int millisecondsTimeout, CancellationToken token)
        {
            ValidateMillisecondsTimeout(millisecondsTimeout);
            return Wait_i(obj, millisecondsTimeout, token);
        }

        /// <summary>
        /// Waits on this condition variable using a TimeSpan to specify the time interval and releases the specified critical section.
        /// </summary>
        /// <param name="obj">The critical section to release.</param>
        /// <param name="timeout">A TimeSpan that represents the number of milliseconds to wait, or a TimeSpan that represents -1 milliseconds to wait indefinitely.</param>
        /// <returns>True if condition variable was successfully waited on. Or false if time out occurs while waiting for condition variable.</returns>
        /// <exception cref="System.ObjectDisposedException">
        /// The current instance has already been disposed.
        /// </exception>
        /// <exception cref="System.Threading.SynchronizationLockException">
        /// The critical section is not owned by the caller at the time this method is called.
        /// </exception>
        /// <remarks>
        /// A thread that is waiting on a condition variable can be woken before the 
        /// a time-out interval has elapsed using the Pulse or PulseAll function. 
        /// In this case, the thread wakes when the wake processing is complete, and not 
        /// when its time-out interval elapses. After the thread is woken, it re-acquires 
        /// the critical section it released when the thread entered the waiting state.
        /// 
        /// Condition variables are subject to spurious wakeups (those not associated with 
        /// an explicit wake) and stolen wakeups (another thread manages to run before the 
        /// woken thread). Therefore, you should recheck a predicate (typically in a while 
        /// loop) after a wait operation returns.      
        /// </remarks>
        /// <example>
        /// This examples shows how to call Wait using a TimeSpan.
        /// <code>
        /// bool empty = true;
        /// ConditionVariable cond = new ConditionVariable();
        /// object obj = new object();               
        /// 
        /// lock(obj)
        /// {
        ///     while(empty)
        ///     {
        ///         bool timed_out = !cond.Wait(obj, TimeSpan.FromMilliseconds(100));
        ///     }
        /// }
        /// </code>
        /// </example>
        public bool Wait(object obj, TimeSpan timeout)
        {
            ValidateTimeout(timeout);
            return Wait_i(obj, (int)timeout.TotalMilliseconds, CancellationToken.None);
        }

        /// <summary>
        /// Waits on this condition variable using a TimeSpan to specify the time interval and releases the specified critical section 
        /// while observing a cancellation token.
        /// </summary>
        /// <param name="obj">The critical section to release.</param>
        /// <param name="timeout">A TimeSpan that represents the number of milliseconds to wait, or a TimeSpan that represents -1 milliseconds to wait indefinitely.</param>
        /// <param name="token">The CancellationToken token to observe.</param>
        /// <returns>True if condition variable was successfully waited on. Or false if time out occurs while waiting for condition variable.</returns>
        /// <exception cref="System.ObjectDisposedException">
        /// The current instance has already been disposed.
        /// </exception>
        /// <exception cref="System.Threading.SynchronizationLockException">
        /// The critical section is not owned by the caller at the time this method is called.
        /// </exception>
        /// <exception cref="System.OperationCanceledException">
        /// The specified token was cancelled.
        /// </exception>
        /// <remarks>
        /// A thread that is waiting on a condition variable can be woken before the 
        /// a time-out interval has elapsed using the Pulse or PulseAll function. 
        /// In this case, the thread wakes when the wake processing is complete, and not 
        /// when its time-out interval elapses. After the thread is woken, it re-acquires 
        /// the critical section it released when the thread entered the waiting state.
        /// 
        /// Condition variables are subject to spurious wakeups (those not associated with 
        /// an explicit wake) and stolen wakeups (another thread manages to run before the 
        /// woken thread). Therefore, you should recheck a predicate (typically in a while 
        /// loop) after a wait operation returns.
        /// 
        /// If the token is cancelled, the method throws a OperationCancelledException.
        /// </remarks>
        /// <example>
        /// This examples shows how to call Wait using a TimeSpan and CancellationToken.
        /// <code>
        /// bool empty = true;
        /// ConditionVariable cond = new ConditionVariable();
        /// object obj = new object();
        /// CancellationTokenSource cts = new CancellationTokenSource();
        /// 
        /// lock(obj)
        /// {
        ///     while(empty)
        ///     {
        ///         bool timed_out = !cond.Wait(obj, TimeSpan.FromMilliseconds(100), cts.Token);
        ///     }
        /// }
        /// </code>
        /// </example>
        public bool Wait(object obj, TimeSpan timeout, CancellationToken token)
        {
            ValidateTimeout(timeout);
            return Wait_i(obj, (int)timeout.TotalMilliseconds, token);
        }

        /// <summary>
        /// Releases all resources used by the current instance of the ConditionVariable class.
        /// </summary>
        /// <remarks>
        /// Call Dispose when you are finished using the ConditionVariable. The Dispose method 
        /// leaves the ConditionVariable in an unusable state. After calling Dispose, you must 
        /// release all references to the ConditionVariable so the garbage collector can reclaim 
        /// the memory that the ConditionVariable was occupying.
        /// </remarks>
        public void Dispose()
        {
            Dispose(true);
            //GC.SuppressFinalize(this);
        }

        #region Private

        private bool Wait_i(object obj, int timeout, CancellationToken token)
        {            
            CheckDisposed();

            if (obj == null)
                throw new ArgumentNullException("obj");

            bool reacquire = true, success = false;

            try
            {                
                lock (_waiters_lock)
                {
                    ++_waiters;
                }

                Monitor.Exit(obj);

                success = _sema.Wait(timeout, token);
            }
            catch(SynchronizationLockException)
            {
                // don't "reacquire" lock if did not own it in first place!
                reacquire = false;
                throw;
            }
            finally
            {
                bool last_waiter;

                lock (_waiters_lock)
                {
                    --_waiters;

                    last_waiter = _was_pulse_all && _waiters == 0;
                }

                // signal broadcaster if the last waiter
                if (last_waiter)
                    _waiters_done.Set();

                // reacquire the lock on exit
                if(reacquire)
                    Monitor.Enter(obj);                
            }

            return success;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_is_disposed)
            {
                if (disposing)
                {                    
                    _sema.Dispose();
                    _waiters_done.Dispose();
                }

                _sema = null;
                _waiters_done = null;

                _is_disposed = true;
            }
        }

        private static void ValidateTimeout(TimeSpan timeout)
        {
            long total_milliseconds = (long)timeout.TotalMilliseconds;
            if ((total_milliseconds < 0 || total_milliseconds > Int32.MaxValue) && (total_milliseconds != Timeout.Infinite))
            {
                throw new ArgumentOutOfRangeException("timeout");
            }
        }

        private static void ValidateMillisecondsTimeout(int millisecondsTimeout)
        {
            if ((millisecondsTimeout < 0) && (millisecondsTimeout != Timeout.Infinite))
            {
                throw new ArgumentOutOfRangeException("millisecondsTimeout");
            }
        }

        private void CheckDisposed()
        {
            if (_is_disposed)
            {
                throw new ObjectDisposedException("ConditionVariable");
            }
        }

        #endregion
    }
}
