// Credits: https://github.com/Jashepp & https://dev.mivor.net/VRChat/
// Version: 0.0.1

#if VRC_SDK_VRCSDK3
#if UNITY_EDITOR
using System;
using Object = System.Object;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using IEnumerator = System.Collections.IEnumerator;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.Threading.Tasks;

namespace MivorTools.Editor
{
	public partial class BaseWindow<TEditorWindow> : EditorWindow
	{
		protected delegate void anonFunc();
		protected anonFunc noOp = ()=>{};


		protected string stringsWhereCommon(List<string> strings){
			if(strings.Count==0) return "";
			int commonLength; int minimumLength = -1;
			strings.ForEach(s=>{ if(minimumLength<0 || s.Length<minimumLength) minimumLength=s.Length; });
			for(commonLength = 0; commonLength < minimumLength; commonLength++){
				bool same = true;
				foreach(var s1 in strings){
					foreach(var s2 in strings){
						if(s1[commonLength]!=s2[commonLength]) same = false;
					}
				}
				if(!same) break;
			}
			return strings[0].Substring(0, commonLength);
		}

		

		protected class checkpointTiming {
			public double minPerSecond = 30;
			public double maxPerSecond = 100;
			public Stopwatch stopwatch = null;
			public double lastMS = 0;
			public checkpointTiming(){
				stopwatch = new Stopwatch();
				stopwatch.Start();
			}
			public bool checkpoint(){
				double ms = 1000 / Math.Max(minPerSecond,Math.Min(maxPerSecond,Application.targetFrameRate));
				if(stopwatch.Elapsed.TotalMilliseconds>lastMS+ms){ lastMS=stopwatch.Elapsed.TotalMilliseconds; return true; }
				return false;
			}
		}



		protected IEnumerator StartCoroutine(IEnumerator runCoroutine) => manualCoroutine.StartCoroutine(runCoroutine);
		protected static class manualCoroutine {
			// routineIndex idea from http://answers.unity.com/answers/1413012/view.html
			private static bool isRunning = false;
			public delegate void callback();
			public delegate bool gateCallback();
			private class rTask {
				public IEnumerator routine = null;
				public callback onStop = null;
				public gateCallback gateCall = null;
				public rTask parent = null;
				public rTask waitOn = null;
			}
			private static rTask currentTask = null;
			private static int currentRoutineIndex = 0;
			private static List<rTask> routines = new List<rTask>();
			public static IEnumerator StartCoroutine(IEnumerator routine, callback onStop=null, gateCallback gateCall=null){
				var task = new rTask { routine=routine, onStop=onStop, gateCall=gateCall };
				if(currentTask!=null){ currentTask.waitOn = task; task.parent = currentTask; }
				routines.Add(task);
				if(!isRunning) Start();
				return routine;
			}
			public static void Start(){
				if(isRunning) return;
				isRunning = true;
				EditorApplication.update += processNextCoroutine;
			}
			public static void Stop(){
				if(!isRunning) return;
				isRunning = false;
				if(EditorApplication.update!=null) EditorApplication.update -= processNextCoroutine;
			}
			private static void processNextCoroutine(){
				if(routines.Count<=0) return;
				currentRoutineIndex = (currentRoutineIndex + 1) % routines.Count;
				var task = routines[currentRoutineIndex];
				if(processTask(task,currentRoutineIndex)==false) processNextCoroutine();
			}
			private static bool processTask(rTask task) => processTask(task, routines.FindIndex((t=>t==task)));
			private static bool processTask(rTask task, int rIndex){
				if(task.waitOn!=null){ processTask(task.waitOn); return true; }
				if(task.gateCall!=null && task.gateCall()==false){ return false; }
				currentTask = task;
				bool continues = task.routine.MoveNext();
				currentTask = null;
				if(continues) return true;
				if(task.parent!=null){
					task.parent.waitOn = null;
					task.parent = null;
				}
				routines.RemoveAt(rIndex);
				if(task.onStop!=null) task.onStop();
				return true;
			}
		}
		
		// Requires manualCoroutine
		public partial class Promise {
			public static Promise StartCoroutine(IEnumerator routine){
				var p = new Promise();
				var cr = manualCoroutine.StartCoroutine(routine,()=>{ p.resolve(null); });
				return p;
			}
			public static Promise operator +(Promise p, IEnumerator r) => StartCoroutine(r);
		}

