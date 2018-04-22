﻿using System;
using System.Data;
using NHibernate;

namespace Simplify.Repository.FluentNHibernate
{
	/// <summary>
	/// Provides unit of work with manual open transaction
	/// </summary>
	public class BeginTransactUnitOfWork : UnitOfWork, IBeginTransactUnitOfWork
	{
		private ITransaction _transaction;

		/// <summary>
		/// Initializes a new instance of the <see cref="BeginTransactUnitOfWork"/> class.
		/// </summary>
		/// <param name="sessionFactory">The session factory.</param>
		public BeginTransactUnitOfWork(ISessionFactory sessionFactory) : base(sessionFactory)
		{
		}

		/// <summary>
		/// Begins the transaction.
		/// </summary>
		/// <param name="isolationLevel">The isolation level.</param>
		public virtual void BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
		{
			_transaction = Session.BeginTransaction(isolationLevel);
		}

		/// <summary>
		/// Commits transaction.
		/// </summary>
		/// <exception cref="System.InvalidOperationException">Oops! We don't have an active transaction</exception>
		public virtual void Commit()
		{
			if (!_transaction.IsActive)
				throw new InvalidOperationException("Oops! We don't have an active transaction");

			_transaction.Commit();
		}

		/// <summary>
		/// Rollbacks transaction.
		/// </summary>
		public virtual void Rollback()
		{
			if (_transaction.IsActive)
				_transaction.Rollback();
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			Session?.Dispose();
		}
	}
}