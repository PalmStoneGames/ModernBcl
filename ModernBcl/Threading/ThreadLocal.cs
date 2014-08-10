﻿// 
// ThreadLocal.cs
//  
// Author:
//       Jérémie "Garuma" Laval <jeremie.laval@gmail.com>
// 
// Copyright (c) 2009 Jérémie "Garuma" Laval
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Security.Permissions;

namespace System.Threading
{

	[HostProtection (SecurityAction.LinkDemand, Synchronization = true, ExternalThreading = true)]
	[System.Diagnostics.DebuggerDisplay ("IsValueCreated={IsValueCreated}, Value={ValueForDebugDisplay}")]
	[System.Diagnostics.DebuggerTypeProxy ("System.Threading.SystemThreading_ThreadLocalDebugView`1")]
	public class ThreadLocal<T> : IDisposable
	{
	    bool disposed;
		readonly Func<T> valueFactory;
		LocalDataStoreSlot localStore;
		readonly ThreadLocal<Exception> cachedException;
		
		class DataSlotWrapper
		{
			public bool Creating;
			public bool Init;
			public Func<T> Getter;
		}
		
		public ThreadLocal () : this (InitDefault)
		{
		}

	    private static T InitDefault()
	    {
	        return default(T);
	    }

	    public ThreadLocal (Func<T> valueFactory) : this(valueFactory, true)
        {
		}

	    private ThreadLocal(Func<T> valueFactory, bool initCachedException)
	    {
	        if (initCachedException)
	        {
	            cachedException = new ThreadLocal<Exception>(ThreadLocal<Exception>.InitDefault, false);
	        }

            if (valueFactory == null)
                throw new ArgumentNullException("valueFactory");

            localStore = Thread.AllocateDataSlot();
            this.valueFactory = valueFactory;
	    }
		
		public void Dispose ()
		{
			Dispose (true);
		}
		
		protected virtual void Dispose (bool disposing)
		{
		    disposed = true;
		}
		
		public bool IsValueCreated {
			get {
				ThrowIfNeeded (false);
				return IsInitializedThreadLocal ();
			}
		}
		
		public T Value {
			get {
				ThrowIfNeeded (true);
				return GetValueThreadLocal ();
			}
			set {
				ThrowIfNeeded (false);

				DataSlotWrapper w = GetWrapper ();
				w.Init = true;
				w.Getter = () => value;
			}
		}
		
		public override string ToString ()
		{
			return string.Format ("[ThreadLocal: IsValueCreated={0}, Value={1}]", IsValueCreated, Value);
		}

		
		T GetValueThreadLocal ()
		{
			DataSlotWrapper myWrapper = GetWrapper ();
			if (myWrapper.Creating)
				throw new InvalidOperationException ("The initialization function attempted to reference Value recursively");

			return myWrapper.Getter ();
		}
		
		bool IsInitializedThreadLocal ()
		{
			DataSlotWrapper myWrapper = GetWrapper ();

			return myWrapper.Init;
		}

		DataSlotWrapper GetWrapper ()
		{
			DataSlotWrapper myWrapper = (DataSlotWrapper)Thread.GetData (localStore);
			if (myWrapper == null) {
				myWrapper = DataSlotCreator ();
				Thread.SetData (localStore, myWrapper);
			}

			return myWrapper;
		}

		void ThrowIfNeeded (bool throwCachedException)
		{
            if(disposed)
                throw new ObjectDisposedException("ThreadLocal");
            if (throwCachedException && cachedException != null && cachedException.IsValueCreated)
				throw cachedException.Value;
		}

		DataSlotWrapper DataSlotCreator ()
		{
			var wrapper = new DataSlotWrapper ();
			Func<T> valSelector = valueFactory;
	
			wrapper.Getter = delegate {
				wrapper.Creating = true;
				try {
					T val = valSelector ();
					wrapper.Creating = false;
					wrapper.Init = true;
					wrapper.Getter = () => val;
					return val;
				} catch (Exception e)
				{
                    wrapper.Creating = false;
					if(cachedException != null) cachedException.Value = e;
					throw e;
				}
			};
			
			return wrapper;
		}
	}
}