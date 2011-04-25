﻿using System;
using System.Diagnostics.Contracts;

namespace Castle.Services.Transaction
{
	[ContractClassFor(typeof (ICreatedTransaction))]
	internal abstract class ICreatedTransactionContract : ICreatedTransaction
	{
		#region Implementation of ICreatedTransaction

		ITransaction ICreatedTransaction.Transaction
		{
			get
			{
				Contract.Ensures(Contract.Result<ITransaction>() != null);
				throw new NotImplementedException();
			}
		}

		bool ICreatedTransaction.ShouldFork
		{
			get { throw new NotImplementedException(); }
		}

		public IDisposable GetForkScope()
		{
			Contract.Ensures(Contract.Result<IDisposable>() != null);
			throw new NotImplementedException();
		}

		#endregion
	}
}