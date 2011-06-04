﻿#region license

// Copyright 2004-2011 Castle Project - http://www.castleproject.org/
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using System.Transactions;
using Castle.Services.Transaction.Internal;
using Castle.Services.Transaction.IO;
using log4net;

namespace Castle.Services.Transaction
{
	public sealed class TransactionManager : ITransactionManager
	{
		private static readonly ILog _Logger = LogManager.GetLogger(typeof (TransactionManager));
		private readonly IActivityManager _ActivityManager;
		private readonly IFileAdapter _FileAdapter;
		private readonly IDirectoryAdapter _DirectoryAdapter;

		public TransactionManager(IActivityManager activityManager, IFileAdapter fileAdapter, IDirectoryAdapter directoryAdapter)
		{
			Contract.Requires(activityManager != null);
			Contract.Requires(fileAdapter != null);
			Contract.Requires(directoryAdapter != null);

			_ActivityManager = activityManager;
			_FileAdapter = fileAdapter;
			_DirectoryAdapter = directoryAdapter;
		}

		[ContractInvariantMethod]
		private void Invariant()
		{
			Contract.Invariant(_ActivityManager != null);
		}

		/// <summary>
		/// Call this method to set <see cref="File"/> and
		/// <see cref="Directory"/> state.
		/// </summary>
		public static void Initialize(TransactionManager myInstance)
		{
			File.InitializeWith(myInstance._FileAdapter);
			Directory.InitializeWith(myInstance._DirectoryAdapter);
		}

		public static void Reset()
		{
			Directory.Reset();
			File.Reset();
		}

		Maybe<ITransaction> ITransactionManager.CurrentTopTransaction
		{
			get { return _ActivityManager.GetCurrentActivity().TopTransaction; }
		}

		Maybe<ITransaction> ITransactionManager.CurrentTransaction
		{
			get { return _ActivityManager.GetCurrentActivity().CurrentTransaction; }
		}

		uint ITransactionManager.Count
		{
			get { return _ActivityManager.GetCurrentActivity().Count; }
		}

		Maybe<ICreatedTransaction> ITransactionManager.CreateTransaction()
		{
			return ((ITransactionManager) this).CreateTransaction(new DefaultTransactionOptions());
		}

		Maybe<ICreatedTransaction> ITransactionManager.CreateTransaction(ITransactionOptions transactionOptions)
		{
			var activity = _ActivityManager.GetCurrentActivity();

			if (transactionOptions.Mode == TransactionScopeOption.Suppress)
				return Maybe.None<ICreatedTransaction>();

			var nextStackDepth = activity.Count + 1;
			var shouldFork = ShouldFork(transactionOptions, nextStackDepth);

			ITransaction tx;
			if (activity.Count == 0)
			{
				tx = new Transaction(new CommittableTransaction(new TransactionOptions
					{
						IsolationLevel = transactionOptions.IsolationLevel,
						Timeout = transactionOptions.Timeout
					}), nextStackDepth, transactionOptions, () => activity.Pop());
			}
			else
			{
				var clone = activity
					.CurrentTransaction.Value
					.Inner
					.DependentClone(transactionOptions.DependentOption);
				Contract.Assume(clone != null);
				
				Action onDispose = () => activity.Pop();
				tx = new Transaction(clone, nextStackDepth, transactionOptions, shouldFork ? null : onDispose);
			}

			if (!shouldFork) // forked transactions should not be on the current context's activity stack
				activity.Push(tx);

			Contract.Assume(tx.State == TransactionState.Active, "by c'tor post condition for both cases of the if statement");

			var m = Maybe.Some(new CreatedTransaction(tx, 
			    shouldFork, // we should only fork if we have a different current top transaction than the current
				ForkScopeFactory(tx)) as ICreatedTransaction);

			// warn if fork and the top transaction was just created
			if (transactionOptions.Fork && nextStackDepth == 1)
				_Logger.WarnFormat("transaction {0} created with Fork=true option, but was top-most "
				                   + "transaction in invocation chain. running transaction sequentially",
				                   tx.LocalIdentifier);

			Contract.Assume(m.HasValue && m.Value.Transaction.State == TransactionState.Active);

			return m;
		}

		private Func<IDisposable> ForkScopeFactory(ITransaction tx)
		{
			return () => {
				_ActivityManager.GetCurrentActivity().Push(tx);
				return new DisposableScope(_ActivityManager.GetCurrentActivity().Pop);
			};
		}

		private static bool ShouldFork(ITransactionOptions transactionOptions, uint nextStackDepth)
		{
			return transactionOptions.Fork && nextStackDepth > 1;
		}

		Maybe<ICreatedTransaction> ITransactionManager.CreateFileTransaction()
		{
			return ((ITransactionManager) this).CreateFileTransaction(new DefaultTransactionOptions());
		}

		Maybe<ICreatedTransaction> ITransactionManager.CreateFileTransaction(ITransactionOptions transactionOptions)
		{
			// TODO: we need to decide what transaction manager we want running the show and be smarter about this:
			var activity = _ActivityManager.GetCurrentActivity();
			var nextStackDepth = activity.Count + 1;
			var tx = new FileTransaction();
			var fork = ShouldFork(transactionOptions, nextStackDepth);
			if (!fork) activity.Push(tx);
			return new CreatedTransaction(tx, fork, ForkScopeFactory(tx));
		}

		/// <summary>
		/// Enlists a dependent task in the current top transaction.
		/// </summary>
		/// <param name="task">
		/// The task to enlist; this task is the action of running
		/// a dependent transaction on the thread pool.
		/// </param>
		public void EnlistDependentTask(Task task)
		{
			Contract.Requires(task != null);
			_ActivityManager.GetCurrentActivity().EnlistDependentTask(task);
		}

		private class DisposableScope : IDisposable
		{
			private readonly Func<ITransaction> _OnDispose;

			public DisposableScope(Func<ITransaction> onDispose)
			{
				Contract.Requires(onDispose != null);
				_OnDispose = onDispose;
			}

			public void Dispose()
			{
				_OnDispose();
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool isManaged)
		{
			if (!isManaged)
				return;
		}

		// for v3.1
		//void ITransactionManager.AddRetryPolicy(string policyKey, Func<Exception, bool> retryPolicy)
		//{
		//    throw new NotImplementedException();
		//}

		//void ITransactionManager.AddRetryPolicy(string policyKey, IRetryPolicy retryPolicy)
		//{
		//    throw new NotImplementedException();
		//}
	}
}