		// Requires manualCoroutine
		public partial class Promise {
			public class PromiseDetached : Promise {
				// not sure if this works
				// public new static PromiseDetached Resolve(object value=null) => (PromiseDetached)Promise.Resolve(value);
				// public new static PromiseDetached Reject(object value=null) => (PromiseDetached)Promise.Reject(value);
				// public override Promise Then(valueBothCallback thenCB, valueBothCallback catchCB=null) => (PromiseDetached)base.Then(thenCB,catchCB);
				// protected PromiseDetached() : base(){}
				// public PromiseDetached(constructorCallback pBody) : base(pBody){}

				protected override void resolve(object value){ if(currentState==States.Unfulfilled) StartCoroutine(resolve_routine(value)); }
				protected override void reject(object value){ if(currentState==States.Unfulfilled) StartCoroutine(reject_routine(value)); }
				private IEnumerator resolve_routine(object value){ yield return null; base.resolve(value); }
				private IEnumerator reject_routine(object value){ yield return null; base.reject(value); }

				// Not working
				public new System.Runtime.CompilerServices.TaskAwaiter<object> GetAwaiter(){
					var tcs = new TaskCompletionSource<object>();
					IEnumerator routine(){
						yield return null;
						((PromiseDetached)this).Then(
							(v)=>tcs.TrySetResult(v),
							(v)=>tcs.TrySetException(v is Exception ? (Exception)v : new Exception(v.ToString()))
						);
					}
					StartCoroutine(routine());
					return tcs.Task.GetAwaiter();
				}

			}
		}

		public partial class Promise {
			public delegate void emptyCallback();
			public delegate object valueOutCallback();
			public delegate void valueInCallback(object value=null);
			public delegate object valueBothCallback(object value=null);
			public delegate void constructorCallback(valueInCallback resolve, valueInCallback reject);

			public static Promise Resolve(object value=null) => value is Promise ? (Promise)value : fastPromise(States.Resolved,value);
			public static Promise Reject(object value=null) => value is Promise ? (Promise)value : fastPromise(States.Rejected,value);
			private static Promise fastPromise(States state, object value){
				return new Promise((resolve,reject)=>{
					if(state==States.Resolved) resolve(value); else reject(value);
				});
			}

			protected enum States { Resolved, Rejected, Unfulfilled }
			protected States currentState = States.Unfulfilled;
			protected object settledValue = null;

			protected Promise(){}
			public Promise(constructorCallback pBody) => pBody((v)=>resolve(v),(v)=>reject(v));
			protected virtual void resolve(object value){
				if(currentState!=States.Unfulfilled) return;
				if(value is Exception){ reject(value); return; }
				currentState = States.Resolved;
				settledValue = value;
				processResolveQueue();
			}
			protected virtual void reject(object value){
				if(currentState!=States.Unfulfilled) return;
				currentState = States.Rejected;
				settledValue = value;
				processRejectQueue();
			}

			private List<valueInCallback> onResolveQueue = new List<valueInCallback>();
			private List<valueInCallback> onRejectQueue = new List<valueInCallback>();
			private void processResolveQueue(){
				if(currentState!=States.Resolved) return;
				var arr = onResolveQueue.ToArray();
				onResolveQueue.Clear(); onRejectQueue.Clear();
				foreach(valueInCallback vCB in arr){
					vCB(settledValue);
				}
			}
			private void processRejectQueue(){
				if(currentState!=States.Rejected) return;
				var arr = onRejectQueue.ToArray();
				onRejectQueue.Clear(); onResolveQueue.Clear();
				foreach(valueInCallback vCB in arr){
					vCB(settledValue);
				}
			}

			private void onResolve(valueInCallback cb) => onResolveQueue.Add(cb);
			private void onReject(valueInCallback cb) => onRejectQueue.Add(cb);
			private void onResolveLinked(valueBothCallback cb,Promise newP) => onResolve((v)=>newP.resolve(cb!=null?cb(v):v));
			private void onRejectLinked(valueBothCallback cb,Promise newP) => onReject((v)=>newP.resolve(cb!=null?cb(v):v));

