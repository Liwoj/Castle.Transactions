﻿#region License
// Copyright 2004-2022 Castle Project - https://www.castleproject.org/
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

namespace Castle.Services.Transaction
{
    using System;

    using Castle.Core.Logging;

    public class DefaultTransactionManager : MarshalByRefObject, ITransactionManager
    {
        private IActivityManager _activityManager;

        public event EventHandler<TransactionEventArgs> TransactionCreated;
        public event EventHandler<TransactionEventArgs> TransactionCompleted;
        public event EventHandler<TransactionEventArgs> TransactionRolledBack;
        public event EventHandler<TransactionFailedEventArgs> TransactionFailed;
        public event EventHandler<TransactionEventArgs> TransactionDisposed;
        public event EventHandler<TransactionEventArgs> ChildTransactionCreated;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultTransactionManager" /> class.
        /// </summary>
        public DefaultTransactionManager() : this(new CallContextActivityManager())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultTransactionManager" /> class.
        /// </summary>
        /// <exception cref="ArgumentNullException">activityManager is null</exception>
        /// <param name="activityManager">The activity manager.</param>
        public DefaultTransactionManager(IActivityManager activityManager)
        {
            _activityManager = activityManager ?? throw new ArgumentNullException("activityManager");

            if (Logger.IsDebugEnabled)
            {
                Logger.Debug("DefaultTransactionManager created.");
            }
        }

        public ILogger Logger { get; set; } = NullLogger.Instance;

        /// <summary>
        /// Gets or sets the activity manager.
        /// </summary>
        /// <exception cref="ArgumentNullException">value is null</exception>
        /// <value>The activity manager.</value>
        public IActivityManager ActivityManager
        {
            get => _activityManager;
            set => _activityManager = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// <see cref="ITransactionManager.CreateTransaction(TransactionMode,IsolationMode)" />.
        /// </summary>
        public ITransaction CreateTransaction(TransactionMode txMode, IsolationMode isolationMode)
        {
            return CreateTransaction(txMode, isolationMode, false, false);
        }

        public ITransaction CreateTransaction(TransactionMode txMode, IsolationMode iMode, bool isAmbient, bool isReadOnly)
        {
            txMode = ObtainDefaultTransactionMode(txMode);

            AssertModeSupported(txMode);

            if (CurrentTransaction == null &&
                (txMode == TransactionMode.Supported ||
                 txMode == TransactionMode.NotSupported))
            {
                return null;
            }

            TransactionBase transaction = null;

            if (CurrentTransaction != null)
            {
                if (txMode == TransactionMode.Requires || txMode == TransactionMode.Supported)
                {
                    transaction = ((TransactionBase) CurrentTransaction).CreateChildTransaction();

                    Logger.DebugFormat("Child transaction \"{0}\" created with mode '{1}'.", transaction.Name, txMode);
                }
            }

            if (transaction == null)
            {
                transaction = InstantiateTransaction(txMode, iMode, isAmbient, isReadOnly);

                if (isAmbient)
                {
#if MONO
                    throw new NotSupportedException("Distributed transactions are not supported on Mono.");
#else
                    transaction.CreateAmbientTransaction();
#endif
                }

                Logger.DebugFormat("Transaction \"{0}\" created.", transaction.Name);
            }

            _activityManager.CurrentActivity.Push(transaction);

            if (transaction.IsChildTransaction)
            {
                ChildTransactionCreated.Fire(this, new TransactionEventArgs(transaction));
            }
            else
            {
                TransactionCreated.Fire(this, new TransactionEventArgs(transaction));
            }

            return transaction;
        }

        private TransactionBase InstantiateTransaction(TransactionMode mode, IsolationMode isolationMode, bool ambient, bool readOnly)
        {
            var t = new TalkactiveTransaction(mode, isolationMode, ambient, readOnly)
            {
                Logger = Logger.CreateChildLogger("TalkactiveTransaction")
            };

            t.TransactionCompleted += CompletedHandler;
            t.TransactionRolledBack += RolledBackHandler;
            t.TransactionFailed += FailedHandler;

            return t;
        }

        private void CompletedHandler(object sender, TransactionEventArgs e)
        {
            TransactionCompleted.Fire(this, e);
        }

        private void RolledBackHandler(object sender, TransactionEventArgs e)
        {
            TransactionRolledBack.Fire(this, e);
        }

        private void FailedHandler(object sender, TransactionFailedEventArgs e)
        {
            TransactionFailed.Fire(this, e);
        }

        private void AssertModeSupported(TransactionMode mode)
        {
            var ctx = CurrentTransaction;

            if (mode == TransactionMode.NotSupported &&
                ctx != null &&
                ctx.Status == TransactionStatus.Active)
            {
                var message = "There is a transaction active and the transaction mode " +
                              "explicit says that no transaction is supported for this context";

                Logger.Error(message);

                throw new TransactionModeUnsupportedException(message);
            }
        }

        /// <summary>
        /// Gets the default transaction mode, i.e. the mode which is the current mode
        /// when <see cref="TransactionMode.Unspecified" /> is passed to <see cref="CreateTransaction(TransactionMode,IsolationMode)" />.
        /// </summary>
        /// <param name="mode">The mode which was passed.</param>
        /// <returns>
        /// Requires &lt;- mode = Unspecified mode &lt;- otherwise.
        /// </returns>
        protected virtual TransactionMode ObtainDefaultTransactionMode(TransactionMode mode)
        {
            return mode == TransactionMode.Unspecified ? TransactionMode.Requires : mode;
        }

        /// <summary>
        /// <see cref="ITransactionManager.CurrentTransaction" />
        /// </summary>
        /// <remarks>Thread-safety of this method depends on that of the <see cref="IActivityManager.CurrentActivity" />.</remarks>
        public ITransaction CurrentTransaction =>
            _activityManager.CurrentActivity.CurrentTransaction;

        /// <summary>
        /// <see cref="ITransactionManager.Dispose" />.
        /// </summary>
        /// <param name="transaction"></param>
        public virtual void Dispose(ITransaction transaction)
        {
            if (transaction == null)
            {
                throw new ArgumentNullException("transaction", "Tried to dispose a null transaction");
            }

            Logger.DebugFormat("Trying to dispose transaction {0}.", transaction.Name);

            if (CurrentTransaction != transaction)
            {
                throw new ArgumentException("Tried to dispose a transaction that is not on the current active transaction",
                                            "transaction");
            }

            _activityManager.CurrentActivity.Pop();

            if (transaction is IDisposable)
            {
                (transaction as IDisposable).Dispose();
            }

            if (transaction is IEventPublisher)
            {
                (transaction as IEventPublisher).TransactionCompleted -= CompletedHandler;
                (transaction as IEventPublisher).TransactionRolledBack -= RolledBackHandler;
                (transaction as IEventPublisher).TransactionFailed -= FailedHandler;
            }

            TransactionDisposed.Fire(this, new TransactionEventArgs(transaction));

            Logger.DebugFormat("Transaction {0} disposed successfully", transaction.Name);
        }

        /// <summary>
        /// <see cref="MarshalByRefObject.InitializeLifetimeService" />.
        /// </summary>
        /// <returns>always null</returns>
        public override object InitializeLifetimeService()
        {
            return null;
        }
    }
}