			public virtual Promise Then(valueBothCallback thenCB, valueBothCallback catchCB=null){
				if(settledValue is Promise vp){ return vp.Then(thenCB,catchCB); }
				if(currentState==States.Rejected) return this;
				if(currentState==States.Resolved && thenCB!=null) return Promise.Resolve(thenCB(settledValue));
				if(currentState==States.Rejected && catchCB!=null) return Promise.Resolve(catchCB(settledValue));
				var newPromise = new Promise();
				onResolveLinked(thenCB,newPromise);
				onRejectLinked(catchCB,newPromise);
				return newPromise;
			}
			public Promise Catch(valueBothCallback catchCB){
				if(settledValue is Promise vp){ return vp.Catch(catchCB); }
				if(currentState==States.Resolved) return this;
				if(currentState==States.Rejected) return Promise.Resolve(catchCB(settledValue));
				return Then(null,catchCB);
			}

			public Promise Then(valueInCallback thenCB=null, valueInCallback catchCB=null){
				if(thenCB==null && catchCB==null) return this;
				if(thenCB==null && catchCB!=null) return Catch(catchCB);
				if(catchCB==null) return Then((v)=>{ thenCB(v); return null; });
				return Then((v)=>{ thenCB(v); return null; },(v)=>{ catchCB(v); return null; });
			}
			public Promise Catch(valueInCallback catchCB=null){
				if(catchCB!=null) return Catch((v)=>{ catchCB(v); return null; });
				return Catch((v)=>null);
			}

			// async/await: `var value = await promise;`
			public System.Runtime.CompilerServices.TaskAwaiter<object> GetAwaiter(){
				var tcs = new TaskCompletionSource<object>();
				this.Then(
					(v)=>tcs.TrySetResult(v),
					(v)=>tcs.TrySetException(v is Exception ? (Exception)v : new Exception(v.ToString()))
				);
				return tcs.Task.GetAwaiter();
			}

			public bool Equals(Promise p){
				if(p==null) return false;
				if(Object.ReferenceEquals(this,p)) return true;
				return this.currentState!=States.Unfulfilled && this.currentState==p.currentState && this.settledValue==p.settledValue;
			}
			public static bool operator ==(Promise lhs, Promise rhs) => (lhs==null || rhs==null) ? (lhs==null && rhs==null) : lhs.Equals(rhs);
			public static bool operator !=(Promise lhs, Promise rhs) => !(lhs==rhs);
			public override bool Equals(object value) => settledValue!=null && settledValue==value;
			public static bool operator ==(Promise lhs, object rhs) => lhs.Equals(rhs);
			public static bool operator !=(Promise lhs, object rhs) => !lhs.Equals(rhs);
			public override int GetHashCode() => (typeof(Promise), currentState, settledValue).GetHashCode();
			public override string ToString() => currentState+(settledValue!=null?": "+settledValue:"");//+" (Promise)";

			public static bool operator true(Promise p) => p.currentState!=States.Unfulfilled;
			public static bool operator false(Promise p) => p.currentState==States.Unfulfilled;
			//public static bool operator &(Promise lhs, Promise rhs) => lhs?.currentState==rhs?.currentState;
			//public static bool operator |(Promise lhs, Promise rhs) => lhs?.currentState!=rhs?.currentState;
			public static implicit operator Promise(constructorCallback cb) => new Promise(cb);
			public static implicit operator Promise(valueOutCallback cb) => Resolve(cb());

			public static Promise operator +(Promise p, valueBothCallback cb) => p.Then(cb);
			public static Promise operator -(Promise p, valueBothCallback cb) => p.Catch(cb);
			public static Promise operator +(Promise p, valueInCallback cb) => p.Then(cb);
			public static Promise operator -(Promise p, valueInCallback cb) => p.Catch(cb);
			public static Promise operator +(Promise p, Promise p2) => p.Then(v=>p2);
			public static Promise operator -(Promise p, Promise p2) => p.Catch(v=>p2);

		}

		public partial class Promise {

			public Promise Finally(valueBothCallback finallyCB){
				//if(currentState!=States.Unfulfilled) return this;
				return Catch((v)=>v).Catch((v)=>null).Then(finallyCB);
			}
			public Promise Finally(valueInCallback finallyCB=null){
				if(finallyCB!=null) return Finally((v)=>{ finallyCB(v); return null; });
				return Finally((v)=>null);
			}

			public static Promise AllSettled(params Promise[] pArr){
				Promise pChain = Resolve();
				foreach(Promise p in pArr){ if(p.currentState==States.Unfulfilled) pChain = pChain.Finally((v)=>p); }
				return pChain.Finally((v)=>{
					object[] rArr = new object[pArr.Length];
					for (int i = 0; i < pArr.Length; i++){ rArr[i] = pArr[i].settledValue; }
					return rArr;
				});
			}
			public static Promise All(params Promise[] pArr){
				Promise pChain = Resolve();
				foreach(Promise p in pArr){ if(p.currentState==States.Unfulfilled) pChain = pChain.Then((v)=>p); }
				return pChain.Then((v)=>{
					object[] rArr = new object[pArr.Length];
					for (int i = 0; i < pArr.Length; i++){ rArr[i] = pArr[i].settledValue; }
					return rArr;
				});
			}
			public static Promise Any(params Promise[] pArr){
				Promise pChain = Resolve();
				Promise promise = new Promise();
				List<object> rejects = new List<object>();
				for (int i = 0; i < pArr.Length; i++){
					pArr[i].Then((v)=>promise.resolve(v));
					pChain = pChain.Finally((p)=>pArr[i].Catch((v)=>{ rejects.Add(v); }));
				}
				pChain = pChain.Finally((cp)=>{
					if(rejects.Count>0) promise.reject(rejects.ToArray());
				});
				return promise;
			}
			public static Promise Race(params Promise[] pArr){
				Promise promise = new Promise();
				foreach(Promise p in pArr){ p.Then((v)=>promise.resolve(v),(v)=>promise.reject(v)); }
				return promise;
			}

			public static Promise operator &(Promise lhs, Promise rhs) => All(lhs,rhs);
			public static Promise operator |(Promise lhs, Promise rhs) => Any(lhs,rhs);
			
		}

		public partial class Promise {
			public class PromiseChain {
				protected Promise privPromise;
				public Promise promise { get { return privPromise; } }
				public PromiseChain() => privPromise = Resolve();
				public PromiseChain(Promise p) => privPromise = p;
				public PromiseChain(params Promise[] pArr) => privPromise = AllSettled(pArr);
				public PromiseChain Chain(Promise p) => new PromiseChain(privPromise.Finally((v)=>p));
				public PromiseChain Then(valueBothCallback thenCB=null, valueBothCallback catchCB=null) => new PromiseChain(privPromise.Then(thenCB,catchCB));
				public PromiseChain Catch(valueBothCallback catchCB=null) => new PromiseChain(privPromise.Catch(catchCB));
				public PromiseChain Then(valueInCallback thenCB=null, valueInCallback catchCB=null) => new PromiseChain(privPromise.Then(thenCB,catchCB));
				public PromiseChain Catch(valueInCallback catchCB=null) => new PromiseChain(privPromise.Catch(catchCB));
				
				public static PromiseChain operator +(PromiseChain c, Promise p) => c.Chain(p);
				public override bool Equals(object v) => privPromise.Equals(v);
				public override int GetHashCode() => (typeof(PromiseChain), privPromise).GetHashCode();
				public override string ToString() => privPromise.ToString(); //+" (Promise)";
				public static implicit operator PromiseChain(Promise p) => new PromiseChain(p);
			}
			public bool Equals(PromiseChain c) => c.Equals(this);
			public static implicit operator Promise(PromiseChain pc) => pc.promise;
		}

		public partial class Promise {
			public class Defer {
				protected Promise privPromise;
				public Promise promise { get { return privPromise; } }
				public object value { get { return privPromise.settledValue; } set { this.resolve(value); } }
				public Defer() => privPromise=new Promise();
				public Defer(Promise p) => privPromise=p;
				public void resolve(object v) => privPromise.resolve(v);
				public void reject(object v) => privPromise.reject(v);
				public Promise Then(valueBothCallback thenCB=null, valueBothCallback catchCB=null) => privPromise.Then(thenCB,catchCB);
				public Promise Catch(valueBothCallback catchCB=null) => privPromise.Catch(catchCB);
				public Promise Then(valueInCallback thenCB=null, valueInCallback catchCB=null) => privPromise.Then(thenCB,catchCB);
				public Promise Catch(valueInCallback catchCB=null) => privPromise.Catch(catchCB);

				public static Defer operator +(Defer d, object v){ d.resolve(v); return d; }
				public static Defer operator -(Defer d, object v){ d.reject(v); return d; }
				public override bool Equals(object v) => privPromise.Equals(v);
				public override int GetHashCode() => (typeof(Defer), privPromise).GetHashCode();
				public override string ToString() => privPromise.ToString(); //+" (Promise)";
				public static implicit operator Defer(Promise p) => new Defer(p);
			}
			public bool Equals(Defer d) => d.Equals(this);
			public static implicit operator Promise(Defer pc) => pc.promise;
		}

	}
}

#endif
#endif
